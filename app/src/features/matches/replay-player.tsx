"use client";

import { useEffect, useRef, useState } from "react";

import { resetBall } from "@/engine/replay/drawPitch";
import { frameTime, minuteText, type ReplayEvent } from "@/engine/replay/replay";

import {
  EventFeed,
  type HudState,
  isSecondHalf,
  recentEvents,
  Scoreboard,
  type Speed,
  SpeedPicker,
} from "./match-hud";
import { decodeReplay } from "./replay-codec";
import { type ReplaySource, usePitchRenderer } from "./use-pitch-renderer";

/** Plays a saved match from the replays bucket (via /api/matches/:id/replay). */
export function ReplayPlayer({ matchId }: { matchId: string }) {
  const source = useRef<ReplaySource | null>(null);
  const time = useRef(0);
  const speedRef = useRef<number>(16);
  const canvas = useRef<HTMLCanvasElement>(null);
  const wrap = useRef<HTMLDivElement>(null);

  const [speed, setSpeed] = useState<Speed>(16);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [hud, setHud] = useState<HudState>({
    clock: "0'",
    home: 0,
    away: 0,
    homeName: "",
    awayName: "",
  });
  const [events, setEvents] = useState<ReplayEvent[]>([]);

  useEffect(() => {
    speedRef.current = speed;
  }, [speed]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const res = await fetch(`/api/matches/${matchId}/replay`);
      if (!res.ok) throw new Error(`Replay not available (${res.status})`);
      const replay = await decodeReplay(new Uint8Array(await res.arrayBuffer()));
      if (cancelled) return;
      resetBall();
      time.current = 0;
      const last = replay.frames[replay.frames.length - 1];
      source.current = { replay, duration: last ? frameTime(last) : 0 };
      setHud((h) => ({ ...h, homeName: replay.home.name, awayName: replay.away.name }));
      setLoaded(true);
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
    onTick: ({ time: t, snap, replay }) => {
      setHud((h) => ({
        ...h,
        clock: minuteText(snap.clock, isSecondHalf(replay.events, t)),
        home: snap.homeScore,
        away: snap.awayScore,
      }));
      setEvents(recentEvents(replay.events, t));
    },
  });

  return (
    <section className="flex w-full max-w-5xl flex-col gap-4">
      <div className="flex items-center gap-2">
        <button
          type="button"
          className="rounded bg-zinc-700 px-4 py-2 text-white disabled:opacity-50"
          disabled={!loaded}
          onClick={() => {
            time.current = 0;
            resetBall();
          }}
        >
          Restart
        </button>
        <div className="ml-auto">
          <SpeedPicker speed={speed} onChange={setSpeed} />
        </div>
      </div>
      <Scoreboard hud={hud} />
      <div ref={wrap} className="w-full overflow-hidden rounded bg-green-900">
        <canvas ref={canvas} className="block w-full" aria-label="Replay pitch" />
      </div>
      {error && <p className="text-red-400">{error}</p>}
      <EventFeed events={events} />
    </section>
  );
}
