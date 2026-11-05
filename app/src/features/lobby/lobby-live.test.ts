import { describe, expect, it } from "vitest";

import { type LobbyLiveRow, mergeLive } from "./lobby-live";
import type { LobbyState } from "./lobby-schema";

const base: LobbyState = {
  id: "l1",
  code: "123456",
  status: "open",
  role: "guest",
  seed: 7,
  conditions: { weather: "Sunny", maintenance: 1, wind: 0, windAngle: 0, night: false },
  hostName: "Host",
  guestName: null,
  hostFrame: 0,
  finalFrame: null,
  inputs: [],
};

const lobby = (frame: number, final: number | null = null): LobbyLiveRow => ({
  row_kind: "lobby",
  id: "l1",
  status: final === null ? "playing" : "finished",
  frame,
  final_frame: final,
  guest_name: "Guest",
  team: null,
  action_kind: null,
  payload: null,
  seq: null,
});

const input = (id: string, frame: number | null, seq: number | null): LobbyLiveRow => ({
  row_kind: "input",
  id,
  status: null,
  frame,
  final_frame: null,
  guest_name: null,
  team: 1,
  action_kind: "sub",
  payload: { out: 10, in: 14 },
  seq,
});

describe("mergeLive", () => {
  it("returns null without a lobby row", () => {
    expect(mergeLive(base, [input("a", 5, 1)])).toBeNull();
  });

  it("merges the lobby row and orders inputs by frame, then seq, unapplied last", () => {
    const s = mergeLive(base, [
      input("c", null, null),
      input("b", 10, 2),
      lobby(40),
      input("a", 10, 1),
    ]);
    expect(s).not.toBeNull();
    expect(s!.status).toBe("playing");
    expect(s!.hostFrame).toBe(40);
    expect(s!.guestName).toBe("Guest");
    expect(s!.seed).toBe(7);
    expect(s!.inputs.map((i) => i.id)).toEqual(["a", "b", "c"]);
    expect(s!.inputs[0]!.action).toEqual({ kind: "sub", out: 10, in: 14 });
  });

  it("carries the final frame", () => {
    expect(mergeLive(base, [lobby(120, 120)])!.finalFrame).toBe(120);
  });
});
