import { NextResponse } from "next/server";
import { z } from "zod";

import { lobbyCodeSchema } from "@/features/lobby/lobby-schema";
import { parseJson, withUser } from "@/server/http";
import { joinLobby } from "@/server/lobbies";

export const POST = withUser(async (user, request: Request) => {
  const body = await parseJson(request, z.object({ code: lobbyCodeSchema }));
  if ("error" in body) return body.error;
  return NextResponse.json(await joinLobby(user.id, body.data.code));
});
