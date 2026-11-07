"use client";

import { useState } from "react";

import { FullTime, type FinalHeader } from "./full-time";
import { LiveScreen } from "./live-screen";
import { usePlanChanges } from "./plan-changes";
import { type HubData, PlayHub } from "./play-hub";
import { PreMatch } from "./pre-match";
import { type MatchSetup, useMatch } from "./use-match";

/**
 * /play (Figma "Match day & more – screens"): Play hub → Pre-match → Live (half-time dialog, substitutions and
 * tactics drawers) → Full-time report. One engine session runs through all of them.
 */
export function MatchDay({ data }: { data: HubData }) {
  const session = useMatch();
  const [setup, setSetup] = useState<MatchSetup | null>(null);
  const plan = usePlanChanges(
    session.replay,
    setup?.plan ?? data.club?.plan ?? null,
    session.setPlan,
  );

  const start = (s: MatchSetup) => {
    setSetup(s);
    void session.prepare(s);
  };
  const { phase, replay } = session;
  const strength = setup
    ? setup.awayStrength[0]!.toUpperCase() + setup.awayStrength.slice(1)
    : "Medium";

  if (phase === "prematch" && replay)
    return (
      <PreMatch
        replay={replay}
        strength={strength}
        plan={plan}
        onKickOff={session.kickOff}
        onCancel={session.reset}
      />
    );

  if ((phase === "live" || phase === "halftime") && replay)
    return <LiveScreen session={session} plan={plan} onLeave={session.reset} />;

  if (phase === "finished" && replay)
    return (
      <FullTime
        replay={replay}
        final={(session.live.current?.finalHeader as FinalHeader) ?? null}
        home={session.tick.home}
        away={session.tick.away}
        save={session.save}
        onRetrySave={() => void session.saveMatch()}
        onPlayAgain={session.reset}
      />
    );

  return <PlayHub data={data} phase={phase} error={session.error} onStart={start} />;
}
