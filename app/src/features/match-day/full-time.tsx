"use client";

import Link from "next/link";

import { AppIcon } from "@/components/app-shell/app-icon";
import { Card } from "@/components/app-shell/ui";
import type { Replay } from "@/engine/replay/replay";

import {
  type HeaderTeam,
  keyMoments,
  type PlayerRecord,
  scorerLine,
  scorersAt,
  statsFromFinal,
  teamOf,
} from "./derive";
import { useScrollLock } from "./live-screen";
import { MatchPanel, StatsList } from "./match-panel";
import { Crest } from "./pre-match";
import { ScoreBug } from "./score-bar";
import { btn, CommentaryLine, RatingBadge } from "./ui";
import type { SaveState } from "./use-match";

export type FinalHeader = { home?: HeaderTeam; away?: HeaderTeam } | null;

/** Result for the manager (home side): Win / Draw / Defeat. */
function resultOf(home: number, away: number) {
  return home > away
    ? { text: "Win", cls: "bg-goal/20 text-goal" }
    : home < away
      ? { text: "Defeat", cls: "bg-foul/20 text-foul" }
      : { text: "Draw", cls: "bg-border text-text" };
}

function potm(final: FinalHeader): (PlayerRecord & { team: string }) | null {
  for (const side of [final?.home, final?.away]) {
    const r = side?.records?.find((x) => x.motm);
    if (r && side) return { ...r, team: side.name };
  }
  return null;
}

export function SaveNote({ save, onRetry }: { save: SaveState; onRetry: () => void }) {
  if (save.kind === "saving") return <p className="text-[13px] text-muted">Saving to My Games…</p>;
  if (save.kind === "saved")
    return (
      <p
        data-testid="saved"
        className="flex items-center gap-2 rounded-lg border border-goal/60 bg-goal/10 p-3 text-[13px] text-text"
      >
        <AppIcon name="check" size={16} className="text-goal" />
        Saved to My Games with the replay.
      </p>
    );
  if (save.kind === "signin")
    return (
      <p className="rounded-lg bg-info/16 p-3 text-[13px] text-text">
        <Link
          className="font-semibold text-link hover:underline"
          href="/auth/sign-in?redirectTo=/play"
        >
          Log in
        </Link>{" "}
        to save your matches and watch them again.
      </p>
    );
  if (save.kind === "error")
    return (
      <p
        role="alert"
        className="flex flex-wrap items-center gap-2 rounded-lg bg-foul/15 p-3 text-[13px] text-text"
      >
        Couldn&apos;t save the match: {save.message}
        <button type="button" className="font-semibold text-link hover:underline" onClick={onRetry}>
          Try again
        </button>
      </p>
    );
  return null;
}

export function FullTime({
  replay,
  final,
  home,
  away,
  save,
  onRetrySave,
  onPlayAgain,
}: {
  replay: Replay;
  final: FinalHeader;
  home: number;
  away: number;
  save: SaveState;
  onRetrySave: () => void;
  onPlayAgain: () => void;
}) {
  useScrollLock();
  const h = teamOf(replay, 0);
  const a = teamOf(replay, 1);
  const res = resultOf(home, away);
  const star = potm(final);
  const stats =
    final?.home?.stats && final.away?.stats
      ? statsFromFinal(final.home.stats, final.away.stats)
      : null;
  const moments = keyMoments(replay.events);
  const end = Infinity;

  return (
    <div
      className="fixed inset-0 z-40 flex flex-col overflow-y-auto bg-bg text-text"
      data-testid="full-time"
    >
      <header className="sticky top-0 z-10 grid grid-cols-[auto_1fr_auto] items-center gap-2 border-b border-border bg-panel px-3 py-2 sm:px-5">
        <Link
          href="/home"
          className="flex items-center gap-1 rounded-md px-2 py-1.5 text-[13px] text-muted hover:text-text"
        >
          <AppIcon name="chevronLeft" size={16} />
          <span className="hidden sm:inline">Back to Home</span>
        </Link>
        <div className="flex justify-center">
          <ScoreBug replay={replay} t={end} clock="FT" home={home} away={away} state="ft" />
        </div>
        <span />
      </header>

      <main className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-3 sm:p-5">
        <section className="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-3 rounded-2xl border border-border bg-panel-2 p-5">
          <div className="flex min-w-0 flex-col items-start gap-2">
            <Crest team={h} size={56} />
            <h1 className="max-w-full truncate text-base font-extrabold sm:text-xl">{h.name}</h1>
            <p className="text-xs text-muted">{scorerLine(scorersAt(replay, 0))}</p>
          </div>
          <div className="flex flex-col items-center gap-1">
            <span className={`rounded-full px-3 py-0.5 text-[11px] font-semibold ${res.cls}`}>
              {res.text} · Friendly
            </span>
            <span className="text-4xl font-extrabold tabular-nums sm:text-6xl">
              {home}–{away}
            </span>
            <span className="text-xs text-muted">Full time</span>
          </div>
          <div className="flex min-w-0 flex-col items-end gap-2 text-right">
            <Crest team={a} size={56} />
            <h2 className="max-w-full truncate text-base font-extrabold sm:text-xl">{a.name}</h2>
            <p className="text-xs text-muted">{scorerLine(scorersAt(replay, 1))}</p>
          </div>
        </section>

        <div className="grid gap-4 lg:grid-cols-[300px_1fr_320px]">
          <div className="flex flex-col gap-4">
            {star && (
              <Card title="Player of the match" icon="star">
                <div className="flex items-center gap-3">
                  <span className="flex size-12 items-center justify-center rounded-full bg-selected text-sm font-bold text-white">
                    {star.number}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-bold">{star.name}</p>
                    <p className="text-xs text-muted">
                      {star.team}
                      {star.goals ? ` · ${star.goals} ${star.goals === 1 ? "goal" : "goals"}` : ""}
                      {star.assists
                        ? ` · ${star.assists} ${star.assists === 1 ? "assist" : "assists"}`
                        : ""}
                    </p>
                  </div>
                  <RatingBadge value={star.rating} />
                </div>
              </Card>
            )}
            <div className="flex flex-col gap-2">
              {save.kind === "saved" && (
                <Link href={`/matches/${save.id}`} className={btn("primary")}>
                  Watch the replay
                </Link>
              )}
              <button
                type="button"
                className={btn(save.kind === "saved" ? "secondary" : "primary")}
                onClick={onPlayAgain}
              >
                Play again
              </button>
              <Link href="/home" className={btn("secondary")}>
                Back to Home
              </Link>
            </div>
            <SaveNote save={save} onRetry={onRetrySave} />
          </div>

          <Card title="Match stats" icon="chart">
            <div className="flex justify-between text-xs font-semibold">
              <span className="text-link">{h.name}</span>
              <span className="text-muted">{a.name}</span>
            </div>
            {stats ? (
              <StatsList stats={stats} />
            ) : (
              <p className="text-[13px] text-muted">No stats for this match.</p>
            )}
            <h3 className="mt-2 text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
              Key moments
            </h3>
            <ul className="flex flex-col gap-0.5">
              {moments.map((m, i) => (
                <CommentaryLine key={`${m.t}-${i}`} m={m} />
              ))}
            </ul>
          </Card>

          <MatchPanelRatings replay={replay} final={final} />
        </div>
      </main>
    </div>
  );
}

/** The ratings tab of the match panel on its own (full-time records). */
function MatchPanelRatings({ replay, final }: { replay: Replay; final: FinalHeader }) {
  return (
    <MatchPanel
      className="h-fit"
      replay={replay}
      t={Infinity}
      row={null}
      stats={null}
      records={[final?.home?.records, final?.away?.records]}
      mode="replay"
      initialTab="Ratings"
      tabs={["Ratings"]}
    />
  );
}
