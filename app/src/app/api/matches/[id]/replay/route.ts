import { NextResponse } from "next/server";
import { z } from "zod";

import { REPLAY_CONTENT_TYPE } from "@/features/matches/replay-codec";
import { currentUser } from "@/server/auth";
import { getReplayBytes } from "@/server/matches";

/** Streams the owner's gzipped replay. Same origin, so no bucket CORS is needed. */
export async function GET(_request: Request, ctx: RouteContext<"/api/matches/[id]/replay">) {
  const user = await currentUser();
  if (!user) return NextResponse.json({ error: "Unauthorized" }, { status: 401 });

  const { id } = await ctx.params;
  if (!z.uuid().safeParse(id).success)
    return NextResponse.json({ error: "Not found" }, { status: 404 });

  const bytes = await getReplayBytes(user.id, id);
  if (!bytes) return NextResponse.json({ error: "Not found" }, { status: 404 });

  return new Response(bytes as BodyInit, {
    headers: {
      "Content-Type": REPLAY_CONTENT_TYPE,
      "Cache-Control": "private, max-age=3600",
    },
  });
}
