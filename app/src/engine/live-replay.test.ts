import { describe, expect, it } from "vitest";

import { LiveReplay } from "./live-replay";

const header = JSON.stringify({
  version: 1,
  seed: 42,
  fps: 10,
  halfRealSeconds: 600,
  halftimeRealSeconds: 120,
  home: { name: "Red", shortName: "RED", kit: {}, players: [] },
  away: { name: "Blue", shortName: "BLU", kit: {}, players: [] },
});

function frame(t: number, home: number, away: number, len = 6) {
  const f = new Array<number>(len).fill(0);
  f[0] = t;
  f[3] = home;
  f[4] = away;
  return f;
}

describe("LiveReplay", () => {
  it("splits flat frame batches into frames and tracks score and duration", () => {
    const live = new LiveReplay(header);
    live.addChunk({
      len: 6,
      frames: Int32Array.from([...frame(0, 0, 0), ...frame(10, 1, 0)]),
      events: JSON.stringify([{ t: 0.1, type: "Goal", text: "Goal!" }]),
      stats: new Int32Array(),
      finished: false,
      final: null,
    });

    expect(live.frameCount).toBe(2);
    expect(live.duration).toBeCloseTo(0.1);
    expect(live.score).toEqual({ home: 1, away: 0 });
    expect(live.replay.events).toHaveLength(1);
    expect(live.replay.seed).toBe(42);
    expect(live.finished).toBe(false);
  });

  it("records the final header when the match finishes", () => {
    const live = new LiveReplay(header);
    live.addChunk({
      len: 6,
      frames: Int32Array.from(frame(540000, 2, 1)),
      events: "[]",
      stats: new Int32Array(),
      finished: true,
      final: JSON.stringify({ seed: 42 }),
    });
    expect(live.finished).toBe(true);
    expect(live.finalHeader).toEqual({ seed: 42 });
  });
});
