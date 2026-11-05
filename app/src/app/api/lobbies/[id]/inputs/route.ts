import { NextResponse } from "next/server";
import { z } from "zod";

import { inputActionSchema } from "@/features/lobby/lobby-schema";
import { parseJson, withUser } from "@/server/http";
import { requestInput } from "@/server/lobbies";

/** Guest: request a substitution / plan change. The host's engine applies it. */
export const POST = withUser(
  async (user, request: Request, ctx: RouteContext<"/api/lobbies/[id]/inputs">) => {
    const { id } = await ctx.params;
    if (!z.uuid().safeParse(id).success)
      return NextResponse.json({ error: "Not found" }, { status: 404 });
    const body = await parseJson(request, inputActionSchema);
    if ("error" in body) return body.error;
    return NextResponse.json(await requestInput(user.id, id, body.data), { status: 201 });
  },
);
