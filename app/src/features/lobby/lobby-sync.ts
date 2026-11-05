import type { RealtimeChannel } from "@supabase/supabase-js";

import { supabaseBrowser } from "@/features/auth/supabase-client";

import type { LobbyState } from "./lobby-schema";

export type SyncMode = "connecting" | "realtime" | "polling";

/** Fallback polling interval (only when Realtime is unavailable). */
const POLL_MS = 1500;

async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { cache: "no-store", ...init });
  const body = (await res.json().catch(() => ({}))) as T & { error?: string };
  if (!res.ok) throw Object.assign(new Error(body.error ?? res.statusText), { status: res.status });
  return body;
}

/**
 * Keeps a lobby's state up to date.
 *
 * Realtime: the database broadcasts "changed" on the private channel `lobby:<id>` after every committed change to
 * the lobby or its inputs (drizzle/0002_lobby_realtime.sql). Each signal re-reads GET /api/lobbies/[id], which
 * reads the watermark before the inputs, so a state never shows a watermark without the inputs below it (the
 * lockstep relies on this). Reads never overlap: signals that arrive during a read cause exactly one more read,
 * and states are delivered in order.
 * Fallback: if Realtime is not configured here (503) or the channel fails, it polls.
 *
 * `onState` is called with each new state, in order.
 */
export function syncLobby(
  lobbyId: string,
  onState: (s: LobbyState) => void,
  onMode: (m: SyncMode) => void,
  onError: (message: string | null) => void,
): () => void {
  let stopped = false;
  let channel: RealtimeChannel | null = null;
  let pollTimer: ReturnType<typeof setTimeout> | undefined;
  let reading = false;
  let dirty = false;

  const read = () => getJson<LobbyState>(`/api/lobbies/${lobbyId}`);

  /** One read at a time; a signal during a read schedules one more. */
  async function refresh() {
    if (stopped) return;
    if (reading) {
      dirty = true;
      return;
    }
    reading = true;
    try {
      do {
        dirty = false;
        const s = await read();
        if (stopped) return;
        onError(null);
        onState(s);
      } while (dirty && !stopped);
    } catch (e) {
      onError(e instanceof Error ? e.message : String(e));
    } finally {
      reading = false;
    }
  }

  function poll() {
    if (stopped) return;
    onMode("polling");
    read()
      .then((s) => {
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
    if (stopped || !channel) return;
    console.warn(`[lobby] realtime unavailable, polling instead: ${reason}`);
    const ch = channel;
    channel = null;
    void supabaseBrowser().removeChannel(ch);
    poll();
  }

  async function start() {
    await refresh();
    if (stopped) return;

    let topic: string;
    try {
      ({ topic } = await getJson<{ topic: string }>(`/api/lobbies/${lobbyId}/live`, {
        method: "POST",
      }));
    } catch (e) {
      console.warn(`[lobby] realtime unavailable, polling instead: ${String(e)}`);
      return poll();
    }
    if (stopped) return;

    onMode("connecting");
    const supabase = supabaseBrowser();
    await supabase.realtime.setAuth(); // private channel: authorize with this user's session
    if (stopped) return;
    channel = supabase
      .channel(topic, { config: { private: true } })
      .on("broadcast", { event: "changed" }, () => void refresh())
      .subscribe((status, err) => {
        if (stopped) return;
        if (status === "SUBSCRIBED") {
          onMode("realtime");
          // Anything committed between the first read and the subscription.
          void refresh();
        } else if (status === "CHANNEL_ERROR" || status === "TIMED_OUT") {
          fallBack(err?.message ?? status);
        } else if (status === "CLOSED") {
          fallBack("channel closed");
        }
      });
  }

  void start();

  return () => {
    stopped = true;
    clearTimeout(pollTimer);
    if (channel) void supabaseBrowser().removeChannel(channel);
    channel = null;
  };
}
