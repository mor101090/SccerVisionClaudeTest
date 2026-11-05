import { LiveReplay } from "./live-replay";
import type { FromWorker, ScheduledInput, StartOptions, ToWorker } from "./protocol";

/** Written by scripts/build-engine.sh. */
export interface EngineManifest {
  version: string;
  base: string;
}

async function currentEngine(): Promise<EngineManifest> {
  const res = await fetch("/engine/current.json", { cache: "no-cache" });
  if (!res.ok) throw new Error("Engine not built (run `yarn engine:build`)");
  return (await res.json()) as EngineManifest;
}

export interface MatchCallbacks {
  onHeader?: (live: LiveReplay) => void;
  onChunk?: (live: LiveReplay) => void;
  onFinished?: (live: LiveReplay) => void;
  onError?: (message: string) => void;
  /** Lockstep: an input took effect at `frame` (`order`: n-th input applied this match). */
  onApplied?: (id: string, frame: number, order: number) => void;
}

/**
 * Main-thread handle on the engine worker. One instance per page; one match at a time.
 * Browser-only: create it inside an effect / event handler.
 */
export class EngineClient {
  private worker: Worker;
  private ready: Promise<void>;
  private token = 0;
  private live: LiveReplay | null = null;
  private callbacks: MatchCallbacks = {};

  constructor(base?: string) {
    this.worker = new Worker(new URL("./engine.worker.ts", import.meta.url), { type: "module" });
    const workerReady = new Promise<void>((resolve, reject) => {
      const onFirst = (e: MessageEvent<FromWorker>) => {
        if (e.data.type === "ready") {
          this.worker.removeEventListener("message", onFirst);
          resolve();
        } else if (e.data.type === "error") {
          this.worker.removeEventListener("message", onFirst);
          reject(new Error(e.data.message));
        }
      };
      this.worker.addEventListener("message", onFirst);
    });
    this.worker.addEventListener("message", (e: MessageEvent<FromWorker>) =>
      this.onMessage(e.data),
    );
    const manifest: Promise<EngineManifest> = base
      ? Promise.resolve({ base, version: base.split("/").filter(Boolean).pop() ?? "unknown" })
      : currentEngine();
    this.ready = manifest.then((m) => {
      this.version = m.version;
      this.post({ cmd: "init", base: m.base });
      return workerReady;
    });
  }

  /** Content hash of the loaded engine build (known once ready). */
  version: string | null = null;

  whenReady(): Promise<void> {
    return this.ready;
  }

  async start(options: StartOptions, callbacks: MatchCallbacks = {}): Promise<number> {
    await this.ready;
    this.token += 1;
    this.live = null;
    this.callbacks = callbacks;
    this.post({ cmd: "start", token: this.token, ...options });
    return this.token;
  }

  go(): void {
    this.post({ cmd: "go", token: this.token });
  }

  /** Live pacing: the viewer has played this many frames. */
  progress(frames: number): void {
    this.post({ cmd: "progress", token: this.token, frames });
  }

  setPlan(team: 0 | 1, plan: string): void {
    this.post({ cmd: "plan", token: this.token, team, plan });
  }

  substitute(team: 0 | 1, outNumber: number, inNumber: number): void {
    this.post({ cmd: "sub", token: this.token, team, out: outNumber, in: inNumber });
  }

  /** Lockstep: never simulate past `frames` (-1: no limit). */
  limit(frames: number): void {
    this.post({ cmd: "limit", token: this.token, frames });
  }

  /** Lockstep: apply an input at `input.frame` (null: next step). */
  schedule(input: ScheduledInput): void {
    this.post({ cmd: "schedule", token: this.token, input });
  }

  stop(): void {
    this.token += 1;
    this.post({ cmd: "stop", token: this.token });
  }

  dispose(): void {
    this.worker.terminate();
  }

  private post(message: ToWorker): void {
    this.worker.postMessage(message);
  }

  private onMessage(m: FromWorker): void {
    if (m.type === "error") {
      this.callbacks.onError?.(m.message);
      return;
    }
    if (m.type === "ready" || m.token !== this.token) return;
    if (m.type === "applied") {
      this.callbacks.onApplied?.(m.id, m.frame, m.order);
      return;
    }
    if (m.type === "header") {
      this.live = new LiveReplay(m.header);
      this.callbacks.onHeader?.(this.live);
      return;
    }
    if (!this.live) return;
    this.live.addChunk(m);
    this.callbacks.onChunk?.(this.live);
    if (m.finished) this.callbacks.onFinished?.(this.live);
  }
}
