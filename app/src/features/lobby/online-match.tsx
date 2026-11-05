"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { EngineClient } from "@/engine/engine-client";
import type { LiveReplay } from "@/engine/live-replay";
import type { InputAction, Weather } from "@/engine/protocol";
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
} from "@/features/matches/match-hud";
import { type ReplaySource, usePitchRenderer } from "@/features/matches/use-pitch-renderer";

import { framesChecksum } from "./checksum";
import type { InputActionBody, LobbyState, ProgressBody } from "./lobby-schema";
import { type SyncMode, syncLobby } from "./lobby-sync";

/** How far (frames, 10/s) the engine may run ahead of the viewer. */
const LEAD = 100;
/**
 * The host moves the watermark in steps of this many frames (3 s of match), or sooner when it has
 * applied an input or reached full time. At most ~400 writes per match; the guest makes none.
 */
const WATERMARK_STEP = 30;
/** How often the host checks whether it has something to report. */
const REPORT_CHECK_MS = 500;

type Applied = ProgressBody["applied"][number];

function toAction(team: 0 | 1, a: InputActionBody): InputAction {
  return a.kind === "plan"
    ? { kind: "plan", team, plan: a.plan }
    : { kind: "sub", team, out: a.out, in: a.in };
}

/**
 * Online match, host-authoritative lockstep:
 * - both browsers run the same deterministic engine from the same seed;
 * - the host's engine applies every manager input and reports its frame + order, then its watermark;
 * - the guest's engine never simulates past the watermark and applies inputs at the same frames.
 * Only the inputs and a frame number travel through the server; no match data does.
 */
export function OnlineMatch({ lobbyId }: { lobbyId: string }) {
  const [state, setState] = useState<LobbyState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [speed, setSpeed] = useState<Speed>(1);
  const [hud, setHud] = useState<HudState>({
    clock: "0'",
    home: 0,
    away: 0,
    homeName: "",
    awayName: "",
  });
  const [events, setEvents] = useState<ReplayEvent[]>([]);
  const [result, setResult] = useState<{ home: number; away: number; checksum: string } | null>(
    null,
  );
  const [subOut, setSubOut] = useState("");
  const [subIn, setSubIn] = useState("");
  const [note, setNote] = useState<string | null>(null);
  const [simFrames, setSimFrames] = useState(0);
  const [syncMode, setSyncMode] = useState<SyncMode>("connecting");

  const client = useRef<EngineClient | null>(null);
  const live = useRef<LiveReplay | null>(null);
  const source = useRef<ReplaySource | null>(null);
  const time = useRef(0);
  const speedRef = useRef<number>(1);
  const canvas = useRef<HTMLCanvasElement>(null);
  const wrap = useRef<HTMLDivElement>(null);

  const started = useRef<Promise<void> | null>(null);
  const scheduled = useRef(new Set<string>());
  /** Host: what each scheduled input was (for the progress report). */
  const actions = useRef(
    new Map<string, { team: 0 | 1; action: InputActionBody; guest: boolean }>(),
  );
  /** Host: applied since the last progress report. */
  const appliedQueue = useRef<Applied[]>([]);
  const finishReported = useRef(false);
  /** Known after the first state. Only the host reports progress. */
  const role = useRef<LobbyState["role"] | null>(null);

  useEffect(() => {
    speedRef.current = speed;
  }, [speed]);

  useEffect(() => {
    const c = new EngineClient();
    client.current = c;
    c.whenReady().catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)));
    return () => c.dispose();
  }, []);

  usePitchRenderer({
    canvas,
    wrap,
    source,
    speed: speedRef,
    time,
    onTick: ({ time: t, snap, replay }) => {
      // Live pacing: tell the engine how far the viewer has got.
      client.current?.progress(Math.floor(t * (replay.fps || 10)));
      setHud((h) => ({
        ...h,
        clock: minuteText(snap.clock, isSecondHalf(replay.events, t)),
        home: snap.homeScore,
        away: snap.awayScore,
      }));
      setEvents(recentEvents(replay.events, t));
      setSimFrames(live.current?.frameCount ?? 0);
    },
  });

  const startEngine = useCallback((s: LobbyState) => {
    const c = client.current;
    if (!c) return Promise.resolve();
    const isGuest = s.role === "guest";
    resetBall();
    time.current = 0;
    return c
      .start(
        {
          seed: s.seed,
          mode: "online",
          weather: s.conditions.weather as Weather,
          maintenance: s.conditions.maintenance,
          wind: s.conditions.wind,
          windAngle: s.conditions.windAngle,
          night: s.conditions.night,
          lead: LEAD,
          // The guest waits for the host's watermark before simulating anything.
          hold: isGuest,
        },
        {
          onHeader: (l) => {
            live.current = l;
            source.current = { replay: l.replay, duration: 0 };
            setHud((h) => ({ ...h, homeName: l.replay.home.name, awayName: l.replay.away.name }));
          },
          onChunk: (l) => {
            source.current = { replay: l.replay, duration: l.duration };
          },
          onFinished: (l) => {
            setResult({ ...l.score, checksum: framesChecksum(l.replay.frames) });
          },
          onApplied: (id, frame, order) => {
            if (isGuest) return;
            const a = actions.current.get(id);
            if (!a) return;
            appliedQueue.current.push({
              ...(a.guest ? { id } : {}),
              team: a.team,
              action: a.action,
              frame,
              seq: order,
            });
          },
          onError: (message) => setError(message),
        },
      )
      .then(() => {
        if (isGuest) {
          c.limit(0);
          c.go();
        }
      });
  }, []);

  /** Applies one lobby state (from Realtime or polling), strictly in arrival order. */
  const applyState = useCallback(
    async (s: LobbyState) => {
      setState(s);
      role.current = s.role;
      const c = client.current;
      if (!c || (s.status !== "playing" && s.status !== "finished")) return;

      started.current ??= startEngine(s);
      await started.current;

      if (s.role === "guest") {
        // Inputs first (ordered by frame, seq), then raise the limit to the host's watermark.
        for (const i of s.inputs) {
          if (i.frame === null || scheduled.current.has(i.id)) continue;
          scheduled.current.add(i.id);
          c.schedule({ id: i.id, frame: i.frame, ...toAction(i.team, i.action) });
        }
        c.limit(s.finalFrame ?? s.hostFrame);
        return;
      }

      // Host: apply the guest's new requests at our next step (reported by the writer below).
      for (const i of s.inputs) {
        if (i.frame !== null || scheduled.current.has(i.id)) continue;
        scheduled.current.add(i.id);
        actions.current.set(i.id, { team: i.team, action: i.action, guest: true });
        c.schedule({ id: i.id, frame: null, ...toAction(i.team, i.action) });
      }
    },
    [startEngine],
  );

  // Reads: Realtime push (or polling fallback). States are applied one after another.
  useEffect(() => {
    let queue = Promise.resolve();
    return syncLobby(
      lobbyId,
      (s) => {
        queue = queue.then(() => applyState(s)).catch((e: unknown) => setError(String(e)));
      },
      setSyncMode,
      setError,
    );
  }, [lobbyId, applyState]);

  // Writes (host only): report applied inputs and the watermark, only when there is news.
  useEffect(() => {
    let busy = false;
    let lastFrame = 0;
    const timer = setInterval(() => {
      const l = live.current;
      if (busy || role.current !== "host" || !l || finishReported.current) return;
      const due =
        appliedQueue.current.length > 0 ||
        l.frameCount - lastFrame >= WATERMARK_STEP ||
        (l.finished && l.frameCount > 0);
      if (!due) return;
      const body: ProgressBody = {
        frame: l.frameCount,
        finished: l.finished,
        applied: appliedQueue.current.splice(0),
      };
      busy = true;
      fetch(`/api/lobbies/${lobbyId}/progress`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      })
        .then(async (res) => {
          if (!res.ok)
            throw new Error(((await res.json()) as { error?: string }).error ?? res.statusText);
          lastFrame = body.frame;
          if (body.finished) finishReported.current = true;
        })
        .catch((e: unknown) => {
          appliedQueue.current.unshift(...body.applied); // retry on the next check
          setError(e instanceof Error ? e.message : String(e));
        })
        .finally(() => {
          busy = false;
        });
    }, REPORT_CHECK_MS);
    return () => clearInterval(timer);
  }, [lobbyId]);

  async function startGame() {
    const res = await fetch(`/api/lobbies/${lobbyId}/start`, { method: "POST" });
    if (!res.ok) setError(((await res.json()) as { error?: string }).error ?? "Could not start");
  }

  async function requestSub() {
    const out = Number(subOut);
    const inn = Number(subIn);
    if (!state || !out || !inn) return;
    const action: InputActionBody = { kind: "sub", out, in: inn };
    if (state.role === "host") {
      const id = crypto.randomUUID();
      scheduled.current.add(id);
      actions.current.set(id, { team: 0, action, guest: false });
      client.current?.schedule({ id, frame: null, ...toAction(0, action) });
      setNote(`Change requested: #${out} off, #${inn} on.`);
    } else {
      const res = await fetch(`/api/lobbies/${lobbyId}/inputs`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(action),
      });
      setNote(res.ok ? `Change requested: #${out} off, #${inn} on.` : "Could not send the change.");
    }
    setSubOut("");
    setSubIn("");
  }

  if (!state) {
    return error ? (
      <p className="text-red-400">
        {error}.{" "}
        <Link href="/lobby" className="underline">
          Back
        </Link>
      </p>
    ) : (
      <p className="text-zinc-400">Connecting…</p>
    );
  }

  const myTeam = state.role === "host" ? "home" : "away";

  return (
    <section className="flex w-full max-w-5xl flex-col gap-4" data-testid={`online-${state.role}`}>
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <span>
          Game code{" "}
          <strong className="font-mono text-lg tracking-widest" data-testid="lobby-code">
            {state.code}
          </strong>
        </span>
        <span className="text-zinc-400">
          {state.hostName} (home) v {state.guestName ?? "waiting for a friend…"} (away)
        </span>
        <span className="text-zinc-500" data-testid="lobby-status">
          {state.status}
        </span>
        {state.role === "host" && state.status === "ready" && (
          <button
            type="button"
            onClick={() => void startGame()}
            className="rounded bg-emerald-600 px-4 py-2 font-medium text-white"
          >
            Kick off
          </button>
        )}
        {state.role === "guest" && state.status === "ready" && (
          <span className="text-zinc-400">Waiting for the host to kick off…</span>
        )}
        <div className="ml-auto">
          <SpeedPicker speed={speed} onChange={setSpeed} />
        </div>
      </div>

      <Scoreboard hud={hud} />
      <div ref={wrap} className="w-full overflow-hidden rounded bg-green-900">
        <canvas ref={canvas} className="block w-full" aria-label="Match pitch" />
      </div>

      {(state.status === "playing" || state.status === "finished") && !result && (
        <form
          className="flex flex-wrap items-end gap-2 text-sm"
          onSubmit={(e) => {
            e.preventDefault();
            void requestSub();
          }}
        >
          <span className="text-zinc-400">Change ({myTeam} team):</span>
          <label className="flex flex-col">
            Off #
            <input
              className="w-16 rounded border border-zinc-700 bg-zinc-900 px-2 py-1"
              inputMode="numeric"
              value={subOut}
              onChange={(e) => setSubOut(e.target.value.replace(/\D/g, ""))}
            />
          </label>
          <label className="flex flex-col">
            On #
            <input
              className="w-16 rounded border border-zinc-700 bg-zinc-900 px-2 py-1"
              inputMode="numeric"
              value={subIn}
              onChange={(e) => setSubIn(e.target.value.replace(/\D/g, ""))}
            />
          </label>
          <button type="submit" className="rounded bg-zinc-700 px-3 py-1 text-white">
            Request change
          </button>
          {note && <span className="text-zinc-400">{note}</span>}
        </form>
      )}

      {result && (
        <p className="text-zinc-300" data-testid="final">
          Full time{" "}
          <span data-testid="final-score">
            {result.home} – {result.away}
          </span>{" "}
          · match checksum <code data-testid="checksum">{result.checksum}</code>
        </p>
      )}
      {error && <p className="text-red-400">{error}</p>}
      <p className="text-xs text-zinc-600" data-testid="sync">
        sync <span data-testid="sync-mode">{syncMode}</span>: {simFrames} frames simulated · host
        watermark {state.hostFrame}
        {state.finalFrame !== null ? ` · final ${state.finalFrame}` : ""}
      </p>
      <EventFeed events={events} />
    </section>
  );
}
