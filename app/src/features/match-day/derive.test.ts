import { describe, expect, it } from "vitest";

import type { Replay, ReplayEvent } from "@/engine/replay/replay";
import { DEFAULT_TEAM, planText } from "@/features/tactics/plan";

import {
  benchAt,
  commentaryAt,
  elevenAt,
  keyMoments,
  momentum,
  positionGroup,
  ratingAt,
  rowAt,
  scorerLine,
  scorersAt,
  STATS_LENGTH,
  statRows,
  statsFromRow,
  substitutionsAt,
  windowsUsed,
} from "./derive";
import { adjust, planFromHeader } from "./mentality";

const player = (i: number, role = "CM") => ({
  index: i,
  number: i + 1,
  name: `Player ${i}`,
  shortName: `P. ${i}`,
  role,
  passing: 50,
  speed: 50,
  stamina: 50,
});
const team = (name: string, base: number) => ({
  name,
  shortName: name.slice(0, 3).toUpperCase(),
  kit: { primary: "#000", secondary: "#fff", number: "#fff", gk: "#ff0", gkNumber: "#000" },
  players: Array.from({ length: 11 }, (_, k) => player(base + k, k === 0 ? "GK" : "CM")),
  bench: [{ number: 30, name: "Sub Thirty", shortName: "S. Thirty", role: "ST", stamina: 60 }],
});
const ev = (e: Partial<ReplayEvent>): ReplayEvent => ({
  t: 0,
  minute: "1'",
  type: "Pass",
  text: "",
  team: 0,
  whistle: null,
  tone: "Normal",
  ...e,
});

const replay = {
  version: 1,
  seed: 1,
  fps: 10,
  halfRealSeconds: 300,
  halftimeRealSeconds: 10,
  home: team("Harbour Rovers", 0),
  away: team("Northside", 11),
  frames: [],
  events: [
    ev({ t: 10, minute: "5'", type: "Goal", tone: "Goal", text: "GOAL!", team: 0, p: 9 }),
    ev({ t: 20, minute: "9'", type: "Pass", text: "A pass." }),
    ev({ t: 50, minute: "20'", type: "Goal", tone: "Goal", text: "GOAL!", team: 0, p: 9 }),
    ev({ t: 60, minute: "25'", type: "YellowCard", tone: "Card", text: "Yellow.", team: 1, p: 14 }),
    ev({
      t: 90,
      minute: "40'",
      type: "Goal",
      tone: "Goal",
      text: "Own goal.",
      team: 1,
      p: 3,
      v: 1,
    }),
    ev({ t: 120, minute: "46'", type: "SecondHalfStart", text: "Second half." }),
    ev({
      t: 150,
      minute: "60'",
      type: "Substitution",
      tone: "Info",
      text: "Sub.",
      team: 0,
      p: 9,
      s: "30|S. Thirty|10",
    }),
  ],
} as unknown as Replay;

describe("match-day read-outs", () => {
  it("splits flat stats batches into rows and finds the row at a time", () => {
    const a = new Array(STATS_LENGTH).fill(0);
    a[0] = 100;
    const b = new Array(STATS_LENGTH).fill(1);
    b[0] = 300;
    const rows = statRows([[...a, ...b]]);
    expect(rows).toHaveLength(2);
    expect(rowAt(rows, 0.5)).toBeNull();
    expect(rowAt(rows, 2)?.[0]).toBe(100);
    expect(rowAt(rows, 9)?.[0]).toBe(300);
  });

  it("reads possession as a percentage, pass accuracy and ratings", () => {
    const row = new Array(STATS_LENGTH).fill(0);
    row[1] = 300;
    row[2] = 100;
    row[59] = 200;
    row[61] = 150;
    row[37 + 4] = 72;
    const s = statsFromRow(row);
    expect(s.possession).toEqual([75, 25]);
    expect(s.passAccuracy[0]).toBe(75);
    expect(ratingAt(row, 4)).toBe(7.2);
    expect(ratingAt(row, 5)).toBeNull();
  });

  it("groups goals by scorer and gives an own goal to the side it helped", () => {
    expect(scorerLine(scorersAt(replay, 0))).toBe("P. 9 5', 20'");
    expect(scorerLine(scorersAt(replay, 1))).toBe("P. 3 (og) 40'");
    expect(scorersAt(replay, 0, 15)[0]?.minutes).toEqual(["5'"]);
  });

  it("follows substitutions on the pitch and on the bench", () => {
    expect(elevenAt(replay, 0, 100)[9]?.number).toBe(10);
    const after = elevenAt(replay, 0, 200)[9];
    expect(after).toMatchObject({ number: 30, name: "S. Thirty", index: 9 });
    expect(benchAt(replay, 0, 200)[0]?.used).toBe(true);
    expect(substitutionsAt(replay.events, 0)).toEqual([{ t: 150, minute: "60'", on: 30, off: 10 }]);
    expect(windowsUsed(replay.events, 0)).toBe(1);
  });

  it("lists commentary newest first without routine passes, and key moments in order", () => {
    const c = commentaryAt(replay.events, 100);
    expect(c.map((m) => m.minute)).toEqual(["40'", "25'", "20'", "5'"]);
    expect(keyMoments(replay.events).map((m) => m.kind)).toEqual([
      "goal",
      "goal",
      "card",
      "goal",
      "sub",
    ]);
  });

  it("tilts momentum to the side that scored", () => {
    const m = momentum(replay.events, 60, 60, 6);
    expect(m.some((v) => v > 0)).toBe(true);
    expect(m.every((v) => v >= -1 && v <= 1)).toBe(true);
  });

  it("maps roles to position groups", () => {
    expect(["GK", "RB", "DM", "LW", "ST"].map(positionGroup)).toEqual([
      "GK",
      "DEF",
      "MID",
      "WNG",
      "ST",
    ]);
  });
});

describe("in-match changes", () => {
  it("attacking raises the line and tempo, defensive lowers them, within the tactic locks", () => {
    const base = { ...DEFAULT_TEAM };
    expect(adjust(base, "Balanced", [])).toEqual(base);
    expect(adjust(base, "Attacking", [])).toMatchObject({ line: 70, tempo: 2 });
    expect(adjust(base, "Defensive", [])).toMatchObject({ line: 30, tempo: 0 });
    expect(adjust(base, "Balanced", ["push", "drop"]).line).toBe(50);
    expect(adjust(base, "Balanced", ["wide", "stuck"])).toMatchObject({ width: 70, aggression: 2 });
    // Park the bus keeps the line deep however hard the manager pushes.
    expect(adjust({ ...base, defence: 3, line: 20 }, "Attacking", ["push"]).line).toBe(30);
  });

  it("rebuilds the plan the engine started with from the header", () => {
    const plan = planFromHeader({
      ...replay.home,
      formation: "4-3-3",
      tactics: {
        line: 60,
        width: 40,
        passing: 0,
        tempo: 2,
        style: 1,
        defence: 0,
        attack: 0,
        special: 0,
      },
      orders: Array.from({ length: 11 }, () => "0.1.1.1"),
    });
    expect(plan.formation).toBe("4-3-3");
    expect(planText(plan)).toContain("line=0.6");
    expect(plan.orders).toHaveLength(11);
  });
});
