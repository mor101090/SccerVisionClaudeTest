import { NextResponse, type NextRequest } from "next/server";

import { justAutoLinked } from "@/features/auth/linking";
import { passkeyAuthenticatedAt } from "@/features/auth/passkeys";
import { HOME_PATH, safeRedirect } from "@/features/auth/policy";
import { currentUser } from "@/server/auth";
import { nextPathAfterSignIn } from "@/server/auth-flow";
import { vetPasskeySession } from "@/server/passkeys";
import { supabaseServer } from "@/server/supabase";

/**
 * Return point after a social sign-in (and after linking an account). Supabase redirects here with `?code=` (PKCE):
 * exchange it for the session cookie, then route the user: "Almost there" until the profile is complete, then where
 * they were going. A provider error arrives as `?error=`. A sign-in Supabase just merged into an existing account
 * (same verified email) goes to "You already have an account" first.
 */
export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams;
  const code = params.get("code");
  if (code && !params.get("error")) {
    const { error } = await (await supabaseServer()).auth.exchangeCodeForSession(code);
    if (error)
      console.error("[auth] code exchange failed", { status: error.status, code: error.code });
  }
  // A passkey sign-in counts only if the passkey it used was approved by the app (server/passkeys.ts).
  const { data: session } = await (await supabaseServer()).auth.getClaims();
  if (
    passkeyAuthenticatedAt(session?.claims) !== null &&
    !(await vetPasskeySession(session?.claims))
  ) {
    await (await supabaseServer()).auth.signOut();
    return NextResponse.redirect(new URL("/auth/sign-in?error=passkey", request.url));
  }
  const user = await currentUser();
  const providerError = params.get("error_code") ?? params.get("error");
  const redirectTo = safeRedirect(params.get("redirectTo"));
  // Supabase merges a sign-in whose verified email matches an existing account. Unless the user asked for this link
  // (Settings → Connect, or "Connect" on the confirm screen), ask before going on.
  if (
    user &&
    !providerError &&
    params.get("linkConfirmed") !== "1" &&
    !redirectTo.startsWith("/settings")
  ) {
    const { data } = await (await supabaseServer()).auth.getUserIdentities();
    if (data && justAutoLinked(data.identities, Date.now())) {
      return NextResponse.redirect(
        new URL(`/auth/connect-account?redirectTo=${encodeURIComponent(redirectTo)}`, request.url),
      );
    }
  }
  const to = user
    ? await nextPathAfterSignIn(user.id, params.get("redirectTo"))
    : "/auth/sign-in?error=social";
  // Second guard behind safeRedirect: never leave this origin, whatever the path parses to.
  const target = new URL(to, request.url);
  const here = new URL(request.url);
  // Still signed in after a provider error: a Connect (linkIdentity) from Settings failed. Pass the reason on.
  if (user && providerError) target.searchParams.set("linkError", providerError.slice(0, 64));
  return NextResponse.redirect(target.origin === here.origin ? target : new URL(HOME_PATH, here));
}
