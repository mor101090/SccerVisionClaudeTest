import { NextResponse, type NextRequest } from "next/server";

import { currentUser } from "@/server/auth";
import { nextPathAfterSignIn } from "@/server/auth-flow";
import { supabaseServer } from "@/server/supabase";

/**
 * Return point after a social sign-in (and after linking an account). Supabase redirects here with `?code=` (PKCE):
 * exchange it for the session cookie, then route the user: "Almost there" until the profile is complete, then where
 * they were going. A provider error arrives as `?error=`.
 */
export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams;
  const code = params.get("code");
  if (code && !params.get("error")) {
    const { error } = await (await supabaseServer()).auth.exchangeCodeForSession(code);
    if (error)
      console.error("[auth] code exchange failed", { status: error.status, code: error.code });
  }
  const user = await currentUser();
  const to = user
    ? await nextPathAfterSignIn(user.id, params.get("redirectTo"))
    : "/auth/sign-in?error=social";
  // Second guard behind safeRedirect: never leave this origin, whatever the path parses to.
  const target = new URL(to, request.url);
  const here = new URL(request.url);
  return NextResponse.redirect(target.origin === here.origin ? target : new URL("/play", here));
}
