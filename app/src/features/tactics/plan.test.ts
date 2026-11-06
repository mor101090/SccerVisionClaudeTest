import { describe, expect, it } from "vitest";

import { FORMATION_NAMES, FORMATIONS } from "./formations";
import {
  applyLocks,
  DEFAULT_TEAM,
  fitOrders,
  parsePlan,
  planText,
  summaryLine,
  tacticLocks,
} from "./plan";
import { planTextIsSafe } from "../lobby/plan-text";

describe("tactics plan", () => {
  it("has 11 slots in every engine formation, the keeper first", () => {
    expect(FORMATION_NAMES).toHaveLength(12);
    for (const name of FORMATION_NAMES) {
      expect(FORMATIONS[name]).toHaveLength(11);
      expect(FORMATIONS[name]![0]!.abbr).toBe("GK");
    }
  });

  it("round-trips a plan and keeps fields it does not edit", () => {
    const text =
      "formation=4-2-3-1;orders=1.1.1.1,7.1.2.1,4.1.1.2,5.1.1.1,8.1.1.1,20.2.2.1,11.1.1.1,12.1.1.1,21.1.2.1,17.2.1.1,23.2.1.1;" +
      "line=0.6;width=0.7;passing=2;tempo=0.85;aggression=1;defence=1;attack=2;special=3;ctr=10.11;pen=9;slots=1,2,4,5,3,6,8,7,10,11,9";
    const plan = parsePlan(text);
    expect(plan.formation).toBe("4-2-3-1");
    expect(plan.team).toEqual({
      line: 60,
      width: 70,
      passing: 2,
      tempo: 2,
      aggression: 2,
      defence: 1,
      attack: 2,
      special: 3,
    });
    expect(plan.setPieces.cornersRight).toEqual([10, 11]);
    expect(plan.extra.slots).toBe("1,2,4,5,3,6,8,7,10,11,9");
    const out = planText(plan);
    expect(planTextIsSafe(out)).toBe(true);
    expect(parsePlan(out)).toEqual({ ...plan, orders: fitOrders("4-2-3-1", plan.orders) });
  });

  it("falls back to defaults and drops roles that don't fit the new shape", () => {
    const plan = parsePlan("", "3-5-2");
    expect(plan.formation).toBe("3-5-2");
    expect(plan.team).toEqual(DEFAULT_TEAM);
    const orders = fitOrders("4-4-2", [
      { role: 23, longShots: 1, dribbling: 1, aggression: 1 }, // centre forward in goal: back to Auto
      ...Array.from({ length: 10 }, () => ({
        role: 0,
        longShots: 1 as const,
        dribbling: 1 as const,
        aggression: 1 as const,
      })),
    ]);
    expect(orders[0]!.role).toBe(0);
  });

  it("applies the classic tactic locks", () => {
    const parked = { ...DEFAULT_TEAM, defence: 3, line: 80 };
    expect(applyLocks(parked).line).toBe(30);
    expect(tacticLocks(parked).off.tempo.has(2)).toBe(true);
    expect(summaryLine(DEFAULT_TEAM)).toBe(
      "Middle line, normal width, mixed passing, normal tempo, balanced",
    );
  });
});
