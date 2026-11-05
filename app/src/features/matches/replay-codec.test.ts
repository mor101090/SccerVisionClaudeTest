import { describe, expect, it } from "vitest";

import type { Replay } from "@/engine/replay/replay";

import { decodeReplay, encodeReplay, isGzip } from "./replay-codec";

const replay = {
  version: 1,
  seed: 7,
  fps: 10,
  halfRealSeconds: 600,
  halftimeRealSeconds: 120,
  home: { name: "Red", shortName: "RED", kit: {}, players: [] },
  away: { name: "Blue", shortName: "BLU", kit: {}, players: [] },
  frames: Array.from({ length: 50 }, (_, i) => [i * 10, 0, 0, 0, 0, 1, 2, 3]),
  events: [],
} as unknown as Replay;

describe("replay codec", () => {
  it("round-trips through gzip and is smaller than the JSON", async () => {
    const gz = await encodeReplay(replay);
    expect(isGzip(gz)).toBe(true);
    expect(gz.byteLength).toBeLessThan(JSON.stringify(replay).length);
    expect(await decodeReplay(gz)).toEqual(replay);
  });

  it("rejects non-gzip bytes", () => {
    expect(isGzip(new TextEncoder().encode("{}"))).toBe(false);
  });
});
