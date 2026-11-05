import { NextResponse } from "next/server";
import { z } from "zod";

import { withUser } from "@/server/http";
import { getLobbyState } from "@/server/lobbies";

/** Polled by both players (~2×/s while playing). */
export const GET = withUser(async (user, _req: Request, ctx: RouteContext<"/api/lobbies/[id]">) => {
  const { id } = await ctx.params;
  if (!z.uuid().safeParse(id).success)
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(await getLobbyState(user.id, id), {
    headers: { "Cache-Control": "no-store" },
  });
});
