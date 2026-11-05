import { NextResponse } from "next/server";

import { withUser } from "@/server/http";
import { createLobby } from "@/server/lobbies";

/** Host a new online game. Returns its id and six-digit code. */
export const POST = withUser(async (user) =>
  NextResponse.json(await createLobby(user.id), { status: 201 }),
);
