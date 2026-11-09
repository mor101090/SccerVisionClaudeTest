import {
  applyLocks,
  DEFAULT_ORDERS,
  DEFAULT_TEAM,
  parsePlan,
  type PlayerOrdersView,
  type TeamInstructions,
  type TeamPlanView,
} from "@/features/tactics/plan";

import type { HeaderTeam } from "./derive";

/**
 * In-match changes (Figma "Live match": mentality + touchline instructions), as edits of the team plan the engine
 * reads (TeamPlan.cs). Each one moves real engine settings: the defensive line, width, tempo, tackling. They change
 * the plan of this match only, never the saved tactics.
 */

export const MENTALITIES = ["Defensive", "Balanced", "Attacking"] as const;
export type Mentality = (typeof MENTALITIES)[number];

export const INSTRUCTIONS = [
  { id: "push", label: "Push forward", hint: "Defensive line up" },
  { id: "drop", label: "Drop deeper", hint: "Defensive line down" },
  { id: "wide", label: "Play wider", hint: "More width" },
  { id: "stuck", label: "Get stuck in", hint: "Aggressive tackling" },
] as const;
export type InstructionId = (typeof INSTRUCTIONS)[number]["id"];

const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));
const step = (v: 0 | 1 | 2, d: number) => clamp(v + d, 0, 2) as 0 | 1 | 2;

/** The team instructions with a mentality and the active instructions applied on top of `base`. */
export function adjust(
  base: TeamInstructions,
  mentality: Mentality,
  active: readonly InstructionId[],
): TeamInstructions {
  const t = { ...base };
  if (mentality === "Attacking") {
    t.line = clamp(t.line + 20, 0, 100);
    t.tempo = step(t.tempo, 1);
  } else if (mentality === "Defensive") {
    t.line = clamp(t.line - 20, 0, 100);
    t.tempo = step(t.tempo, -1);
  }
  // Pushing forward and dropping deeper cancel out.
  const on = new Set(active);
  if (on.has("push") && !on.has("drop")) t.line = clamp(t.line + 15, 0, 100);
  if (on.has("drop") && !on.has("push")) t.line = clamp(t.line - 15, 0, 100);
  if (on.has("wide")) t.width = clamp(t.width + 20, 0, 100);
  if (on.has("stuck")) t.aggression = 2;
  return applyLocks(t);
}

/** The plan this side started with, as the header reports it (formation, per-slot orders, team instructions). */
export function planFromHeader(team: HeaderTeam, saved?: string | null): TeamPlanView {
  const formation = team.formation ?? "4-4-2";
  const plan = parsePlan(saved ?? `formation=${formation}`, formation);
  if (!saved && team.tactics) {
    const x = team.tactics;
    plan.team = {
      ...DEFAULT_TEAM,
      line: x.line,
      width: x.width,
      passing: clamp(x.passing, 0, 2) as 0 | 1 | 2,
      tempo: clamp(x.tempo, 0, 2) as 0 | 1 | 2,
      aggression: clamp(x.style, 0, 2) as 0 | 1 | 2,
      defence: x.defence,
      attack: x.attack,
      special: x.special,
    };
  }
  if (!saved && team.orders?.length === 11) {
    plan.orders = team.orders.map((o): PlayerOrdersView => {
      const [role, longShots, dribbling, aggression] = o.split(".").map(Number);
      return {
        ...DEFAULT_ORDERS,
        role: role ?? 0,
        longShots: clamp(longShots ?? 1, 0, 2) as 0 | 1 | 2,
        dribbling: clamp(dribbling ?? 1, 0, 2) as 0 | 1 | 2,
        aggression: clamp(aggression ?? 1, 0, 2) as 0 | 1 | 2,
      };
    });
  }
  return plan;
}
