import { NextResponse } from "next/server";
import { z } from "zod";

import { progressSchema } from "@/features/lobby/lobby-schema";
import { parseJson, withUser } from "@/server/http";
import { getLobbyState, reportProgress } from "@/server/lobbies";

/**
 * Host: engine watermark + inputs applied since the last report.
 * Answers with the lobby state, so this request is also the host's poll (half the requests).
 */
export const POST = withUser(
  async (user, request: Request, ctx: RouteContext<"/api/lobbies/[id]/progress">) => {
    const { id } = await ctx.params;
    if (!z.uuid().safeParse(id).success)
      return NextResponse.json({ error: "Not found" }, { status: 404 });
    const body = await parseJson(request, progressSchema);
    if ("error" in body) return body.error;
    await reportProgress(user.id, id, body.data);
    return NextResponse.json(await getLobbyState(user.id, id), {
      headers: { "Cache-Control": "no-store" },
    });
  },
);
