import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

import { engineAttributes, footRatings, toEngineTeam } from "./engine-team";
import { generatorPlayerSchema } from "./generator";
import { suggestLineup } from "./lineup";

// The engine's own Red squad was built from this file by tools/teams/build_teams.py (GeneratedTeams.cs).
const raw = JSON.parse(
  readFileSync(join(__dirname, "../../../../data/teams/red-rovers.json"), "utf8"),
) as unknown[];
const squad = raw.map((p) => generatorPlayerSchema.parse(p));
const label = (i: number) => `${squad[i]!.name} #${squad[i]!.shirt_number}`;

describe("suggestLineup (port of build_teams.squad)", () => {
  it("picks the same XI, in the same slots, as GeneratedTeams.Red", () => {
    const { starters } = suggestLineup(squad);
    expect(starters.map(label)).toEqual([
      "Robert Martin #5",
      "Robert Taylor #1",
      "James Smith #63",
      "Richard Johnson #51",
      "Chris White #47",
      "William White #70",
      "James Harris #18",
      "Richard Taylor #69",
      "John Thomas #14",
      "John Harris #11",
      "Chris White #16",
    ]);
  });

  it("picks the same bench of nine", () => {
    const { bench } = suggestLineup(squad);
    expect(bench.map((i) => squad[i]!.shirt_number)).toEqual([92, 49, 61, 73, 54, 60, 75, 56, 37]);
  });
});

describe("engine attributes", () => {
  it("map a keeper exactly as GeneratedTeams.cs does", () => {
    const keeper = squad.find((p) => p.name === "Robert Martin")!;
    expect(engineAttributes(keeper)).toEqual([
      34, 60, 54, 61, 20, 20, 69, 35, 57, 25, 12, 15, 20, 68, 50, 41, 61, 54, 71, 73, 35, 28, 71,
      72, 68, 68, 71,
    ]);
  });

  it("map an outfield player exactly as GeneratedTeams.cs does", () => {
    const cb = squad.find((p) => p.shirt_number === 63)!;
    expect(engineAttributes(cb)).toEqual([
      32, 26, 68, 40, 87, 94, 86, 67, 65, 68, 27, 27, 23, 89, 76, 68, 60, 88, 85, 86, 74, 52, 15,
      15, 22, 15, 18,
    ]);
  });

  it("writes one header line and 20 player lines", () => {
    const { starters, bench } = suggestLineup(squad);
    const text = toEngineTeam({
      name: "Red | Rovers",
      shortName: "RED",
      manager: "redcoach",
      kit: {
        primary: "#d32f2f",
        secondary: "#1b1b1b",
        number: "#f2f2f2",
        gkPrimary: "#f9d71c",
        gkNumber: "#1b1b1b",
        gkSecondary: "",
      },
      seed: 42,
      starters: starters.map((i) => squad[i]!),
      bench: bench.map((i) => squad[i]!),
    });
    const lines = text.split("\n");
    expect(lines).toHaveLength(21);
    expect(lines[0]).toBe("Red   Rovers|RED|redcoach|#d32f2f|#1b1b1b|#f2f2f2|#f9d71c|#1b1b1b||42");
    expect(lines[1]!.split("|").slice(0, 8)).toEqual([
      "Robert",
      "Martin",
      "5",
      "Goalkeeper",
      "GK",
      "184",
      "66",
      "Normal",
    ]);
    expect(lines[6]!.split("|")[3]).toBe("Midfielder"); // RM slot
  });
});

describe("feet in stars (port of build_teams.foot_ratings)", () => {
  // Every P(...) line of GeneratedTeams.cs: id, ..., number, ..., left, right.
  const cs = readFileSync(
    join(
      __dirname,
      "../../../../unity/SoccerVision/Assets/SoccerVision/Simulation/Demo/GeneratedTeams.cs",
    ),
    "utf8",
  );
  const engine = [
    ...cs.matchAll(
      /P\((\d+), "([^"]*)", "([^"]*)", (\d+), PositionGroup\.\w+, "\w+", \d+, \d+, FitnessLevel\.\w+, (\d), (\d),/g,
    ),
  ].map((m) => ({ id: +m[1]!, number: +m[4]!, feet: [+m[5]!, +m[6]!] }));

  for (const [team, file, base] of [
    ["Red", "red-rovers", 100],
    ["Blue", "blue-falcons", 200],
  ] as const) {
    it(`match GeneratedTeams.${team} for all 20 players`, () => {
      const players = (
        JSON.parse(
          readFileSync(join(__dirname, `../../../../data/teams/${file}.json`), "utf8"),
        ) as unknown[]
      ).map((p) => generatorPlayerSchema.parse(p));
      const mine = engine.filter((e) => e.id > base && e.id < base + 100);
      expect(mine).toHaveLength(20);
      for (const e of mine) {
        const p = players.find((x) => x.shirt_number === e.number)!;
        expect([p.name, footRatings(p)]).toEqual([p.name, e.feet]);
      }
    });
  }

  it("keep the generator's own stars and rate the strong foot 5", () => {
    const p = generatorPlayerSchema.parse({
      ...(
        JSON.parse(
          readFileSync(join(__dirname, "../../../../data/teams/red-rovers.json"), "utf8"),
        ) as Record<string, unknown>[]
      )[0],
      preferred_foot: { dominant_foot: "Left", weak_foot_level: 1, weak_foot_stars: 2 },
    });
    expect(footRatings(p)).toEqual([5, 2]);
  });
});
