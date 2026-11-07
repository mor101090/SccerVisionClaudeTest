"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { resetBall } from "@/engine/replay/drawPitch";
import { frameTime, minuteText, type Replay } from "@/engine/replay/replay";
import { decodeReplay } from "@/features/matches/replay-codec";
import { type ReplaySource, usePitchRenderer } from "@/features/matches/use-pitch-renderer";

import { keyMoments, statsFromFinal } from "./derive";
import type { FinalHeader } from "./full-time";
import { PitchFrame, useScrollLock } from "./live-screen";
import { MatchPanel } from "./match-panel";
import { weatherText } from "./pre-match";
import { ScoreBug, SpeedControl } from "./score-bar";
import { Timeline } from "./ui";
import type { Speed } from "./use-match";

const clockText = (seconds: number) =>
  `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, "0")}`;

/**
 * Figma "Replay": a saved match from the replays bucket, with a scrubber, jumps between key moments (← →) and the
 * full-time stats and ratings saved with it.
 */
export function ReplayView({
  matchId,
  summary,
  label,
}: {
  matchId: string;
  summary: FinalHeader;
  /** "Replay · seed 7274 · Sat 14 Nov". */
  label: string;
}) {
  useScrollLock();
  const source = useRef<ReplaySource | null>(null);
  const time = useRef(0);
  const speedRef = useRef(0);
  const canvas = useRef<HTMLCanvasElement>(null);
  const wrap = useRef<HTMLDivElement>(null);

  const [replay, setReplay] = useState<Replay | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [speed, setSpeed] = useState<Speed>(4);
  const [paused, setPaused] = useState(false);
  const [tick, setTick] = useState({ t: 0, clock: "0'", home: 0, away: 0 });
  const duration = useRef(0);
  const [total, setTotal] = useState(0);

  useEffect(() => {
    speedRef.current = paused ? 0 : speed;
  }, [paused, speed]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const res = await fetch(`/api/matches/${matchId}/replay`);
      if (!res.ok) throw new Error(`Replay not available (${res.status})`);
      const r = await decodeReplay(new Uint8Array(await res.arrayBuffer()));
      if (cancelled) return;
      resetBall();
      time.current = 0;
      const last = r.frames[r.frames.length - 1];
      duration.current = last ? frameTime(last) : 0;
      source.current = { replay: r, duration: duration.current };
      setTotal(duration.current);
      setReplay(r);
    })().catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)));
    return () => {
      cancelled = true;
    };
  }, [matchId]);

  usePitchRenderer({
    canvas,
    wrap,
    source,
    speed: speedRef,
    time,
    onTick: ({ time: t, snap, replay: r }) => {
      const secondHalf = r.events.some((e) => e.type === "SecondHalfStart" && e.t <= t);
      setTick({
        t,
        clock: minuteText(snap.clock, secondHalf),
        home: snap.homeScore,
        away: snap.awayScore,
      });
    },
  });

  const seek = useCallback((t: number) => {
    time.current = Math.max(0, Math.min(duration.current, t));
    resetBall();
    setTick((x) => ({ ...x, t: time.current }));
  }, []);

  const moments = useMemo(() => (replay ? keyMoments(replay.events) : []), [replay]);
  const step = useCallback(
    (dir: 1 | -1) => {
      const now = time.current;
      const target =
        dir > 0
          ? moments.find((m) => m.t - 4 > now + 0.5)
          : [...moments].reverse().find((m) => m.t - 4 < now - 0.5);
      seek(target ? target.t - 4 : dir > 0 ? duration.current : 0);
    },
    [moments, seek],
  );

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement)?.closest("input, select, textarea")) return;
      if (e.key === "ArrowRight") step(1);
      else if (e.key === "ArrowLeft") step(-1);
      else if (e.key === " ") {
        e.preventDefault();
        setPaused((p) => !p);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [step]);

  const stats =
    summary?.home?.stats && summary.away?.stats
      ? statsFromFinal(summary.home.stats, summary.away.stats)
      : null;
  const htEvent = replay?.events.find((e) => e.type === "HalfTime");

  return (
    <div className="fixed inset-0 z-40 flex flex-col bg-bg text-text">
      <header className="grid grid-cols-[auto_1fr_auto] items-center gap-2 border-b border-border bg-panel px-3 py-2 sm:px-5">
        <Link
          href="/matches"
          className="flex items-center gap-1 rounded-md px-2 py-1.5 text-[13px] text-muted hover:text-text focus-visible:outline-2 focus-visible:outline-link"
        >
          <AppIcon name="chevronLeft" size={16} />
          <span className="hidden sm:inline">My Games</span>
        </Link>
        <div className="flex justify-center">
          {replay && (
            <ScoreBug
              replay={replay}
              t={tick.t}
              clock={tick.clock}
              home={tick.home}
              away={tick.away}
              state="replay"
            />
          )}
        </div>
        <span />
      </header>

      <main className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto p-3 sm:p-4 lg:flex-row lg:overflow-hidden">
        <div className="flex min-w-0 flex-1 flex-col gap-4">
          <PitchFrame wrap={wrap} canvas={canvas} label="Replay pitch">
            {replay?.conditions && (
              <span className="absolute top-3 left-3 flex items-center gap-1.5 rounded-md bg-bg/70 px-2 py-1 text-[11px] font-medium">
                <AppIcon name="cloud" size={14} />
                {weatherText(replay)}
              </span>
            )}
            <span className="absolute top-3 right-3 flex items-center gap-1.5 rounded-md bg-selected px-2 py-1 text-[11px] font-semibold text-white">
              <AppIcon name="refresh" size={14} />
              {label}
            </span>
            {!replay && (
              <p className="absolute inset-0 flex items-center justify-center p-4 text-center text-sm font-medium text-white/85">
                {error ?? "Loading the replay…"}
              </p>
            )}
          </PitchFrame>
        </div>
        {replay && (
          <MatchPanel
            className="h-[460px] shrink-0 lg:h-auto lg:w-[380px]"
            replay={replay}
            t={tick.t}
            row={null}
            stats={stats}
            records={[summary?.home?.records, summary?.away?.records]}
            mode="replay"
            onSeek={seek}
          />
        )}
      </main>

      <footer className="flex flex-wrap items-center gap-3 border-t border-border bg-panel px-3 py-3 sm:px-5">
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => step(-1)}
            aria-label="Previous moment"
            className="flex size-9 items-center justify-center rounded-full bg-panel-2 text-text hover:bg-border focus-visible:outline-2 focus-visible:outline-link"
          >
            <AppIcon name="chevronLeft" size={18} />
          </button>
          <button
            type="button"
            onClick={() => setPaused((p) => !p)}
            aria-label={paused ? "Play" : "Pause"}
            className="flex size-11 items-center justify-center rounded-full bg-accent text-on-accent hover:brightness-110 focus-visible:outline-2 focus-visible:outline-link"
          >
            <AppIcon name={paused ? "play" : "pause"} size={18} />
          </button>
          <button
            type="button"
            onClick={() => step(1)}
            aria-label="Next moment"
            className="flex size-9 items-center justify-center rounded-full bg-panel-2 text-text hover:bg-border focus-visible:outline-2 focus-visible:outline-link"
          >
            <AppIcon name="chevronRight" size={18} />
          </button>
        </div>
        <span className="w-12 text-[13px] font-semibold tabular-nums">{clockText(tick.t)}</span>
        <div className="min-w-48 flex-1">
          <Timeline
            compact
            moments={moments}
            t={tick.t}
            duration={total}
            halftime={htEvent?.t ?? null}
            onSeek={seek}
          />
        </div>
        <span className="w-12 text-[13px] text-muted tabular-nums">{clockText(total)}</span>
        <SpeedControl
          speed={speed}
          onSpeed={setSpeed}
          paused={paused}
          onPause={() => setPaused((p) => !p)}
        />
        <span className="sr-only" aria-live="polite">
          {moments.find((m) => Math.abs(m.t - tick.t) < 1)?.text ?? ""}
        </span>
      </footer>
    </div>
  );
}
