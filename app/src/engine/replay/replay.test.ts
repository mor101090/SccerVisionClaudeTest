import { describe, expect, it } from "vitest";

import { BALL_SPIN, type Replay, sample } from "./replay";

function replayWith(frameLength: number, spin?: [number, number, number]): Replay {
  const frame = (t: number) => {
    const f = Array.from({ length: frameLength }, () => 0);
    f[0] = t;
    f[8] = -1; // the ball is free
    if (spin) [f[BALL_SPIN], f[BALL_SPIN + 1], f[BALL_SPIN + 2]] = spin;
    return f;
  };
  return {
    version: 1,
    seed: 1,
    fps: 10,
    halfRealSeconds: 600,
    halftimeRealSeconds: 120,
    home: {} as Replay["home"],
    away: {} as Replay["away"],
    frames: [frame(0), frame(10)],
    events: [],
  };
}

describe("the ball's physical spin in a replay frame (ball physics stage 6)", () => {
  it("sits after the 22 gestures, at the end of the frame the engine writes", () => {
    // 9 header numbers, 22 players x 5, 3 officials x 3, the ball that went out (3), the other man of a substitution (4), 22 gestures
    expect(BALL_SPIN).toBe(9 + 22 * 5 + 9 + 3 + 4 + 22);
  });

  it("is read in rad per replay second", () => {
    const s = sample(replayWith(BALL_SPIN + 3, [125, -40, 7]), 0.05);
    expect(s.ball.w).toEqual([12.5, -4, 0.7]);
  });

  it("is absent from older replays, whose frames end before it", () => {
    const s = sample(replayWith(BALL_SPIN), 0.05);
    expect(s.ball.w).toBeUndefined();
  });
});
