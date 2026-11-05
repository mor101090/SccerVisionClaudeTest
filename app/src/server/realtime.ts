import "server-only";

import { createRealtime, rawSql } from "@neon/realtime/server";

import { serverEnv } from "@/env";
import type { LobbyLiveRow } from "@/features/lobby/lobby-live";

type Realtime = ReturnType<typeof createRealtime>;
let instance: Realtime | null | undefined;

/** Null when Realtime is not configured for this environment (clients then fall back to polling). */
export function realtime(): Realtime | null {
  if (instance !== undefined) return instance;
  const secret = serverEnv.NEON_REALTIME_SECRET;
  const dbUrl = serverEnv.DATABASE_URL;
  if (!secret || !serverEnv.NEON_REALTIME_URL || !dbUrl) return (instance = null);
  const db = new URL(dbUrl).pathname.replace(/^\//, "") || "neondb";
  instance = createRealtime({ secret, db });
  return instance;
}

/**
 * Everything an online player needs, as ONE live query: the lobby row plus its input rows.
 * One subscription means each commit arrives atomically and in order, which the lockstep relies on:
 * the host writes inputs before it moves the watermark, so no snapshot shows a watermark without
 * the inputs below it. Authorization is in the SQL (host or guest of this lobby), so it stays live.
 * Deterministic and read-only, as Realtime requires.
 */
export function lobbyLiveQuery(userId: string, lobbyId: string) {
  return rawSql<LobbyLiveRow>(
    `SELECT 'lobby' AS row_kind, l.id::text AS id, l.status::text AS status,
            l.host_frame AS frame, l.final_frame AS final_frame, coalesce(g.display_name, g.username) AS guest_name,
            NULL::smallint AS team, NULL::text AS action_kind, NULL::jsonb AS payload, NULL::integer AS seq
       FROM lobbies l
       LEFT JOIN profiles g ON g.user_id = l.guest_id
      WHERE l.id = $1::uuid AND (l.host_id = $2::uuid OR l.guest_id = $2::uuid)
     UNION ALL
     SELECT 'input', i.id::text, NULL, i.frame, NULL, NULL, i.team, i.kind, i.payload, i.seq
       FROM lobby_inputs i
       JOIN lobbies l ON l.id = i.lobby_id
      WHERE i.lobby_id = $1::uuid AND (l.host_id = $2::uuid OR l.guest_id = $2::uuid)`,
    [lobbyId, userId],
  );
}
