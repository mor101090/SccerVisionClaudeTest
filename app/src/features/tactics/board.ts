import { FORMATIONS, type FormationSlot } from "./formations";

/**
 * Where a slot sits on a vertical pitch drawing, 0..1 each way (own goal at the bottom, attacking up). Engine
 * coordinates: forward −52.5 (own goal line) … 0 (halfway, the kick-off shape), lateral < 0 is the right wing.
 */
export function boardPoint(slot: Pick<FormationSlot, "forward" | "lateral">): {
  x: number;
  y: number;
} {
  return {
    x: 0.5 - slot.lateral / 72,
    y: 0.93 - ((slot.forward + 52.5) / 52.5) * 0.8,
  };
}

/**
 * The squad's XI is stored in 4-4-2 slot order (players.starter_slot). For another shape, each new position takes
 * the nearest remaining starter, the way the engine reshapes the team before kick-off (TeamPlan.Apply).
 * Returns, per slot of `formation`, the index into `starters442` (11 entries, slot order).
 */
export function assignToFormation(formation: string): number[] {
  const from = FORMATIONS["4-4-2"]!;
  const to = FORMATIONS[formation] ?? from;
  const free = new Set(from.map((_, i) => i));
  return to.map((slot, i) => {
    if (i === 0) {
      free.delete(0);
      return 0;
    }
    let best = -1;
    let bestD = Infinity;
    for (const j of free) {
      if (j === 0) continue;
      const d = (from[j]!.forward - slot.forward) ** 2 + (from[j]!.lateral - slot.lateral) ** 2;
      if (d < bestD) {
        bestD = d;
        best = j;
      }
    }
    free.delete(best);
    return best;
  });
}
