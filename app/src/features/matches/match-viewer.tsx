"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { EngineClient } from "@/engine/engine-client";
import type { LiveReplay } from "@/engine/live-replay";
import type { MatchMode, Weather } from "@/engine/protocol";
import { resetBall } from "@/engine/replay/drawPitch";
import { minuteText, type ReplayEvent } from "@/engine/replay/replay";

import {
  EventFeed,
  type HudState,
  isSecondHalf,
  recentEvents,
  Scoreboard,
  type Speed,
  SpeedPicker,
} from "./match-hud";
import type { SaveMatchInput } from "./match-schema";
import { encodeReplay } from "./replay-codec";
import { type ReplaySource, usePitchRenderer } from "./use-pitch-renderer";

type Status = "loading" | "ready" | "simulating" | "playing" | "finished" | "error";
type SaveState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; id: string }
  | { kind: "signin" }
  | { kind: "error"; message: string };

const STAGED_SEED = 7274;

/** `myTeam`: the manager's own club (engine squad text); "Play with …" fields it at home against the computer. */
export function MatchViewer({ myTeam }: { myTeam?: { name: string; text: string } | null } = {}) {
  const client = useRef<EngineClient | null>(null);
  const live = useRef<LiveReplay | null>(null);
  const source = useRef<ReplaySource | null>(null);
  const time = useRef(0);
  const speedRef = useRef<number>(16);
  const canvas = useRef<HTMLCanvasElement>(null);
  const wrap = useRef<HTMLDivElement>(null);
  const meta = useRef<Omit<
    SaveMatchInput,
    "homeName" | "awayName" | "homeScore" | "awayScore"
  > | null>(null);

  const [status, setStatus] = useState<Status>("loading");
  const [error, setError] = useState<string | null>(null);
  const [speed, setSpeed] = useState<Speed>(16);
  const [hud, setHud] = useState<HudState>({
    clock: "0'",
    home: 0,
    away: 0,
    homeName: "",
    awayName: "",
  });
  const [events, setEvents] = useState<ReplayEvent[]>([]);
  const [simDone, setSimDone] = useState(false);
  const [save, setSave] = useState<SaveState>({ kind: "idle" });

  useEffect(() => {
    const c = new EngineClient();
    client.current = c;
    c.whenReady()
      .then(() => setStatus("ready"))
      .catch((e: unknown) => {
        setStatus("error");
        setError(e instanceof Error ? e.message : String(e));
      });
    return () => c.dispose();
  }, []);

  useEffect(() => {
    speedRef.current = speed;
  }, [speed]);

  usePitchRenderer({
    canvas,
    wrap,
    source,
    speed: speedRef,
    time,
    onTick: ({ time: t, snap, replay, ended }) => {
      setHud((h) => ({
        ...h,
        clock: minuteText(snap.clock, isSecondHalf(replay.events, t)),
        home: snap.homeScore,
        away: snap.awayScore,
      }));
      setEvents(recentEvents(replay.events, t));
      if (ended && live.current?.finished) setStatus("finished");
    },
  });

  const start = useCallback(async (mode: MatchMode, homeTeam?: string) => {
    const c = client.current;
    if (!c) return;
    const seed = mode === "staged" ? STAGED_SEED : Math.floor(Math.random() * 2147483646) + 1;
    const conditions = {
      weather: "Sunny" as Weather,
      maintenance: 1,
      wind: 0,
      windAngle: (seed * 97) % 360,
      night: false,
    };
    meta.current = {
      mode,
      seed,
      engineVersion: c.version ?? "unknown",
      conditions,
      ...(homeTeam ? { homeSquad: homeTeam } : {}),
    };
    setEvents([]);
    setError(null);
    setSimDone(false);
    setSave({ kind: "idle" });
    time.current = 0;
    live.current = null;
    source.current = null;
    resetBall();
    setStatus("simulating");
    await c.start(
      { seed, mode, ...conditions, lead: 0, ...(homeTeam ? { homeTeam } : {}) },
      {
        onHeader: (l) => {
          live.current = l;
          source.current = { replay: l.replay, duration: 0 };
          setHud((h) => ({ ...h, homeName: l.replay.home.name, awayName: l.replay.away.name }));
        },
        onChunk: (l) => {
          source.current = { replay: l.replay, duration: l.duration };
          if (l.frameCount > 2) setStatus((st) => (st === "simulating" ? "playing" : st));
        },
        onFinished: () => setSimDone(true),
        onError: (message) => {
          setStatus("error");
          setError(message);
        },
      },
    );
  }, []);

  const saveMatch = useCallback(async () => {
    const l = live.current;
    const m = meta.current;
    if (!l || !m || !l.finished) return;
    setSave({ kind: "saving" });
    try {
      const input: SaveMatchInput = {
        ...m,
        homeName: l.replay.home.name,
        awayName: l.replay.away.name,
        homeScore: l.score.home,
        awayScore: l.score.away,
        summary: (l.finalHeader as Record<string, unknown> | null) ?? undefined,
      };
      const form = new FormData();
      form.set("meta", JSON.stringify(input));
      form.set(
        "replay",
        new Blob([(await encodeReplay(l.replay)) as BlobPart], { type: "application/gzip" }),
      );
      const res = await fetch("/api/matches", { method: "POST", body: form });
      if (res.status === 401) return setSave({ kind: "signin" });
      const body = (await res.json()) as { id?: string; error?: string };
      if (!res.ok || !body.id)
        return setSave({ kind: "error", message: body.error ?? res.statusText });
      setSave({ kind: "saved", id: body.id });
    } catch (e) {
      setSave({ kind: "error", message: e instanceof Error ? e.message : String(e) });
    }
  }, []);

  const busy = status === "loading" || status === "simulating";

  return (
    <section className="flex w-full max-w-5xl flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {myTeam && (
          <button
            type="button"
            className="rounded-lg bg-button px-4 py-2 font-semibold text-button-text enabled:hover:bg-border disabled:opacity-45"
            disabled={busy}
            onClick={() => void start("live", myTeam.text)}
          >
            Play with {myTeam.name}
          </button>
        )}
        <button
          type="button"
          className="rounded bg-emerald-600 px-4 py-2 font-medium text-white disabled:opacity-50"
          disabled={busy}
          onClick={() => void start("random")}
        >
          {status === "loading" ? "Loading engine…" : "Random match"}
        </button>
        <button
          type="button"
          className="rounded bg-zinc-700 px-4 py-2 font-medium text-white disabled:opacity-50"
          disabled={busy}
          onClick={() => void start("staged")}
        >
          Staged match
        </button>
        {simDone && (
          <button
            type="button"
            className="rounded bg-sky-700 px-4 py-2 font-medium text-white disabled:opacity-50"
            disabled={save.kind === "saving" || save.kind === "saved"}
            onClick={() => void saveMatch()}
          >
            {save.kind === "saving" ? "Saving…" : save.kind === "saved" ? "Saved" : "Save match"}
          </button>
        )}
        <div className="ml-auto">
          <SpeedPicker speed={speed} onChange={setSpeed} />
        </div>
      </div>

      <Scoreboard hud={hud} />

      <div ref={wrap} className="w-full overflow-hidden rounded bg-green-900">
        <canvas ref={canvas} className="block w-full" aria-label="Match pitch" />
      </div>

      <div className="text-sm text-zinc-400" aria-live="polite">
        {status === "error" && <p className="text-red-400">Engine error: {error}</p>}
        {status === "simulating" && <p>Simulating…</p>}
        {status === "finished" && <p data-testid="full-time">Full time.</p>}
        {save.kind === "saved" && (
          <p data-testid="saved">
            Saved.{" "}
            <Link className="text-emerald-400 underline" href={`/matches/${save.id}`}>
              Watch the replay
            </Link>
          </p>
        )}
        {save.kind === "signin" && (
          <p>
            <Link className="text-emerald-400 underline" href="/auth/sign-in?redirectTo=/play">
              Sign in
            </Link>{" "}
            to save matches.
          </p>
        )}
        {save.kind === "error" && <p className="text-red-400">Could not save: {save.message}</p>}
        <EventFeed events={events} />
      </div>
    </section>
  );
}
