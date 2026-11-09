"use client";

import { useMemo, useState } from "react";

import type { Replay } from "@/engine/replay/replay";

import {
  commentaryAt,
  elevenAt,
  energyAt,
  keyMoments,
  momentum,
  type PlayerRecord,
  ratingAt,
  teamOf,
  type TeamStats,
} from "./derive";
import { CommentaryLine, Momentum, PosBadge, RatingBadge, Segmented, StatCompare } from "./ui";

export type Tab = "Commentary" | "Key moments" | "Stats" | "Ratings";

export function StatsList({ stats }: { stats: TeamStats }) {
  return (
    <div className="flex flex-col gap-3.5">
      <StatCompare
        label="Possession"
        home={stats.possession[0]}
        away={stats.possession[1]}
        suffix="%"
      />
      <StatCompare label="Shots" home={stats.shots[0]} away={stats.shots[1]} />
      <StatCompare label="On target" home={stats.onTarget[0]} away={stats.onTarget[1]} />
      <StatCompare label="Passes" home={stats.passes[0]} away={stats.passes[1]} />
      <StatCompare
        label="Pass accuracy"
        home={stats.passAccuracy[0]}
        away={stats.passAccuracy[1]}
        suffix="%"
      />
      <StatCompare label="Tackles won" home={stats.tacklesWon[0]} away={stats.tacklesWon[1]} />
      {stats.corners && (
        <StatCompare label="Corners" home={stats.corners[0]} away={stats.corners[1]} />
      )}
      {stats.fouls && <StatCompare label="Fouls" home={stats.fouls[0]} away={stats.fouls[1]} />}
    </div>
  );
}

/** Live ratings from the stats row (with energy), or the full-time records of a finished match. */
function Ratings({
  replay,
  t,
  row,
  records,
}: {
  replay: Replay;
  t: number;
  row: number[] | null;
  records?: [PlayerRecord[] | undefined, PlayerRecord[] | undefined];
}) {
  const [side, setSide] = useState<0 | 1>(0);
  const h = teamOf(replay, 0);
  const a = teamOf(replay, 1);
  const recs = records?.[side];
  return (
    <div className="flex flex-col gap-3">
      <Segmented
        size="sm"
        label="Team"
        options={[h.name, a.name]}
        value={side === 0 ? h.name : a.name}
        onChange={(v) => setSide(v === h.name ? 0 : 1)}
      />
      <ul className="flex flex-col">
        {recs
          ? [...recs]
              .sort((x, y) => y.rating - x.rating)
              .map((r) => (
                <li
                  key={r.number}
                  className="flex items-center gap-2 border-b border-border/40 py-2 text-[13px] last:border-0"
                >
                  <span className="w-6 text-xs text-muted tabular-nums">{r.number}</span>
                  <span className="flex-1 truncate text-text">
                    {r.name}
                    {r.motm && (
                      <span className="ml-1 text-card" title="Player of the match">
                        ★
                      </span>
                    )}
                  </span>
                  {r.goals > 0 && (
                    <span className="text-xs text-goal">{"⚽".repeat(Math.min(3, r.goals))}</span>
                  )}
                  <RatingBadge value={r.rating} />
                </li>
              ))
          : elevenAt(replay, side, t).map((p) => {
              const e = energyAt(row, p.index);
              return (
                <li
                  key={p.index}
                  className="flex items-center gap-2 border-b border-border/40 py-2 text-[13px] last:border-0"
                >
                  <span className="w-6 text-xs text-muted tabular-nums">{p.number}</span>
                  <span className="min-w-0 flex-1 truncate text-text">{p.name}</span>
                  <PosBadge role={p.role} />
                  {e !== null && (
                    <span
                      className="h-1.5 w-12 overflow-hidden rounded-full bg-border"
                      title={`Energy ${e}%`}
                    >
                      <span
                        className={`block h-full rounded-full ${e >= 70 ? "bg-goal" : e >= 50 ? "bg-card" : "bg-foul"}`}
                        style={{ width: `${e}%` }}
                      />
                    </span>
                  )}
                  <RatingBadge value={ratingAt(row, p.index)} />
                </li>
              );
            })}
      </ul>
    </div>
  );
}

export function MatchPanel({
  replay,
  t,
  row,
  stats,
  records,
  mode,
  onSeek,
  className = "",
  tabs: only,
  initialTab,
}: {
  replay: Replay;
  t: number;
  row: number[] | null;
  stats: TeamStats | null;
  records?: [PlayerRecord[] | undefined, PlayerRecord[] | undefined];
  mode: "live" | "replay";
  onSeek?: (t: number) => void;
  className?: string;
  /** Only these tabs (e.g. the full-time report shows the ratings alone, without momentum). */
  tabs?: Tab[];
  initialTab?: Tab;
}) {
  const first: Tab = mode === "live" ? "Commentary" : "Key moments";
  const tabs: Tab[] = only ?? [first, "Stats", "Ratings"];
  const [tab, setTab] = useState<Tab>(initialTab ?? tabs[0] ?? first);
  const coarse = Math.floor(t * 2) / 2;
  const lines = useMemo(
    () =>
      mode === "live"
        ? commentaryAt(replay.events, coarse)
        : keyMoments(replay.events).slice().reverse(),
    // replay.events grows in place while the match is simulated; the coarse time re-reads it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [replay, coarse, mode, replay.events.length],
  );
  const mom = useMemo(() => momentum(replay.events, coarse), [replay, coarse]);

  return (
    <section
      className={`flex min-h-0 flex-col rounded-2xl border border-border bg-panel-2 ${className}`}
    >
      <div
        role="tablist"
        aria-label="Match details"
        className="flex gap-1 border-b border-border px-3 pt-2"
      >
        {tabs.map((x) => (
          <button
            key={x}
            type="button"
            role="tab"
            id={`mp-${x}`}
            aria-selected={tab === x}
            aria-controls="mp-panel"
            onClick={() => setTab(x)}
            className={`-mb-px border-b-2 px-3 py-2.5 text-[13px] font-semibold focus-visible:outline-2 focus-visible:outline-link ${tab === x ? "border-selected text-text" : "border-transparent text-muted hover:text-text"}`}
          >
            {x}
          </button>
        ))}
      </div>
      <div
        role="tabpanel"
        id="mp-panel"
        aria-labelledby={`mp-${tab}`}
        className="min-h-0 flex-1 overflow-y-auto p-3"
        aria-live={tab === "Commentary" ? "polite" : undefined}
      >
        {tab === "Commentary" || tab === "Key moments" ? (
          lines.length ? (
            <>
              {mode === "replay" && (
                <p className="px-2.5 pb-2 text-xs text-muted">Pick a moment to jump there.</p>
              )}
              <ul className="flex flex-col gap-0.5">
                {lines.map((m, i) => (
                  <CommentaryLine
                    key={`${m.t}-${i}`}
                    m={m}
                    onClick={onSeek ? () => onSeek(Math.max(0, m.t - 4)) : undefined}
                  />
                ))}
              </ul>
            </>
          ) : (
            <p className="p-2.5 text-[13px] text-muted">The commentary starts with the kick-off.</p>
          )
        ) : tab === "Stats" ? (
          stats ? (
            <StatsList stats={stats} />
          ) : (
            <p className="p-2.5 text-[13px] text-muted">Stats appear after the first minute.</p>
          )
        ) : (
          <Ratings replay={replay} t={t} row={row} records={records} />
        )}
      </div>
      {!only && (
        <div className="border-t border-border px-4 py-3">
          <div className="flex items-center justify-between text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
            <span>Momentum</span>
            <span className="font-medium tracking-normal normal-case">last minutes</span>
          </div>
          <Momentum values={mom} />
        </div>
      )}
    </section>
  );
}
