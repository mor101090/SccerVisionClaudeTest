"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { EngineClient } from "@/engine/engine-client";
import type { LiveReplay } from "@/engine/live-replay";
import type { TeamStrength, Weather } from "@/engine/protocol";
import { resetBall } from "@/engine/replay/drawPitch";
import { minuteText, type Replay } from "@/engine/replay/replay";
import type { SaveMatchInput } from "@/features/matches/match-schema";
import { encodeReplay } from "@/features/matches/replay-codec";
import { type ReplaySource, usePitchRenderer } from "@/features/matches/use-pitch-renderer";

import { statRows } from "./derive";

/**
 * One match on /play, from the pre-match screen to the full-time report: the engine worker, live pacing (the
 * engine stays 20 frames ahead of the viewer so in-match changes matter), the half-time pause and the save.
 */

export type Phase =
  "loading" | "idle" | "preparing" | "prematch" | "live" | "halftime" | "finished" | "error";
export const SPEEDS = [1, 2, 4, 8, 16] as const;
export type Speed = (typeof SPEEDS)[number];
export type SaveState =
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "saved"; id: string }
  | { kind: "signin" }
  | { kind: "error"; message: string };

export type MatchSetup = {
  /** The manager's club (UseHomeTeam text); none: a generated home side. */
  homeTeam?: string;
  plan?: string | null;
  awayStrength: TeamStrength;
  /** Simulate to the end at once and go straight to the report. */
  instant: boolean;
};

export type Tick = { t: number; clock: string; home: number; away: number; duration: number };

/** Live pacing: the engine may run this many frames ahead of what is shown (classic page: 20). */
const LEAD = 20;
const WEATHERS: Weather[] = ["Sunny", "Sunny", "Cloudy", "Cloudy", "Rain", "Snow"];

/** Weather, pitch and wind drawn from the seed: the same match always gets the same conditions. */
export function conditionsFor(seed: number) {
  const r = (k: number) => ((seed * (k * 2654435761)) >>> 0) / 2 ** 32;
  return {
    weather: WEATHERS[Math.floor(r(1) * WEATHERS.length)] ?? "Sunny",
    maintenance: Math.round((0.6 + r(2) * 0.4) * 100) / 100,
    wind: Math.round(r(3) * 8 * 10) / 10,
    windAngle: (seed * 97) % 360,
    night: r(4) < 0.25,
  };
}

export function useMatch() {
  const client = useRef<EngineClient | null>(null);
  const live = useRef<LiveReplay | null>(null);
  const source = useRef<ReplaySource | null>(null);
  const time = useRef(0);
  const speedRef = useRef(0);
  const canvas = useRef<HTMLCanvasElement>(null);
  const wrap = useRef<HTMLDivElement>(null);
  const meta = useRef<Omit<
    SaveMatchInput,
    "homeName" | "awayName" | "homeScore" | "awayScore"
  > | null>(null);
  const halftimeShown = useRef(false);
  const instant = useRef(false);
  const rowsCache = useRef<{ n: number; rows: number[][] }>({ n: 0, rows: [] });

  const [phase, setPhase] = useState<Phase>("loading");
  const [error, setError] = useState<string | null>(null);
  const [speed, setSpeed] = useState<Speed>(4);
  const [paused, setPaused] = useState(false);
  const [tick, setTick] = useState<Tick>({ t: 0, clock: "0'", home: 0, away: 0, duration: 0 });
  const [replay, setReplay] = useState<Replay | null>(null);
  const [save, setSave] = useState<SaveState>({ kind: "idle" });

  useEffect(() => {
    const c = new EngineClient();
    client.current = c;
    c.whenReady()
      .then(() => setPhase("idle"))
      .catch((e: unknown) => {
        setPhase("error");
        setError(e instanceof Error ? e.message : String(e));
      });
    return () => c.dispose();
  }, []);

  const running = phase === "live" && !paused;
  useEffect(() => {
    speedRef.current = running ? speed : 0;
  }, [running, speed]);

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

  const finished = useRef(false);
  const finish = useCallback(() => {
    if (finished.current) return;
    finished.current = true;
    setPhase("finished");
    void saveMatch();
  }, [saveMatch]);

  usePitchRenderer({
    canvas,
    wrap,
    source,
    speed: speedRef,
    time,
    onTick: ({ time: t, snap, replay: r, ended }) => {
      const secondHalf = r.events.some((e) => e.type === "SecondHalfStart" && e.t <= t);
      setTick({
        t,
        clock: minuteText(snap.clock, secondHalf),
        home: snap.homeScore,
        away: snap.awayScore,
        duration: source.current?.duration ?? 0,
      });
      if (!instant.current) client.current?.progress(Math.floor(t * (r.fps || 10)));
      if (!halftimeShown.current && r.events.some((e) => e.type === "HalfTime" && e.t <= t)) {
        halftimeShown.current = true;
        setPhase((p) => (p === "live" ? "halftime" : p));
      }
      if (ended && live.current?.finished) finish();
    },
  });

  /** Starts the engine for a match. Watch live: hold at kick-off (pre-match screen). Instant: run to the end. */
  const prepare = useCallback(
    async (setup: MatchSetup) => {
      const c = client.current;
      if (!c) return;
      const seed = Math.floor(Math.random() * 2147483646) + 1;
      const conditions = conditionsFor(seed);
      // "live": the home side is the manager's (their club, or a generated one) and the computer answers.
      meta.current = {
        mode: "live",
        seed,
        engineVersion: c.version ?? "unknown",
        conditions,
        ...(setup.homeTeam ? { homeSquad: setup.homeTeam } : {}),
        ...(setup.homeTeam && setup.plan ? { homePlan: setup.plan } : {}),
      };
      instant.current = setup.instant;
      finished.current = false;
      halftimeShown.current = setup.instant;
      rowsCache.current = { n: 0, rows: [] };
      time.current = 0;
      live.current = null;
      source.current = null;
      setReplay(null);
      setSave({ kind: "idle" });
      setPaused(false);
      setError(null);
      resetBall();
      setPhase("preparing");
      await c.start(
        {
          seed,
          mode: "live",
          ...conditions,
          lead: setup.instant ? 0 : LEAD,
          hold: !setup.instant,
          awayStrength: setup.awayStrength,
          ...(setup.homeTeam ? { homeTeam: setup.homeTeam } : {}),
          ...(setup.homeTeam && setup.plan ? { plan: setup.plan } : {}),
        },
        {
          onHeader: (l) => {
            live.current = l;
            source.current = { replay: l.replay, duration: 0 };
            setReplay(l.replay);
            if (!setup.instant) setPhase("prematch");
          },
          onChunk: (l) => {
            source.current = { replay: l.replay, duration: l.duration };
          },
          onFinished: (l) => {
            if (setup.instant) {
              time.current = l.duration;
              source.current = { replay: l.replay, duration: l.duration };
              setTick({
                t: l.duration,
                clock: "90'",
                home: l.score.home,
                away: l.score.away,
                duration: l.duration,
              });
              finish();
            }
          },
          onError: (message) => {
            setPhase("error");
            setError(message);
          },
        },
      );
    },
    [finish],
  );

  const kickOff = useCallback(() => {
    client.current?.go();
    setPhase("live");
  }, []);

  const resumeSecondHalf = useCallback(() => setPhase("live"), []);

  const setPlan = useCallback((text: string) => client.current?.setPlan(0, text), []);
  const substitute = useCallback(
    (outNumber: number, inNumber: number) => client.current?.substitute(0, outNumber, inNumber),
    [],
  );

  /** Seek within what has been played (replay of the live match is not possible: the engine is ahead). */
  const reset = useCallback(() => {
    client.current?.stop();
    live.current = null;
    source.current = null;
    setReplay(null);
    setPhase("idle");
  }, []);

  const rows = useCallback(() => {
    const l = live.current;
    if (!l) return [];
    if (rowsCache.current.n !== l.stats.length)
      rowsCache.current = { n: l.stats.length, rows: statRows(l.stats) };
    return rowsCache.current.rows;
  }, []);

  return {
    phase,
    error,
    speed,
    setSpeed,
    paused,
    setPaused,
    tick,
    replay,
    save,
    saveMatch,
    canvas,
    wrap,
    live,
    rows,
    prepare,
    kickOff,
    resumeSecondHalf,
    setPlan,
    substitute,
    reset,
  };
}

export type MatchSession = ReturnType<typeof useMatch>;
