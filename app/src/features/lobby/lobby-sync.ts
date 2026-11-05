import {
  createRealtimeClient,
  type RealtimeClient,
  type SealedLiveQuery,
} from "@neon/realtime/client";

import { type LobbyLiveRow, mergeLive } from "./lobby-live";
import type { LobbyState } from "./lobby-schema";

export type SyncMode = "connecting" | "realtime" | "polling";

/** Fallback polling interval (only when Realtime is unavailable). */
const POLL_MS = 1500;
/** Renew sealed queries this long before they expire (they live ~60 s). */
const RENEW_EARLY_MS = 10_000;
const RETRY_MS = 1_000;

async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { cache: "no-store", ...init });
  const body = (await res.json().catch(() => ({}))) as T & { error?: string };
  if (!res.ok) throw Object.assign(new Error(body.error ?? res.statusText), { status: res.status });
  return body;
}

type Sealed = { url: string; sealed: SealedLiveQuery<LobbyLiveRow> };

/**
 * Keeps a lobby's state up to date.
 *
 * Realtime: one GET for the static parts, then a live query pushes every committed change (lobby row
 * + inputs) over one WebSocket. Renewing the sealed query costs one request a minute. No polling.
 * Fallback: if Realtime is not configured here (503), not allowed for this origin, or errors, it polls.
 *
 * `onState` is called with each new state, in commit order.
 */
export function syncLobby(
  lobbyId: string,
  onState: (s: LobbyState) => void,
  onMode: (m: SyncMode) => void,
  onError: (message: string | null) => void,
): () => void {
  let stopped = false;
  let base: LobbyState | null = null;
  let rt: RealtimeClient | null = null;
  let pollTimer: ReturnType<typeof setTimeout> | undefined;
  let renewTimer: ReturnType<typeof setTimeout> | undefined;

  const read = () => getJson<LobbyState>(`/api/lobbies/${lobbyId}`);
  const seal = () => getJson<Sealed>(`/api/lobbies/${lobbyId}/live`, { method: "POST" });

  function poll() {
    if (stopped) return;
    onMode("polling");
    read()
      .then((s) => {
        base = s;
        onError(null);
        onState(s);
        // Full time known (or the game is closed): the client has everything it will ever need.
        if (s.finalFrame !== null || s.status === "closed") return;
        pollTimer = setTimeout(poll, POLL_MS);
      })
      .catch((e: unknown) => {
        onError(e instanceof Error ? e.message : String(e));
        pollTimer = setTimeout(poll, POLL_MS);
      });
  }

  function fallBack(reason: string) {
    if (stopped) return;
    console.warn(`[lobby] realtime unavailable, polling instead: ${reason}`);
    clearTimeout(renewTimer);
    rt?.close();
    rt = null;
    poll();
  }

  async function start() {
    try {
      base = await read();
      onState(base);
    } catch (e) {
      onError(e instanceof Error ? e.message : String(e));
    }
    if (stopped) return;

    let first: Sealed;
    try {
      first = await seal();
    } catch (e) {
      return fallBack(e instanceof Error ? e.message : String(e));
    }
    if (stopped) return;

    rt = createRealtimeClient({ url: first.url });
    const sub = rt.subscribe(first.sealed);
    onMode("connecting");

    sub.onChange((snap) => {
      if (snap.status === "error") return fallBack(snap.error?.message ?? "subscription error");
      if (snap.status === "live") onMode("realtime");
      if (!snap.data || !base) return;
      const s = mergeLive(base, snap.data);
      if (!s) return;
      base = s;
      onError(null);
      onState(s);
    });

    // Renew before expiry; a failed renewal falls back to polling.
    const schedule = (expiresAt: number) => {
      clearTimeout(renewTimer);
      if (stopped) return;
      renewTimer = setTimeout(renew, Math.max(0, expiresAt - Date.now() - RENEW_EARLY_MS));
    };
    const renew = async () => {
      let next: Sealed;
      try {
        next = await seal();
      } catch {
        renewTimer = setTimeout(renew, RETRY_MS);
        return;
      }
      schedule(next.sealed.expiresAt);
      sub.renew(next.sealed).catch((e: unknown) => fallBack(String(e)));
    };
    schedule(first.sealed.expiresAt);
  }

  void start();

  return () => {
    stopped = true;
    clearTimeout(pollTimer);
    clearTimeout(renewTimer);
    rt?.close();
  };
}
