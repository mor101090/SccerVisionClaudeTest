import type { GeneratorPlayer, GeneratorPosition } from "./generator";

/**
 * The starting XI and bench "the way the engine picks": a TypeScript port of `squad()` in tools/teams/build_teams.py,
 * which made the engine's own squads (GeneratedTeams.cs). Best per role, left-footers on the left.
 * Works on indices into the squad so callers can map back to their rows.
 */
type Kind = "gk" | "cb" | "fb" | "wide" | "cm" | "st";

export const SLOT_LABELS = [
  "GK",
  "RB",
  "CB",
  "CB",
  "LB",
  "RM",
  "CM",
  "CM",
  "LM",
  "ST",
  "ST",
] as const;

function g(p: GeneratorPlayer, grp: string, k: string): number {
  return p.stats[grp]?.[k] ?? 30;
}

export function roleScore(p: GeneratorPlayer, kind: Kind | "any"): number {
  switch (kind) {
    case "cb":
      return (
        g(p, "defense", "tackling") +
        g(p, "defense", "marking") +
        g(p, "athletic", "heading") +
        g(p, "athletic", "physical_power")
      );
    case "fb":
      return (
        g(p, "defense", "tackling") +
        g(p, "athletic", "speed") +
        g(p, "athletic", "acceleration") +
        g(p, "playmaking", "crossing") +
        p.stamina
      );
    case "wide":
      return (
        g(p, "athletic", "speed") +
        g(p, "athletic", "acceleration") +
        g(p, "athletic", "dribbling") +
        g(p, "playmaking", "crossing")
      );
    case "cm":
      return (
        g(p, "playmaking", "passing") +
        g(p, "playmaking", "creative") +
        g(p, "mental", "decisions") +
        g(p, "athletic", "ball_control") +
        p.stamina
      );
    case "st":
      return (
        g(p, "attack", "finishing") +
        g(p, "attack", "off_the_ball") +
        g(p, "athletic", "speed") +
        g(p, "athletic", "heading")
      );
    default: {
      // "gk" and "any": the mean of the group means (as the Python does for unknown kinds)
      const groups = Object.values(p.stats);
      return groups.reduce((a, v) => {
        const xs = Object.values(v);
        return a + xs.reduce((x, y) => x + y, 0) / xs.length;
      }, 0);
    }
  }
}

const leftFooted = (p: GeneratorPlayer) => p.preferred_foot.dominant_foot === "Left";

/** Stable sort by score, best first (Python's sorted() is stable too). */
function best(pool: number[], squad: GeneratorPlayer[], kind: Kind): number[] {
  return [...pool].sort((a, b) => roleScore(squad[b]!, kind) - roleScore(squad[a]!, kind));
}

function pick(
  pool: number[],
  squad: GeneratorPlayer[],
  positions: GeneratorPosition[],
  kind: Kind,
  n: number,
): number[] {
  const chosen: number[] = [];
  for (const pos of positions) {
    const cand = best(
      pool.filter((i) => squad[i]!.position === pos && !chosen.includes(i)),
      squad,
      kind,
    );
    chosen.push(...cand.slice(0, n - chosen.length));
    if (chosen.length === n) break;
  }
  if (chosen.length < n) {
    const rest = best(
      pool.filter((i) => !chosen.includes(i) && squad[i]!.position !== "goalkeeper"),
      squad,
      kind,
    );
    chosen.push(...rest.slice(0, n - chosen.length));
  }
  for (const i of chosen) pool.splice(pool.indexOf(i), 1);
  return chosen;
}

/** Two players for the two flanks: a left-footer goes left when only one of them is. Returns [right, left]. */
function side(two: number[], squad: GeneratorPlayer[]): [number, number] {
  const [a, b] = two as [number, number];
  if (leftFooted(squad[a]!) && !leftFooted(squad[b]!)) return [b, a];
  return [a, b];
}

function pickSided(
  pool: number[],
  squad: GeneratorPlayer[],
  positions: GeneratorPosition[],
  kind: Kind,
  tol: number,
): [number, number] {
  const cands: number[] = [];
  for (const pos of positions) {
    cands.push(
      ...best(
        pool.filter((i) => squad[i]!.position === pos && !cands.includes(i)),
        squad,
        kind,
      ),
    );
  }
  if (cands.length < 2) {
    cands.push(
      ...best(
        pool.filter((i) => !cands.includes(i) && squad[i]!.position !== "goalkeeper"),
        squad,
        kind,
      ),
    );
  }
  const top = roleScore(squad[cands[0]!]!, kind);
  const good = cands.slice(0, 5).filter((i) => roleScore(squad[i]!, kind) >= tol * top);
  const lefts = good.filter((i) => leftFooted(squad[i]!));
  const rights = good.filter((i) => !leftFooted(squad[i]!));
  let right: number;
  let left: number;
  if (lefts.length && rights.length) {
    left = lefts[0]!;
    right = rights[0] !== left ? rights[0]! : rights[1]!;
  } else {
    [right, left] = side([cands[0]!, cands[1]!], squad);
  }
  for (const i of [right, left]) pool.splice(pool.indexOf(i), 1);
  return [right, left];
}

export type Lineup = {
  /** 11 squad indices in slot order: GK, RB, CB right, CB left, LB, RM, CM right, CM left, LM, ST right, ST left. */
  starters: number[];
  /** Up to 9 substitutes: a keeper, three defenders, three midfielders, two forwards. */
  bench: number[];
};

export function suggestLineup(squad: GeneratorPlayer[]): Lineup {
  let pool = squad.map((_, i) => i);
  const keepers = best(
    pool.filter((i) => squad[i]!.position === "goalkeeper"),
    squad,
    "gk",
  );
  const gk = keepers[0]!;
  const gk2 = keepers[1];
  pool = pool.filter((i) => squad[i]!.position !== "goalkeeper");
  const cbs = pick(pool, squad, ["defender"], "cb", 2);
  const [rb, lb] = pickSided(pool, squad, ["fullback", "defender"], "fb", 0.8);
  const [rm, lm] = pickSided(pool, squad, ["winger", "midfielder"], "wide", 0.85);
  const cms = pick(pool, squad, ["midfielder"], "cm", 2);
  const sts = pick(pool, squad, ["striker", "winger"], "st", 2);
  const [cbr, cbl] = side(cbs, squad);
  const [cmr, cml] = side(cms, squad);
  const [str, stl] = side(sts, squad);
  const starters = [gk, rb, cbr, cbl, lb, rm, cmr, cml, lm, str, stl];
  const bench: number[] = gk2 === undefined ? [] : [gk2];
  bench.push(...pick(pool, squad, ["defender", "fullback"], "cb", 2));
  bench.push(...pick(pool, squad, ["fullback", "defender"], "fb", 1));
  bench.push(...pick(pool, squad, ["midfielder", "winger"], "cm", 2));
  bench.push(...pick(pool, squad, ["winger", "midfielder"], "wide", 1));
  bench.push(...pick(pool, squad, ["striker", "winger"], "st", 2));
  return { starters, bench };
}

/** Which slot a player of this position naturally takes when the manager puts him in the XI. */
export function slotsFor(position: GeneratorPosition): number[] {
  switch (position) {
    case "goalkeeper":
      return [0];
    case "fullback":
      return [1, 4, 2, 3];
    case "defender":
      return [2, 3, 1, 4];
    case "midfielder":
      return [6, 7, 5, 8];
    case "winger":
      return [5, 8, 9, 10];
    case "striker":
      return [9, 10, 5, 8];
  }
}
