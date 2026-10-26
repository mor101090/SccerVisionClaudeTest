// Replay files written by the C# SimRunner (`npm run replay`): the exact same engine Unity runs, recorded
// at a fixed frame rate. Used by the dev viewer to watch matches without a Unity build.

export interface ReplayPlayer {
  index: number;
  number: number;
  name: string;
  shortName: string;
  role: string;
  passing: number;
  speed: number;
  stamina: number;
}

export interface ReplayTeam {
  name: string;
  shortName: string;
  kit: { primary: string; secondary: string; number: string; gk: string; gkNumber: string };
  players: ReplayPlayer[];
}

export interface ReplayEvent {
  t: number;
  minute: string;
  type: string;
  text: string;
  team: number;
  whistle: string | null;
  tone: "Normal" | "Goal" | "Foul" | "Card" | "Chance" | "Info";
}

export interface Replay {
  version: number;
  seed: number;
  fps: number;
  halfRealSeconds: number;
  halftimeRealSeconds: number;
  home: ReplayTeam;
  away: ReplayTeam;
  frames: number[][];
  events: ReplayEvent[];
}

export const PLAYERS = 22;
const PLAYER_BASE = 9;
const PLAYER_STRIDE = 5;
const OFFICIAL_BASE = PLAYER_BASE + PLAYERS * PLAYER_STRIDE;

export interface Snapshot {
  t: number;
  clock: number;
  phase: number;
  homeScore: number;
  awayScore: number;
  ball: { x: number; y: number; z: number; owner: number };
  players: { x: number; y: number; facing: number; jump: number; sentOff: boolean; gk: boolean; booked: boolean }[];
  officials: { x: number; y: number; flag: boolean }[];
  /** True when t falls in the half-time gap (no frames are recorded during the break). */
  halftime: boolean;
}

export function frameTime(f: number[]): number {
  return f[0] / 100;
}

/** Index of the last frame with time <= t. */
export function findFrame(frames: number[][], t: number): number {
  let lo = 0;
  let hi = frames.length - 1;
  if (t <= frameTime(frames[0])) return 0;
  if (t >= frameTime(frames[hi])) return hi;
  while (hi - lo > 1) {
    const mid = (lo + hi) >> 1;
    if (frameTime(frames[mid]) <= t) lo = mid;
    else hi = mid;
  }
  return lo;
}

function lerp(a: number, b: number, t: number) {
  return a + (b - a) * t;
}

function lerpAngle(a: number, b: number, t: number) {
  let d = b - a;
  while (d > Math.PI) d -= Math.PI * 2;
  while (d < -Math.PI) d += Math.PI * 2;
  return a + d * t;
}

export function sample(replay: Replay, t: number): Snapshot {
  const frames = replay.frames;
  const i = findFrame(frames, t);
  const a = frames[i];
  const b = frames[Math.min(i + 1, frames.length - 1)];
  const ta = frameTime(a);
  const tb = frameTime(b);
  const gap = tb - ta;
  const halftime = gap > 5;
  const k = gap > 1e-6 && !halftime ? Math.min(1, Math.max(0, (t - ta) / gap)) : 0;
  const players = [];
  for (let p = 0; p < PLAYERS; p++) {
    const o = PLAYER_BASE + p * PLAYER_STRIDE;
    const flags = a[o + 4];
    players.push({
      x: lerp(a[o], b[o], k) / 10,
      y: lerp(a[o + 1], b[o + 1], k) / 10,
      facing: lerpAngle(a[o + 2] / 100, b[o + 2] / 100, k),
      jump: lerp(a[o + 3], b[o + 3], k) / 100,
      sentOff: (flags & 1) !== 0,
      gk: (flags & 2) !== 0,
      booked: (flags & 4) !== 0,
    });
  }
  const officials = [];
  for (let j = 0; j < 3; j++) {
    const o = OFFICIAL_BASE + j * 3;
    officials.push({ x: lerp(a[o], b[o], k) / 10, y: lerp(a[o + 1], b[o + 1], k) / 10, flag: a[o + 2] === 1 });
  }
  // Ball teleports at restarts: do not interpolate across big jumps.
  const jump = Math.hypot(b[5] - a[5], b[6] - a[6]) > 150;
  const bk = jump ? 0 : k;
  return {
    t,
    clock: lerp(a[1], b[1], halftime ? 0 : k),
    phase: a[2],
    homeScore: a[3],
    awayScore: a[4],
    ball: { x: lerp(a[5], b[5], bk) / 10, y: lerp(a[6], b[6], bk) / 10, z: lerp(a[7], b[7], bk) / 100, owner: a[8] },
    players,
    officials,
    halftime,
  };
}

/** Football minute text, like the engine's MatchClock ("45+2'"). */
export function minuteText(clock: number, secondHalf: boolean): string {
  const end = secondHalf ? 5400 : 2700;
  if (clock < end) return Math.floor(clock / 60) + 1 + "'";
  return end / 60 + "+" + (Math.floor((clock - end) / 60) + 1) + "'";
}
