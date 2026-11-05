import { NextResponse } from "next/server";
import { z } from "zod";

import { withUser } from "@/server/http";
import { startLobby } from "@/server/lobbies";

export const POST = withUser(
  async (user, _req: Request, ctx: RouteContext<"/api/lobbies/[id]/start">) => {
    const { id } = await ctx.params;
    if (!z.uuid().safeParse(id).success)
      return NextResponse.json({ error: "Not found" }, { status: 404 });
    await startLobby(user.id, id);
    return NextResponse.json({ ok: true });
  },
);
