import { NextResponse, type NextRequest } from "next/server";

import { HOME_PATH } from "@/features/auth/policy";
import { currentUser } from "@/server/auth";

/**
 * "Real website platform" (the button on the classic game at /): signed in → the homepage; signed out → the login
 * screen, which lands on the homepage afterwards.
 */
export async function GET(request: NextRequest) {
  const user = await currentUser();
  const to = user ? HOME_PATH : `/auth/sign-in?redirectTo=${encodeURIComponent(HOME_PATH)}`;
  return NextResponse.redirect(new URL(to, request.url));
}
