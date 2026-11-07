"use client";

import { useCallback, useMemo, useState } from "react";

import type { Replay } from "@/engine/replay/replay";
import { planText, type TeamPlanView } from "@/features/tactics/plan";

import { teamOf } from "./derive";
import { adjust, type InstructionId, type Mentality, planFromHeader } from "./mentality";

/** What the manager changed during this match, on top of the plan the club started with. */
export type Changes = {
  mentality: Mentality;
  instructions: InstructionId[];
  formation: string;
  line: number;
  width: number;
};

export function compose(base: TeamPlanView, c: Changes): TeamPlanView {
  return {
    ...base,
    formation: c.formation,
    orders: c.formation === base.formation ? base.orders : null,
    team: adjust({ ...base.team, line: c.line, width: c.width }, c.mentality, c.instructions),
  };
}

export function initialChanges(base: TeamPlanView): Changes {
  return {
    mentality: "Balanced",
    instructions: [],
    formation: base.formation,
    line: base.team.line,
    width: base.team.width,
  };
}

/** The base plan (header + saved tactics) and the changes; every change is sent to the engine at once. */
export function usePlanChanges(
  replay: Replay | null,
  saved: string | null | undefined,
  send: (text: string) => void,
) {
  const base = useMemo(
    () => (replay ? planFromHeader(teamOf(replay, 0), saved) : null),
    [replay, saved],
  );
  const [state, setState] = useState<{ base: TeamPlanView; changes: Changes } | null>(null);
  // A new match (new header) starts from its own plan.
  const changes = base && state?.base === base ? state.changes : base ? initialChanges(base) : null;

  const apply = useCallback(
    (next: Partial<Changes>) => {
      if (!base || !changes) return;
      const c = { ...changes, ...next };
      setState({ base, changes: c });
      send(planText(compose(base, c)));
    },
    [base, changes, send],
  );

  const resetToSaved = useCallback(() => {
    if (!base) return;
    const c = initialChanges(base);
    setState({ base, changes: c });
    send(planText(base));
  }, [base, send]);

  return { base, changes, apply, resetToSaved };
}

export type PlanChanges = ReturnType<typeof usePlanChanges>;
