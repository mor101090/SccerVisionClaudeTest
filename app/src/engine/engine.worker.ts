/// <reference lib="webworker" />
/**
 * Runs the C# match engine (WebAssembly) off the main thread and streams the match as replay data:
 * a header, then batches of frames, events and stats. Port of tools/demo-page/engine-worker.js.
 */

import type { DotnetModule, LiveEngineExports } from "./dotnet";
import type { FromWorker, ScheduledInput, ToWorker } from "./protocol";
import { planTextIsSafe } from "../features/lobby/plan-text";

declare const self: DedicatedWorkerGlobalScope;

let engine: LiveEngineExports | null = null;
let frameLength = 0;
let token = 0;
let running = false;
let again = false;
let generated = 0;
let played = 0;
let lead = 0;
let held = false;
/** Lockstep limit (-1: none). */
let limit = -1;
/** Inputs waiting for their frame; applied between steps, in arrival order. */
let pending: ScheduledInput[] = [];
let appliedCount = 0;

/**
 * Applies the inputs due at the current frame. Online play always steps 5 frames at a time,
 * so the host and the guest reach the same frame boundaries and apply inputs at the same point.
 */
function applyDue(L: LiveEngineExports): void {
  if (!pending.length) return;
  const keep: ScheduledInput[] = [];
  for (const input of pending) {
    if (input.frame !== null && input.frame > generated) {
      keep.push(input);
      continue;
    }
    // A bad plan must leave the queue. Leaving it at the head makes every later step throw on the same request.
    try {
      if (input.kind === "plan") {
        if (planTextIsSafe(input.plan)) L.SetPlanFor(input.team, input.plan);
      } else L.SubstituteFor(input.team, input.out, input.in);
    } catch {
      // Still reported below, so the host records it and a reload does not schedule it again.
    }
    appliedCount += 1;
    send({ type: "applied", token, id: input.id, frame: generated, order: appliedCount });
  }
  pending = keep;
}

function send(message: FromWorker, transfer: Transferable[] = []): void {
  self.postMessage(message, transfer);
}

function errorText(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}

function requireEngine(): LiveEngineExports {
  if (!engine) throw new Error("Engine not initialised");
  return engine;
}

self.onmessage = async (e: MessageEvent<ToWorker>) => {
  const m = e.data;
  try {
    switch (m.cmd) {
      case "init": {
        // The .NET loader lives in public/, outside the bundle: load it at runtime.
        const url = new URL(`${m.base}_framework/dotnet.js`, self.location.origin).href;
        const mod = (await import(
          /* webpackIgnore: true */ /* turbopackIgnore: true */ url
        )) as DotnetModule;
        const runtime = await mod.dotnet.withDiagnosticTracing(false).create();
        const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
        engine = exports.LiveEngine;
        frameLength = engine.FrameLength();
        send({ type: "ready" });
        break;
      }
      case "start": {
        const L = requireEngine();
        token = m.token;
        lead = m.lead;
        generated = 0;
        played = 0;
        held = !!m.hold;
        limit = -1;
        pending = [];
        appliedCount = 0;
        L.UseHomeTeam?.(m.homeTeam ?? "");
        const header = L.Start(
          m.seed,
          m.mode,
          m.weather,
          m.maintenance,
          m.wind,
          m.windAngle,
          m.night,
          m.plan ?? "",
          m.competition ?? "League",
          m.homeStrength ?? "medium",
          m.awayStrength ?? "medium",
        );
        send({ type: "header", token, header });
        if (!held) void pump();
        break;
      }
      case "go":
        if (m.token === token && held) {
          held = false;
          void pump();
        }
        break;
      case "progress":
        if (m.token === token) {
          played = m.frames;
          if (!held) void pump();
        }
        break;
      case "plan":
        if (m.token === token) requireEngine().SetPlanFor(m.team, m.plan);
        break;
      case "sub":
        if (m.token === token) requireEngine().SubstituteFor(m.team, m.out, m.in);
        break;
      case "stop":
        token = m.token;
        break;
      case "limit":
        if (m.token === token) {
          limit = m.frames;
          if (!held) void pump();
        }
        break;
      case "schedule":
        if (m.token === token) {
          pending.push(m.input);
          if (!held) void pump();
        }
        break;
    }
  } catch (err) {
    send({ type: "error", message: errorText(err) });
  }
};

async function pump(): Promise<void> {
  if (running) {
    again = true;
    return;
  }
  running = true;
  try {
    const L = requireEngine();
    do {
      again = false;
      const mine = token;
      while (
        mine === token &&
        !L.Finished() &&
        (lead === 0 || generated - played < lead) &&
        (limit < 0 || generated < limit)
      ) {
        applyDue(L);
        // Copies: the engine reuses its buffers.
        const frames = new Int32Array(L.Step(lead === 0 ? 30 : 5));
        const stats = new Int32Array(L.Stats());
        generated += frames.length / frameLength;
        const finished = L.Finished();
        send(
          {
            type: "chunk",
            token: mine,
            len: frameLength,
            frames,
            events: L.Events(),
            stats,
            finished,
            final: finished ? L.FinalHeader() : null,
          },
          [frames.buffer, stats.buffer],
        );
        // Let progress / stop messages in.
        await new Promise((r) => setTimeout(r, 0));
      }
    } while (again);
  } catch (err) {
    send({ type: "error", message: errorText(err) });
  } finally {
    running = false;
  }
}
