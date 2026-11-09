import { describe, expect, it } from "vitest";

import { contactCut } from "./ball3d";
import { BALL_CONTACT, BALL_SPIN, type Replay, sample } from "./replay";

function replayWith(frameLength: number, spin?: [number, number, number], contact?: [number, number, number, number]): Replay {
  const frame = (t: number) => {
    const f = Array.from({ length: frameLength }, () => 0);
    f[0] = t;
    f[8] = -1; // the ball is free
    if (spin) [f[BALL_SPIN], f[BALL_SPIN + 1], f[BALL_SPIN + 2]] = spin;
    if (contact) [f[BALL_CONTACT], f[BALL_CONTACT + 1], f[BALL_CONTACT + 2], f[BALL_CONTACT + 3]] = contact;
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

describe("a contact in a replay frame (ball physics stage 9)", () => {
  it("follows the spin and is read as metres and a unit direction", () => {
    expect(BALL_CONTACT).toBe(BALL_SPIN + 3);
    const s = sample(replayWith(BALL_CONTACT + 4, undefined, [38, -100, 0, 0]), 0.05);
    expect(s.ball.contact).toEqual([0.038, -1, 0, 0]);
  });

  it("is absent with no contact in the frame, and from older replays", () => {
    expect(sample(replayWith(BALL_CONTACT + 4), 0.05).ball.contact).toBeUndefined();
    expect(sample(replayWith(BALL_CONTACT), 0.05).ball.contact).toBeUndefined();
  });

  const identity = (dx: number, dy: number): [number, number] => [dx, dy];

  it("flattens the side the ball hit, by the flattening over the radius", () => {
    // a shot pushed back off the post towards -x: the flat side faces +x (the post), 3.3 cm of 11 cm deep
    const cut = contactCut([0.033, -1, 0, 0], identity);
    expect(cut?.[0]).toBeCloseTo(1);
    expect(cut?.[1]).toBeCloseTo(0);
    expect(cut?.[2]).toBeCloseTo(0.3);
  });

  it("shows nothing from above for a bounce (the flat cap is underneath) or a light touch", () => {
    expect(contactCut([0.03, 0, 0, 1], identity)).toBeNull();
    expect(contactCut([0.003, 1, 0, 0], identity)).toBeNull();
    expect(contactCut(undefined, identity)).toBeNull();
  });
});
