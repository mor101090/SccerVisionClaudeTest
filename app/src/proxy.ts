import { createServerClient } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

import { clientEnv } from "@/env";
import { passkeyAuthenticatedAt } from "@/features/auth/passkeys";
import { sessionIdOf } from "@/features/auth/password-session";
import {
  passwordAuthenticatedAt,
  sessionStartedDuringPause,
} from "@/features/auth/recovery-session";
import { passwordPauseStartedAt } from "@/server/login-throttle";
import { isAllowedPasskeySession } from "@/server/passkeys";
import { isAllowedPasswordSession } from "@/server/password-sessions";

/** Member pages: signed-out visitors go to the login screen. /auth/finish needs a session to pick a username. */
const PROTECTED = [
  "/account",
  "/settings",
  "/matches",
  "/teams",
  "/leagues",
  "/lobby",
  "/team",
  "/auth/finish",
  // the signed-in app (/home and /rules stay public: visitors get the landing page and the rules)
  "/squad",
  "/tactics",
  "/games",
  "/training",
  "/coaches",
  "/transfer",
  "/forum",
  "/league",
  "/rankings",
  "/finances",
  "/stadium",
  "/notifications",
  "/messages",
  "/search",
  "/profile",
  "/managers",
  "/clubs",
  "/club",
  "/invite",
  "/support",
];

const isProtected = (path: string) => PROTECTED.some((p) => path === p || path.startsWith(`${p}/`));

/**
 * Next 16 proxy (formerly middleware). Refreshes the Supabase session cookie on every page and API call (access
 * tokens are short-lived and Server Components can't write cookies), and redirects signed-out users away from member
 * pages. Route Handlers and Server Actions still check the session themselves (see requireUser): this is not the
 * only guard.
 */
type Claims = Parameters<typeof passwordAuthenticatedAt>[0];

/** The app's extra session rules (see currentUser): vetted password / passkey sessions, and the login pause. */
async function sessionStillValid(claims: Claims, email: string): Promise<boolean> {
  const sessionId = sessionIdOf(claims);
  const check = async (label: string, run: () => Promise<boolean>) => {
    try {
      return await run();
    } catch (err) {
      console.error(`[auth] ${label} check failed`, err);
      return false;
    }
  };
  const [passwordOk, passkeyOk, pauseOk] = await Promise.all([
    passwordAuthenticatedAt(claims) === null
      ? true
      : check(
          "password session",
          async () => !!sessionId && (await isAllowedPasswordSession(sessionId)),
        ),
    passkeyAuthenticatedAt(claims) === null
      ? true
      : check(
          "passkey session",
          async () => !!sessionId && (await isAllowedPasskeySession(sessionId)),
        ),
    !email
      ? true
      : passwordPauseStartedAt(email).then(
          (started) => !sessionStartedDuringPause(passwordAuthenticatedAt(claims), started),
          (err) => {
            // As before: a failed lockout check does not sign anyone out.
            console.error("[auth] lockout check failed", err);
            return true;
          },
        ),
  ]);
  return passwordOk && passkeyOk && pauseOk;
}

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
  const path = request.nextUrl.pathname;
  let signedIn = Boolean(claims?.sub);
  // The session checks only decide whether a member page redirects to the login screen, so they run only there,
  // all at once (one database round trip). Pages and handlers still check the session themselves (currentUser).
  if (signedIn && isProtected(path)) {
    const email = typeof claims?.email === "string" ? claims.email : "";
    signedIn = await sessionStillValid(claims, email);
  }
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
