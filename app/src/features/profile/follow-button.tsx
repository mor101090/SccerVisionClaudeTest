"use client";

import { useOptimistic, useTransition } from "react";

import { followLeagueAction } from "./actions";

export function FollowButton({ leagueId, following }: { leagueId: string; following: boolean }) {
  const [on, setOn] = useOptimistic(following);
  const [pending, start] = useTransition();
  return (
    <button
      type="button"
      aria-pressed={on}
      disabled={pending}
      onClick={() =>
        start(async () => {
          setOn(!on);
          await followLeagueAction(leagueId, !on);
        })
      }
      className={`inline-flex h-9 items-center rounded-lg px-3 text-[13px] font-semibold focus-visible:outline-2 focus-visible:outline-link ${on ? "border border-border bg-panel text-text hover:bg-border" : "text-link hover:bg-panel"}`}
    >
      {on ? "Following" : "Follow"}
    </button>
  );
}
