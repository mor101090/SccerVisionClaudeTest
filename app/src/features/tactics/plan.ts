import { FORMATION_NAMES, FORMATIONS } from "./formations";

/**
 * The manager's saved tactics, as the engine reads them (TeamPlan.cs text: "formation=4-2-3-1;orders=...;line=0.5").
 * Encodings follow the classic page (tools/demo-page/js/08-tactic.js, 09-manager.js, 10-board-positions.js) so a plan
 * saved here plays the same in the classic game and the engine.
 */

/** Engine PlayerRole names in enum order (PlayerOrders.cs). Index = the number in `orders`. */
export const ROLES = [
  "Auto",
  "Shot-stopper",
  "Sweeper-keeper",
  "Dominant keeper",
  "Stopper",
  "Ball-playing defender",
  "Cover defender",
  "Attacking full-back",
  "Inverted full-back",
  "Defensive full-back",
  "Destroyer",
  "Deep-lying playmaker",
  "Box-to-box",
  "Anchor",
  "All-rounder",
  "Tempo setter",
  "Carrier",
  "Classic playmaker",
  "Shadow striker",
  "Wide playmaker",
  "Inverted winger",
  "Classic winger",
  "Wide target man",
  "Centre forward",
  "Target forward",
  "Second striker",
  "False nine",
  "Regista",
  "Trequartista",
] as const;

/** Roles each position kind may take (classic KIND_ROLES; kind from formations.ts). */
export const KIND_ROLES: number[][] = [
  [0, 1, 2, 3],
  [0, 4, 5, 6],
  [0, 9, 8, 7],
  [0, 10, 11, 12, 13, 27],
  [0, 14, 15, 16],
  [0, 17, 18, 19, 28],
  [0, 20, 21, 22],
  [0, 23, 24, 25, 26],
];

export type Amount = 0 | 1 | 2;
export const AMOUNTS = ["Less", "Mixed", "More"] as const;

export type PlayerOrdersView = {
  role: number;
  longShots: Amount;
  dribbling: Amount;
  aggression: Amount;
};

export const PASSING = ["Short combinations", "Mixed", "Balls in behind"] as const;
export const TEMPO = ["Slow", "Normal", "Fast"] as const;
export const AGGRESSION = ["Gentle", "Balanced", "Aggressive"] as const;
/** [engine value, label] in the order the buttons show them. */
export const DEFENCE: [number, string][] = [
  [3, "Park the bus"],
  [0, "Balanced"],
  [5, "Inverted full-backs"],
  [2, "Offside trap"],
  [4, "High backline"],
  [1, "High press"],
];
export const ATTACK: [number, string][] = [
  [1, "Crosses into the box"],
  [0, "Balanced"],
  [2, "Runs in behind"],
  [3, "Long shots"],
];
export const STYLE: [number, string][] = [
  [2, "Time wasting"],
  [1, "Possession"],
  [0, "Balanced"],
  [3, "Counter-attacks"],
  [4, "Relentless pressure"],
];
const TEMPO_VALUES = [0.15, 0.5, 0.85];
const AGGRESSION_VALUES = [0, 0.5, 1];

export type TeamInstructions = {
  /** 0..100 (deep block → high line). */
  line: number;
  /** 0..100 (narrow → wide). */
  width: number;
  passing: 0 | 1 | 2;
  tempo: 0 | 1 | 2;
  aggression: 0 | 1 | 2;
  defence: number;
  attack: number;
  special: number;
};

export type SetPieces = {
  /** Shirt numbers in order of preference. */
  cornersRight: number[];
  cornersLeft: number[];
  freeKicksRight: number[];
  freeKicksLeft: number[];
  penalties: number[];
};

export type TeamPlanView = {
  formation: string;
  orders: PlayerOrdersView[] | null;
  team: TeamInstructions;
  setPieces: SetPieces;
  /** Fields this editor does not change (slots, pos, corner routines and setups), kept as they were. */
  extra: Record<string, string>;
};

export const DEFAULT_TEAM: TeamInstructions = {
  line: 50,
  width: 50,
  passing: 1,
  tempo: 1,
  aggression: 1,
  defence: 0,
  attack: 0,
  special: 0,
};

export const DEFAULT_ORDERS: PlayerOrdersView = {
  role: 0,
  longShots: 1,
  dribbling: 1,
  aggression: 1,
};

const clampInt = (v: number, lo: number, hi: number) =>
  Number.isFinite(v) ? Math.max(lo, Math.min(hi, Math.round(v))) : lo;
const nearest = (values: number[], v: number) =>
  values.reduce((best, x, i) => (Math.abs(x - v) < Math.abs(values[best]! - v) ? i : best), 0);
const list = (v: string) =>
  v
    .split(".")
    .map(Number)
    .filter((n) => Number.isInteger(n) && n > 0 && n < 100);

export function isFormation(name: string): name is (typeof FORMATION_NAMES)[number] {
  return (FORMATION_NAMES as readonly string[]).includes(name);
}

export function parsePlan(text: string, fallbackFormation = "4-4-2"): TeamPlanView {
  const plan: TeamPlanView = {
    formation: isFormation(fallbackFormation) ? fallbackFormation : "4-4-2",
    orders: null,
    team: { ...DEFAULT_TEAM },
    setPieces: {
      cornersRight: [],
      cornersLeft: [],
      freeKicksRight: [],
      freeKicksLeft: [],
      penalties: [],
    },
    extra: {},
  };
  for (const part of text.split(";")) {
    const eq = part.indexOf("=");
    if (eq <= 0) continue;
    const key = part.slice(0, eq).trim();
    const v = part.slice(eq + 1).trim();
    switch (key) {
      case "formation":
        if (isFormation(v)) plan.formation = v;
        break;
      case "orders": {
        const o = v.split(",");
        if (o.length !== 11) break;
        plan.orders = o.map((s) => {
          const [r = 0, ls = 1, dr = 1, ag = 1] = s.split(".").map(Number);
          return {
            role: clampInt(r, 0, ROLES.length - 1),
            longShots: clampInt(ls, 0, 2) as Amount,
            dribbling: clampInt(dr, 0, 2) as Amount,
            aggression: clampInt(ag, 0, 2) as Amount,
          };
        });
        break;
      }
      case "line":
      case "width":
        plan.team[key] = clampInt(Number(v) * 100, 0, 100);
        break;
      case "tempo":
        plan.team.tempo = nearest(TEMPO_VALUES, Number(v)) as 0 | 1 | 2;
        break;
      case "aggression":
        plan.team.aggression = nearest(AGGRESSION_VALUES, Number(v)) as 0 | 1 | 2;
        break;
      case "passing":
        plan.team.passing = clampInt(Number(v), 0, 2) as 0 | 1 | 2;
        break;
      case "defence":
        plan.team.defence = clampInt(Number(v), 0, 5);
        break;
      case "attack":
        plan.team.attack = clampInt(Number(v), 0, 3);
        break;
      case "special":
        plan.team.special = clampInt(Number(v), 0, 4);
        break;
      case "ctr":
        plan.setPieces.cornersRight = list(v);
        break;
      case "ctl":
        plan.setPieces.cornersLeft = list(v);
        break;
      case "fkr":
        plan.setPieces.freeKicksRight = list(v);
        break;
      case "fkl":
        plan.setPieces.freeKicksLeft = list(v);
        break;
      case "pen":
        plan.setPieces.penalties = list(v);
        break;
      case "takers": {
        const [c = "", f = "", p = ""] = v.split(",");
        plan.setPieces.cornersRight = plan.setPieces.cornersLeft = list(c);
        plan.setPieces.freeKicksRight = plan.setPieces.freeKicksLeft = list(f);
        plan.setPieces.penalties = list(p);
        break;
      }
      default:
        if (key && v) plan.extra[key] = v;
    }
  }
  return plan;
}

/** Roles that no longer fit their slot's position kind fall back to Auto (as the classic page does). */
export function fitOrders(
  formation: string,
  orders: PlayerOrdersView[] | null,
): PlayerOrdersView[] {
  const slots = FORMATIONS[formation] ?? FORMATIONS["4-4-2"]!;
  return slots.map((slot, i) => {
    const o = orders?.[i] ?? DEFAULT_ORDERS;
    return KIND_ROLES[slot.kind]!.includes(o.role) ? o : { ...o, role: 0 };
  });
}

export function planText(plan: TeamPlanView): string {
  const t = plan.team;
  const sp = plan.setPieces;
  const orders = fitOrders(plan.formation, plan.orders);
  // A new shape gets its own positions: hand-placed spots belong to the old one.
  const extra = Object.entries(plan.extra).filter(([k]) => k !== "pos");
  const parts = [
    `formation=${plan.formation}`,
    `orders=${orders.map((o) => `${o.role}.${o.longShots}.${o.dribbling}.${o.aggression}`).join(",")}`,
    `line=${t.line / 100}`,
    `width=${t.width / 100}`,
    `passing=${t.passing}`,
    `tempo=${TEMPO_VALUES[t.tempo]}`,
    `aggression=${AGGRESSION_VALUES[t.aggression]}`,
    `defence=${t.defence}`,
    `attack=${t.attack}`,
    `special=${t.special}`,
    ...(sp.cornersRight.length ? [`ctr=${sp.cornersRight.join(".")}`] : []),
    ...(sp.cornersLeft.length ? [`ctl=${sp.cornersLeft.join(".")}`] : []),
    ...(sp.freeKicksRight.length ? [`fkr=${sp.freeKicksRight.join(".")}`] : []),
    ...(sp.freeKicksLeft.length ? [`fkl=${sp.freeKicksLeft.join(".")}`] : []),
    ...(sp.penalties.length ? [`pen=${sp.penalties.join(".")}`] : []),
    ...extra.map(([k, v]) => `${k}=${v}`),
  ];
  return parts.join(";");
}

type Option = { value: number; label: string };
/**
 * Choices that don't go together (classic tactic locks, js/20-tactic-locks.js): each blocked value with the reason,
 * and the allowed range of the line and width sliders.
 */
export function tacticLocks(t: TeamInstructions) {
  const range = { line: [0, 100] as [number, number], width: [0, 100] as [number, number] };
  const why = { line: "", width: "" };
  const off: Record<
    "passing" | "tempo" | "aggression" | "defence" | "attack" | "special",
    Map<number, string>
  > = {
    passing: new Map(),
    tempo: new Map(),
    aggression: new Map(),
    defence: new Map(),
    attack: new Map(),
    special: new Map(),
  };
  const lim = (k: "line" | "width", lo: number, hi: number, reason: string) => {
    range[k] = [Math.max(range[k][0], lo), Math.min(range[k][1], hi)];
    why[k] = reason;
  };
  const ban = (k: keyof typeof off, v: number, reason: string) => off[k].set(v, reason);
  if (t.defence === 3) {
    lim("line", 0, 30, "Park the bus: the line stays deep");
    ban("special", 4, "Park the bus: no relentless pressure");
    ban("tempo", 2, "Park the bus: no fast tempo");
  }
  if (t.defence === 2) lim("line", 50, 100, "Offside trap: the line has to hold high");
  if (t.defence === 4) lim("line", 65, 100, "High backline: the line lives up the pitch");
  if (t.attack === 1) lim("width", 50, 100, "Crosses into the box: the play goes wide");
  if (t.attack === 2) ban("passing", 0, "Runs in behind need balls played behind");
  if (t.special === 1) {
    ban("passing", 2, "Possession: the ball is kept, not played long");
    ban("tempo", 2, "Possession: no fast tempo");
  }
  if (t.special === 2) {
    ban("tempo", 1, "Time wasting: the tempo is slow");
    ban("tempo", 2, "Time wasting: the tempo is slow");
  }
  if (t.special === 3) {
    ban("tempo", 0, "Counter-attacks are fast — never slow");
    ban("passing", 0, "Counter-attacks are direct — no short combinations");
    lim("line", 0, 60, "Counter-attacks: space behind the opponents to break into");
  }
  if (t.special === 4) {
    lim("line", 55, 100, "Relentless pressure: the whole team up");
    ban("defence", 3, "Relentless pressure: no parking the bus");
    ban("tempo", 0, "Relentless pressure: never slow");
  }
  if (t.passing === 0) {
    ban("special", 3, "Short combinations: no counter-attacks");
    ban("attack", 2, "Short combinations: no runs in behind");
  }
  if (t.passing === 2) ban("special", 1, "Balls in behind: no possession game");
  if (t.tempo === 0) {
    ban("special", 3, "Slow tempo: no counter-attacks");
    ban("special", 4, "Slow tempo: no relentless pressure");
  }
  if (t.tempo === 2) {
    ban("special", 1, "Fast tempo: no possession game");
    ban("special", 2, "Fast tempo: no time wasting");
    ban("defence", 3, "Fast tempo: no parking the bus");
  }
  if (t.tempo === 1) ban("special", 2, "Time wasting needs a slow tempo");
  return { range, why, off };
}

/** Line and width pulled into their allowed range. */
export function applyLocks(t: TeamInstructions): TeamInstructions {
  const { range } = tacticLocks(t);
  return {
    ...t,
    line: Math.min(range.line[1], Math.max(range.line[0], t.line)),
    width: Math.min(range.width[1], Math.max(range.width[0], t.width)),
  };
}

export function lineLabel(v: number): string {
  return v < 30
    ? "Deep block"
    : v < 45
      ? "Deep"
      : v <= 55
        ? "Middle"
        : v <= 70
          ? "High"
          : "High line";
}
export function widthLabel(v: number): string {
  return v < 30
    ? "Narrow"
    : v < 45
      ? "Fairly narrow"
      : v <= 55
        ? "Normal"
        : v <= 70
          ? "Fairly wide"
          : "Wide";
}

/** One-line summary ("Middle line, normal width, mixed passing, normal tempo, balanced"). */
export function summaryLine(t: TeamInstructions): string {
  const style = STYLE.find(([v]) => v === t.special)?.[1] ?? "Balanced";
  return `${lineLabel(t.line)} line, ${widthLabel(t.width).toLowerCase()} width, ${PASSING[t.passing].toLowerCase()} passing, ${TEMPO[t.tempo].toLowerCase()} tempo, ${style.toLowerCase()}`;
}

export type { Option };
