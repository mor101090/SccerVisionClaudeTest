import type { InputActionBody, LobbyState } from "./lobby-schema";

/** One row of the lobby live query (src/server/realtime.ts): the lobby itself or one input. */
export type LobbyLiveRow =
  | {
      row_kind: "lobby";
      id: string;
      status: LobbyState["status"];
      frame: number;
      final_frame: number | null;
      guest_name: string | null;
      team: null;
      action_kind: null;
      payload: null;
      seq: null;
    }
  | {
      row_kind: "input";
      id: string;
      status: null;
      frame: number | null;
      final_frame: null;
      guest_name: null;
      team: number;
      action_kind: "plan" | "sub";
      payload: Record<string, unknown>;
      seq: number | null;
    };

/**
 * Merges a live snapshot into the full lobby state (code, seed, names... come from the first GET and
 * never change). Inputs are ordered like the server's GET: by frame, then seq (unapplied last).
 * Returns null when the snapshot has no lobby row (not a member any more, or not loaded yet).
 */
export function mergeLive(base: LobbyState, rows: readonly LobbyLiveRow[]): LobbyState | null {
  const lobby = rows.find((r) => r.row_kind === "lobby");
  if (!lobby || lobby.row_kind !== "lobby") return null;
  const inputs = rows
    .filter((r): r is Extract<LobbyLiveRow, { row_kind: "input" }> => r.row_kind === "input")
    .map((r) => ({
      id: r.id,
      team: (r.team === 1 ? 1 : 0) as 0 | 1,
      action: { kind: r.action_kind, ...r.payload } as InputActionBody,
      frame: r.frame,
      seq: r.seq,
    }))
    .sort(
      (a, b) =>
        (a.frame ?? Infinity) - (b.frame ?? Infinity) ||
        (a.seq ?? Infinity) - (b.seq ?? Infinity) ||
        a.id.localeCompare(b.id),
    );
  return {
    ...base,
    status: lobby.status,
    hostFrame: lobby.frame,
    finalFrame: lobby.final_frame,
    guestName: lobby.guest_name,
    inputs,
  };
}
