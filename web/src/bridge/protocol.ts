// Message contract between the Unity match (WebGL) and this React shell.
// Mirrors unity/SoccerVision/Assets/SoccerVision/Runtime/Bridge/BridgeProtocol.cs - keep both in sync.
//
// Unity -> React (react-unity-webgl events, payload = JSON string):
//   "MatchReady"  MatchReady   once per match (teams, timing)
//   "MatchState"  MatchState   4x per second (clock, score, stats)
//   "MatchEvent"  MatchEvent   every match event (commentary, whistles...)
// React -> Unity: sendMessage("SoccerVisionBridge", "OnCommand", JSON.stringify(BridgeCommand))

export const BRIDGE_OBJECT = "SoccerVisionBridge";
export const BRIDGE_METHOD = "OnCommand";

export type Tone = "Normal" | "Goal" | "Foul" | "Card" | "Chance" | "Info" | "Red";

export interface PlayerInfo {
  index: number;
  number: number;
  name: string;
  role: string;
  goalkeeper: boolean;
  passing: number;
  speed: number;
  stamina: number;
  finishing: number;
  tackling: number;
}

export interface TeamInfo {
  name: string;
  shortName: string;
  color: string;
  secondaryColor: string;
  goalkeeperColor: string;
  formation: string;
  players: PlayerInfo[];
}

export interface MatchReady {
  seed: number;
  halfRealSeconds: number;
  halftimeRealSeconds: number;
  home: TeamInfo;
  away: TeamInfo;
}

export interface TeamStats {
  goals: number;
  shots: number;
  shotsOnTarget: number;
  corners: number;
  fouls: number;
  yellowCards: number;
  redCards: number;
  offsides: number;
  passes: number;
  passesCompleted: number;
  saves: number;
  xg: number;
  possession: number;
  tackles: number;
  tacklesWon: number;
  dribbles: number;
  dribblesWon: number;
}

export interface MatchState {
  phase: "Live" | "Stoppage" | "GoalCelebration" | "HalfTimeBreak" | "FullTime";
  period: "NotStarted" | "FirstHalf" | "HalfTime" | "SecondHalf" | "FullTime";
  minute: string;
  clock: string;
  homeScore: number;
  awayScore: number;
  addedMinutes: number;
  halftimeRemaining: number;
  realElapsed: number;
  paused: boolean;
  speed: number;
  finished: boolean;
  homeStats: TeamStats;
  awayStats: TeamStats;
  /** Team in possession: 0 home, 1 away, -1 none. */
  ballTeam: number;
}

export interface MatchEvent {
  type: string;
  minute: string;
  clock: number;
  text: string;
  tone: Tone;
  whistle: string;
  team: number;
  player: number;
  playerNumber: number;
  other: number;
  x: number;
  y: number;
}

export interface TacticsSliders {
  mentality: number;
  defensiveLine: number;
  width: number;
  tempo: number;
  pressingIntensity: number;
  /** Marking system; omitted keeps the team's current one. */
  marking?: "zonal" | "manToMan";
  /** Tackling instruction; omitted keeps the team's current one. */
  tackling?: "stayOnFeet" | "normal" | "getStuckIn";
}

export type BridgeCommand =
  | { type: "pause" }
  | { type: "resume" }
  | { type: "togglePause" }
  | { type: "setSpeed"; value: number }
  | { type: "setHud"; flag: boolean }
  | { type: "restart"; seed?: number }
  | { type: "requestState" }
  | { type: "setTactics"; team: 0 | 1; tactics: TacticsSliders };

/** Event types that belong in the commentary box (whistles/net hits are audio-visual only). */
export function isCommentary(e: MatchEvent): boolean {
  return e.type !== "Whistle" && e.type !== "NetHit" && e.text.length > 0;
}

export function parsePayload<T>(payload: unknown): T | null {
  if (typeof payload !== "string") return null;
  try {
    return JSON.parse(payload) as T;
  } catch {
    return null;
  }
}
