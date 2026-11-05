import { NextResponse } from "next/server";
import { z } from "zod";

import { serverEnv } from "@/env";
import { withUser } from "@/server/http";
import { getLobbyState } from "@/server/lobbies";
import { lobbyLiveQuery, realtime } from "@/server/realtime";

/**
 * Seals the lobby live query for the signed-in player (also used to renew it, ~once a minute).
 * 503 when Realtime is not configured here: the client then polls instead.
 */
export const POST = withUser(
  async (user, _req: Request, ctx: RouteContext<"/api/lobbies/[id]/live">) => {
    const { id } = await ctx.params;
    if (!z.uuid().safeParse(id).success)
      return NextResponse.json({ error: "Not found" }, { status: 404 });
    const rt = realtime();
    if (!rt) return NextResponse.json({ error: "Realtime not configured" }, { status: 503 });
    await getLobbyState(user.id, id); // 404s for non-members
    const sealed = await rt.seal({ query: lobbyLiveQuery(user.id, id) });
    return NextResponse.json(
      { url: serverEnv.NEON_REALTIME_URL, sealed },
      { headers: { "Cache-Control": "no-store" } },
    );
  },
);
