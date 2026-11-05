import type { ChunkMessage } from "./protocol";
import type { Replay, ReplayEvent } from "./replay/replay";

/**
 * Builds a growing `Replay` from the worker's header + chunk stream.
 * The renderer (replay/drawPitch.ts) reads the same shape the SimRunner writes to replay files.
 */
export class LiveReplay {
  readonly replay: Replay;
  /** Stats rows (ReplayRecorder.StatsLength numbers each). */
  readonly stats: number[][] = [];
  finished = false;
  finalHeader: Partial<Replay> | null = null;

  constructor(headerJson: string) {
    const header = JSON.parse(headerJson) as Omit<Replay, "frames" | "events">;
    this.replay = { ...header, frames: [], events: [] };
  }

  get frameCount(): number {
    return this.replay.frames.length;
  }

  /** Seconds of match recorded so far (replay time, not match clock). */
  get duration(): number {
    const last = this.replay.frames[this.replay.frames.length - 1];
    return last ? (last[0] ?? 0) / 100 : 0;
  }

  addChunk(
    chunk: Pick<ChunkMessage, "len" | "frames" | "events" | "stats" | "finished" | "final">,
  ) {
    const { len, frames } = chunk;
    for (let i = 0; i + len <= frames.length; i += len) {
      this.replay.frames.push(Array.from(frames.subarray(i, i + len)));
    }
    const events = JSON.parse(chunk.events) as ReplayEvent[];
    if (events.length) this.replay.events.push(...events);
    // Stats rows have a fixed length too, but the worker does not send it; keep the flat batch per chunk.
    if (chunk.stats.length) this.stats.push(Array.from(chunk.stats));
    if (chunk.finished) {
      this.finished = true;
      if (chunk.final) this.finalHeader = JSON.parse(chunk.final) as Partial<Replay>;
    }
  }

  /** Final score from the last frame (indices 3, 4 of the frame layout). */
  get score(): { home: number; away: number } {
    const last = this.replay.frames[this.replay.frames.length - 1];
    return { home: last?.[3] ?? 0, away: last?.[4] ?? 0 };
  }
}
