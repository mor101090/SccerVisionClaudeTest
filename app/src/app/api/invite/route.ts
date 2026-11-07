import { type NextRequest, NextResponse } from "next/server";

import { INVITE_COOKIE, isInviteUsername } from "@/server/invites";
import { managerByUsername } from "@/server/managers";

/** "Create my club" on an invite page: remember who invited (httpOnly, 30 days), then sign up. */
export async function GET(req: NextRequest) {
  const u = req.nextUrl.searchParams.get("u") ?? "";
  const next =
    req.nextUrl.searchParams.get("next") === "sign-in" ? "/auth/sign-in" : "/auth/sign-up";
  const res = NextResponse.redirect(new URL(`${next}?redirectTo=/team/new`, req.url));
  if (isInviteUsername(u) && (await managerByUsername(u))) {
    res.cookies.set(INVITE_COOKIE, u, {
      httpOnly: true,
      sameSite: "lax",
      secure: req.nextUrl.protocol === "https:",
      maxAge: 60 * 60 * 24 * 30,
      path: "/",
    });
  }
  return res;
}
