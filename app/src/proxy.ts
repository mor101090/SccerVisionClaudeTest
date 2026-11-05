import { createServerClient } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

import { clientEnv } from "@/env";
import { sessionIdOf } from "@/features/auth/password-session";
import {
  passwordAuthenticatedAt,
  sessionStartedDuringPause,
} from "@/features/auth/recovery-session";
import { passwordPauseStartedAt } from "@/server/login-throttle";
import { isAllowedPasswordSession } from "@/server/password-sessions";

/** Member pages: signed-out visitors go to the login screen. /auth/finish needs a session to pick a username. */
const PROTECTED = ["/account", "/matches", "/teams", "/leagues", "/lobby", "/team", "/auth/finish"];

const isProtected = (path: string) => PROTECTED.some((p) => path === p || path.startsWith(`${p}/`));

/**
 * Next 16 proxy (formerly middleware). Refreshes the Supabase session cookie on every page and API call (access
 * tokens are short-lived and Server Components can't write cookies), and redirects signed-out users away from member
 * pages. Route Handlers and Server Actions still check the session themselves (see requireUser): this is not the
 * only guard.
 */
export default async function proxy(request: NextRequest) {
  const url = clientEnv.NEXT_PUBLIC_SUPABASE_URL;
  const key = clientEnv.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY;
  if (!url || !key) return NextResponse.next({ request });

  let response = NextResponse.next({ request });
  const supabase = createServerClient(url, key, {
    cookies: {
      getAll: () => request.cookies.getAll(),
      setAll(list) {
        for (const { name, value } of list) request.cookies.set(name, value);
        response = NextResponse.next({ request });
        for (const { name, value, options } of list) response.cookies.set(name, value, options);
      },
    },
  });

  // Validates the JWT and refreshes it when expired; nothing may run between createServerClient and this call.
  const { data } = await supabase.auth.getClaims();
  const claims = data?.claims;
  let signedIn = Boolean(claims?.sub);
  const email = typeof claims?.email === "string" ? claims.email : "";
  if (signedIn && passwordAuthenticatedAt(claims) !== null) {
    try {
      const sessionId = sessionIdOf(claims);
      if (!sessionId || !(await isAllowedPasswordSession(sessionId))) signedIn = false;
    } catch (err) {
      console.error("[auth] password session check failed", err);
      signedIn = false;
    }
  }
  if (signedIn && email) {
    try {
      const pauseStarted = await passwordPauseStartedAt(email);
      if (sessionStartedDuringPause(passwordAuthenticatedAt(claims), pauseStarted))
        signedIn = false;
    } catch (err) {
      console.error("[auth] lockout check failed", err);
    }
  }

  const path = request.nextUrl.pathname;
  if (!signedIn && isProtected(path)) {
    const login = request.nextUrl.clone();
    login.pathname = "/auth/sign-in";
    login.search = `?redirectTo=${encodeURIComponent(path + request.nextUrl.search)}`;
    const redirect = NextResponse.redirect(login);
    for (const c of response.cookies.getAll()) redirect.cookies.set(c);
    return redirect;
  }
  return response;
}

export const config = {
  // Everything except static files: built assets, the classic game, the engine and images.
  matcher: [
    "/((?!_next/static|_next/image|classic/|engine/|auth/.*\\.svg|favicon.ico|.*\\.(?:png|jpg|jpeg|gif|svg|webp|ico|wasm|json|js|css|mp3|ogg)$).*)",
  ],
};
