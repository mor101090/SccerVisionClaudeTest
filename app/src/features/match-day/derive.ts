import { playerAt, type Replay, type ReplayEvent, type ReplayTeam } from "@/engine/replay/replay";

/**
 * Pure read-outs of a match for the match-day screens (Figma "Match day & more – screens"): the score line, live
 * stats, ratings, who is on the pitch, substitutions left, timeline markers and momentum. Everything comes from the
 * engine's replay data (header, events, stats rows); nothing is invented.
 */

/** Numbers per stats row (ReplayRecorder.StatsLength): time, 14 team stats, 22 energies, 22 ratings ×10, 4 pass counts. */
export const STATS_LENGTH = 63;

/** Header fields the base `ReplayTeam` type leaves out (ReplayRecorder.Team). */
export type BenchPlayer = {
  number: number;
  name: string;
  shortName: string;
  role: string;
  stamina: number;
};
export type FinalStats = {
  PossessionSeconds: number;
  Shots: number;
  ShotsOnTarget: number;
  Tackles: number;
  TacklesWon: number;
  Dribbles: number;
  DribblesWon: number;
  ExpectedGoals: number;
  Corners: number;
  Fouls: number;
  Passes: number;
  PassesCompleted: number;
};
export type PlayerRecord = {
  number: number;
  name: string;
  rating: number;
  goals: number;
  assists: number;
  on: number;
  off: number;
  motm?: boolean;
};
export type HeaderTeam = ReplayTeam & {
  manager?: string;
  formation?: string;
  orders?: string[];
  tactics?: {
    line: number;
    width: number;
    passing: number;
    tempo: number;
    style: number;
    defence: number;
    attack: number;
    special: number;
  };
  bench?: BenchPlayer[];
  stats?: FinalStats;
  records?: PlayerRecord[];
};
export type MatchRules = { maxSubs: number; windows: number };

export function teamOf(replay: Replay, side: 0 | 1): HeaderTeam {
  return (side === 0 ? replay.home : replay.away) as HeaderTeam;
}

export function rulesOf(replay: Replay): MatchRules {
  const r = (replay as Replay & { rules?: Partial<MatchRules> }).rules;
  return { maxSubs: r?.maxSubs ?? 5, windows: r?.windows ?? 3 };
}

/** The worker sends stats rows in flat batches (one per chunk): split them into rows. */
export function statRows(chunks: number[][]): number[][] {
  const rows: number[][] = [];
  for (const c of chunks) {
    for (let i = 0; i + STATS_LENGTH <= c.length; i += STATS_LENGTH)
      rows.push(c.slice(i, i + STATS_LENGTH));
  }
  return rows;
}

/** The latest stats row at replay time t (seconds), or null before the first one. */
export function rowAt(rows: number[][], t: number): number[] | null {
  let found: number[] | null = null;
  for (const r of rows) {
    if ((r[0] ?? 0) / 100 > t) break;
    found = r;
  }
  return found;
}

export type Pair = [number, number];
export type TeamStats = {
  possession: Pair;
  shots: Pair;
  onTarget: Pair;
  tackles: Pair;
  tacklesWon: Pair;
  dribbles: Pair;
  passes: Pair;
  passAccuracy: Pair;
  corners?: Pair;
  fouls?: Pair;
};

const pct = (a: number, b: number): Pair => {
  const sum = a + b;
  if (sum <= 0) return [50, 50];
  const h = Math.round((a / sum) * 100);
  return [h, 100 - h];
};
const acc = (done: number, all: number) => (all > 0 ? Math.round((done / all) * 100) : 0);

/** Team stats from a live stats row. */
export function statsFromRow(row: number[]): TeamStats {
  const n = (i: number) => row[i] ?? 0;
  return {
    possession: pct(n(1), n(2)),
    shots: [n(3), n(4)],
    onTarget: [n(5), n(6)],
    tackles: [n(7), n(8)],
    tacklesWon: [n(9), n(10)],
    dribbles: [n(11), n(12)],
    passes: [n(59), n(60)],
    passAccuracy: [acc(n(61), n(59)), acc(n(62), n(60))],
  };
}

/** Team stats from the full-time header (adds corners and fouls). */
export function statsFromFinal(home: FinalStats, away: FinalStats): TeamStats {
  return {
    possession: pct(home.PossessionSeconds, away.PossessionSeconds),
    shots: [home.Shots, away.Shots],
    onTarget: [home.ShotsOnTarget, away.ShotsOnTarget],
    tackles: [home.Tackles, away.Tackles],
    tacklesWon: [home.TacklesWon, away.TacklesWon],
    dribbles: [home.Dribbles, away.Dribbles],
    passes: [home.Passes, away.Passes],
    passAccuracy: [acc(home.PassesCompleted, home.Passes), acc(away.PassesCompleted, away.Passes)],
    corners: [home.Corners, away.Corners],
    fouls: [home.Fouls, away.Fouls],
  };
}

/** Rating 1-10 of frame index i (0-21) in a stats row; null before the engine has rated him. */
export function ratingAt(row: number[] | null, i: number): number | null {
  const v = row?.[37 + i] ?? 0;
  return v > 0 ? v / 10 : null;
}

/** Energy left 0-100 of frame index i. */
export function energyAt(row: number[] | null, i: number): number | null {
  const v = row?.[15 + i];
  return v === undefined ? null : v;
}

export type OnPitch = { index: number; number: number; name: string; role: string };

/** The eleven of one side at time t: the header's eleven with every substitution made so far. */
export function elevenAt(replay: Replay, side: 0 | 1, t: number): OnPitch[] {
  const team = teamOf(replay, side);
  return team.players.slice(0, 11).map((p, k) => {
    const index = side * 11 + k;
    const now = playerAt(replay, index, t);
    const benchP = team.bench?.find((b) => b.number === now.number);
    return {
      index,
      number: now.number,
      name: benchP && now.number !== p.number ? benchP.shortName : p.shortName,
      role: p.role,
    };
  });
}

/** Bench of one side with who already played (a substituted player can't come back, nor go on twice). */
export function benchAt(replay: Replay, side: 0 | 1, t: number) {
  const team = teamOf(replay, side);
  const on = new Set(elevenAt(replay, side, t).map((p) => p.number));
  const used = new Set(substitutionsAt(replay.events, side, t).map((s) => s.on));
  return (team.bench ?? []).map((b) => ({ ...b, used: used.has(b.number) || on.has(b.number) }));
}

export type Substitution = { t: number; minute: string; on: number; off: number };

export function substitutionsAt(events: ReplayEvent[], side: 0 | 1, t = Infinity): Substitution[] {
  const out: Substitution[] = [];
  for (const e of events) {
    if (e.t > t) break;
    if (e.type !== "Substitution" || e.team !== side || !e.s) continue;
    const s = e.s.split("|");
    out.push({ t: e.t, minute: e.minute, on: Number(s[0]), off: Number(s[2]) });
  }
  return out;
}

/** Changes made at one stoppage count as one window (classic windowsUsed); the half-time break is free. */
export function windowsUsed(events: ReplayEvent[], side: 0 | 1, t = Infinity): number {
  const secondHalf = events.find((e) => e.type === "SecondHalfStart")?.t ?? Infinity;
  let n = 0;
  let last = -1e9;
  for (const c of substitutionsAt(events, side, t)) {
    if (c.t >= secondHalf - 200 && c.t <= secondHalf + 3) continue;
    if (c.t - last > 25) n++;
    last = c.t;
  }
  return n;
}

export type Scorer = { name: string; minutes: string[]; own: boolean };

/** Goals of one side up to t, grouped by scorer ("O. Silva 23', 61'"). An own goal counts for the side it helped. */
export function scorersAt(replay: Replay, side: 0 | 1, t = Infinity): Scorer[] {
  const list: Scorer[] = [];
  for (const e of replay.events) {
    if (e.t > t) break;
    if (e.type !== "Goal" || e.team !== side) continue;
    const own = (e.v ?? 0) > 0.5;
    const name = e.p !== undefined && e.p >= 0 ? playerAt(replay, e.p, e.t).shortName : "Goal";
    const hit = list.find((s) => s.name === name && s.own === own);
    if (hit) hit.minutes.push(e.minute);
    else list.push({ name, minutes: [e.minute], own });
  }
  return list;
}

export function scorerLine(scorers: Scorer[]): string {
  return scorers.map((s) => `${s.name}${s.own ? " (og)" : ""} ${s.minutes.join(", ")}`).join(" · ");
}

/** Events the match screens list: goals, cards, chances, injuries, substitutions, the whistles. */
const KEY_TYPES = new Set([
  "Goal",
  "YellowCard",
  "RedCard",
  "PenaltyAwarded",
  "PenaltyMissed",
  "HitWoodwork",
  "Substitution",
  "Injury",
  "HalfTime",
  "FullTime",
]);
export type Moment = ReplayEvent & { kind: MomentKind };
export type MomentKind = "goal" | "card" | "red" | "chance" | "sub" | "whistle" | "info";

export function momentKind(e: ReplayEvent): MomentKind {
  if (e.type === "Goal") return "goal";
  if (e.type === "YellowCard") return "card";
  if (e.type === "RedCard") return "red";
  if (e.type === "Substitution") return "sub";
  if (e.type === "HalfTime" || e.type === "FullTime" || e.type === "SecondHalfStart")
    return "whistle";
  if (e.tone === "Chance") return "chance";
  return "info";
}

/** Engine events too routine for the commentary (every pass, throw-in, duel). */
const ROUTINE = new Set([
  "Pass",
  "Cross",
  "LongBall",
  "Clearance",
  "Header",
  "Tackle",
  "Interception",
  "ThrowIn",
  "GoalKick",
  "Whistle",
  "NetHit",
  "Advantage",
  "ShoulderDuel",
  "ShieldOut",
  "GoalkeeperClaim",
  "SlideTackleMissed",
  "SubstitutionRequest",
]);

/** A knock the player plays on with (Injury detail 0) is commentary, not a key moment. */
const minorKnock = (e: ReplayEvent) => e.type === "Injury" && (e.detail ?? 0) === 0;

/** Commentary: shots, saves, fouls, corners, cards, goals, changes (not the routine passes), newest first. */
export function commentaryAt(events: ReplayEvent[], t: number, n = 60): Moment[] {
  const out: Moment[] = [];
  for (const e of events) {
    if (e.t > t) break;
    if (!e.text || (e.tone === "Normal" && ROUTINE.has(e.type))) continue;
    out.push({ ...e, kind: momentKind(e) });
  }
  return out.slice(-n).reverse();
}

/** Key moments for the timeline and the reports: goals, cards, substitutions, big chances. */
export function keyMoments(events: ReplayEvent[], t = Infinity): Moment[] {
  const out: Moment[] = [];
  for (const e of events) {
    if (e.t > t) break;
    if (!KEY_TYPES.has(e.type) || e.type === "HalfTime" || e.type === "FullTime" || minorKnock(e))
      continue;
    out.push({ ...e, kind: momentKind(e) });
  }
  return out;
}

/**
 * Momentum: which side pressed in each slice of the last `span` seconds of replay, -1 (away) … 1 (home), from shots,
 * chances and goals (an event-weighted tilt; no event = level).
 */
export function momentum(events: ReplayEvent[], t: number, span = 120, bars = 15): number[] {
  const out = new Array<number>(bars).fill(0);
  const from = t - span;
  const weight = (e: ReplayEvent) =>
    e.type === "Goal"
      ? 3
      : e.tone === "Chance"
        ? 2
        : e.type === "Shot" || e.type === "Corner"
          ? 1
          : 0;
  for (const e of events) {
    if (e.t > t) break;
    if (e.t < from) continue;
    const w = weight(e);
    if (!w) continue;
    const k = Math.min(bars - 1, Math.floor(((e.t - from) / span) * bars));
    out[k] = (out[k] ?? 0) + (e.team === 0 ? w : -w);
  }
  return out.map((v) => Math.max(-1, Math.min(1, v / 3)));
}

/** Rating colour band (classic ratingColour), as a palette tone. */
export function ratingTone(r: number): "goal" | "accent" | "card" | "chance" | "foul" {
  return r >= 7.5 ? "goal" : r >= 6.8 ? "accent" : r >= 6.2 ? "card" : r >= 5.6 ? "chance" : "foul";
}

/** Position badge group for a role abbreviation (GK, RB, CB, DM, CM, AM, RW, ST …). */
export function positionGroup(role: string): "GK" | "DEF" | "MID" | "WNG" | "ST" {
  const r = role.toUpperCase();
  if (r === "GK") return "GK";
  if (/^(RB|LB|CB|RWB|LWB|DEF|SW)$/.test(r)) return "DEF";
  if (/^(RW|LW|RM|LM|WNG)$/.test(r)) return "WNG";
  if (/^(ST|CF|SS|FW)$/.test(r)) return "ST";
  return "MID";
}

/** "HAR" from "Harbour Rovers" when the engine has no short name. */
export function shortCode(team: Pick<ReplayTeam, "name" | "shortName">): string {
  const s = team.shortName?.trim();
  if (s && s.length <= 4) return s.toUpperCase();
  return team.name
    .replace(/[^A-Za-z ]/g, "")
    .slice(0, 3)
    .toUpperCase();
}

export function initials(name: string): string {
  const w = name.split(/\s+/).filter(Boolean);
  return ((w[0]?.[0] ?? "") + (w[1]?.[0] ?? w[0]?.[1] ?? "")).toUpperCase();
}
