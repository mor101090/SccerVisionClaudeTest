/**
 * Messages between the page and the engine worker (src/engine/engine.worker.ts).
 * Mirrors the JSExport surface of sim/SoccerVision.Wasm/Program.cs (LiveEngine).
 */

export type MatchMode = "staged" | "random" | "live" | "online";
/** Mirrors `enum Weather` in Simulation/Core/MatchConditions.cs. */
export type Weather = "Sunny" | "Cloudy" | "Rain" | "Snow";
export type Competition = "League" | "Cup" | "LeagueCup";

export interface StartOptions {
  seed: number;
  mode: MatchMode;
  weather: Weather;
  /** Pitch maintenance 0..1. */
  maintenance: number;
  /** Wind speed m/s. */
  wind: number;
  /** Wind direction in degrees. */
  windAngle: number;
  night: boolean;
  /** 0: generate the whole match as fast as possible. N: stay at most N frames ahead of the viewer. */
  lead: number;
  /** Home manager's plan text (TeamPlan.cs format). Live and online only. */
  plan?: string;
  /** Send the header only; play nothing until `go`. */
  hold?: boolean;
  competition?: Competition;
  /** How good each squad is (default medium: the squads the game always had). Online play: always medium. */
  homeStrength?: TeamStrength;
  awayStrength?: TeamStrength;
  /** A manager's own squad for the home side (LiveEngine.UseHomeTeam text, see features/team/engine-team.ts). */
  homeTeam?: string;
}

/** Mirrors `TeamStrength` parsed by LiveEngine.Start (Program.cs). */
export type TeamStrength = "weak" | "medium" | "strong";

export type ToWorker =
  | { cmd: "init"; base: string }
  | ({ cmd: "start"; token: number } & StartOptions)
  | { cmd: "go"; token: number }
  | { cmd: "progress"; token: number; frames: number }
  | { cmd: "plan"; token: number; team: 0 | 1; plan: string }
  | { cmd: "sub"; token: number; team: 0 | 1; out: number; in: number }
  | { cmd: "stop"; token: number }
  /** Lockstep: never simulate past this many frames (-1: no limit). */
  | { cmd: "limit"; token: number; frames: number }
  /** Lockstep: apply an input at `frame` (null: at the next step). The worker answers `applied`. */
  | { cmd: "schedule"; token: number; input: ScheduledInput };

export type InputAction =
  | { kind: "plan"; team: 0 | 1; plan: string }
  | { kind: "sub"; team: 0 | 1; out: number; in: number };

export type ScheduledInput = InputAction & { id: string; frame: number | null };

export interface ChunkMessage {
  type: "chunk";
  token: number;
  /** Numbers per frame (ReplayRecorder.FrameLength). */
  len: number;
  frames: Int32Array;
  /** JSON array text of ReplayEvent. */
  events: string;
  stats: Int32Array;
  finished: boolean;
  /** Full-time header JSON text once finished. */
  final: string | null;
}

export type FromWorker =
  | { type: "ready" }
  | { type: "header"; token: number; header: string }
  | ChunkMessage
  /** An input was applied before the step that produced frame `frame` onwards. */
  | { type: "applied"; token: number; id: string; frame: number; order: number }
  | { type: "error"; message: string };
