import { NextResponse } from "next/server";
import { z } from "zod";

import { clientEnv } from "@/env";
import { withUser } from "@/server/http";
import { getLobbyState } from "@/server/lobbies";

/**
 * The Realtime channel for this lobby, for its host or guest: the private topic `lobby:<id>` (RLS on
 * realtime.messages lets only them receive it; see drizzle/0002_lobby_realtime.sql).
 * 503 when Supabase is not configured here: the client then polls instead.
 */
export const POST = withUser(
  async (user, _req: Request, ctx: RouteContext<"/api/lobbies/[id]/live">) => {
    const { id } = await ctx.params;
    if (!z.uuid().safeParse(id).success)
      return NextResponse.json({ error: "Not found" }, { status: 404 });
    if (!clientEnv.NEXT_PUBLIC_SUPABASE_URL || !clientEnv.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY)
      return NextResponse.json({ error: "Realtime not configured" }, { status: 503 });
    await getLobbyState(user.id, id); // 404s for non-members
    return NextResponse.json(
      { topic: `lobby:${id}` },
      { headers: { "Cache-Control": "no-store" } },
    );
  },
);
