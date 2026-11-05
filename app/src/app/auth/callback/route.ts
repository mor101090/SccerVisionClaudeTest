import { NextResponse, type NextRequest } from "next/server";

import { currentUser } from "@/server/auth";
import { nextPathAfterSignIn } from "@/server/auth-flow";

/**
 * Return point after Google sign-in (and after linking Google). The proxy has already exchanged the
 * OAuth verifier for the session cookie; here we route the user: "Almost there" until the profile
 * is complete, then where they were going.
 */
export async function GET(request: NextRequest) {
  const user = await currentUser();
  const to = user
    ? await nextPathAfterSignIn(user.id, request.nextUrl.searchParams.get("redirectTo"))
    : "/auth/sign-in?error=social";
  // Second guard behind safeRedirect: never leave this origin, whatever the path parses to.
  const target = new URL(to, request.url);
  const here = new URL(request.url);
  return NextResponse.redirect(target.origin === here.origin ? target : new URL("/play", here));
}
