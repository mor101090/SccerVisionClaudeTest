import "server-only";

import { cacheLife } from "next/cache";
import { cookies } from "next/headers";
import { cache } from "react";

import { passkeyAuthenticatedAt } from "@/features/auth/passkeys";
import { sessionIdOf } from "@/features/auth/password-session";
import {
  passwordAuthenticatedAt,
  sessionStartedDuringPause,
} from "@/features/auth/recovery-session";

import { provisionalName } from "./auth-flow";
import { passwordPauseStartedAt } from "./login-throttle";
import { isAllowedPasskeySession } from "./passkeys";
import { isAllowedPasswordSession } from "./password-sessions";
import { profileForRequest } from "./profiles";
import { supabaseServer } from "./supabase";

export type User = { id: string; email: string; name: string };

/**
 * The signed-in user, or null. The session JWT is verified by getClaims() (locally when the project signs with
 * asymmetric keys). The name is the manager's display name, else the username, else the social profile's name,
 * else the email's local part.
 *
 * Cached per session for five minutes so tab switches reuse it. connection() used to block prefetch, which made
 * every click wait on the server. getClaims() reads the clock; that is allowed inside this private cache, and the
 * result is not stored in the shared server cache.
 */
async function readCurrentUser(): Promise<User | null> {
  "use cache: private";
  cacheLife("minutes");
  await cookies();
  const supabase = await supabaseServer();
  const { data } = await supabase.auth.getClaims();
  const claims = data?.claims;
  if (!claims?.sub) return null;
  const email = typeof claims.email === "string" ? claims.email : "";
  // A password grant the app did not create (a direct Auth API call) is not a session. A password
  // session opened after this pause began is not signed in either. Social sign-in keeps working.
  // A passkey session counts only once /auth/callback vetted it (the passkey used was approved by the app).
  // The checks are independent, so they run together (one database round trip); every one must pass.
  const sessionId = sessionIdOf(claims);
  const viaPassword = passwordAuthenticatedAt(claims) !== null;
  const viaPasskey = passkeyAuthenticatedAt(claims) !== null;
  const [passwordOk, passkeyOk, pauseStarted, profile] = await Promise.all([
    viaPassword ? (sessionId ? isAllowedPasswordSession(sessionId) : false) : true,
    viaPasskey ? (sessionId ? isAllowedPasskeySession(sessionId) : false) : true,
    email ? passwordPauseStartedAt(email) : null,
    profileForRequest(claims.sub),
  ]);
  if (!passwordOk || !passkeyOk) return null;
  if (email && sessionStartedDuringPause(passwordAuthenticatedAt(claims), pauseStarted))
    return null;
  const meta = (claims.user_metadata ?? {}) as { full_name?: unknown; name?: unknown };
  const socialName =
    typeof meta.full_name === "string"
      ? meta.full_name
      : typeof meta.name === "string"
        ? meta.name
        : null;
  const name = profile?.displayName || profile?.username || socialName || provisionalName(email);
  return { id: claims.sub, email, name };
}

/** The signed-in user for this request. Repeated calls in one render share one lookup. */
export const currentUser = cache(readCurrentUser);

/** The signed-in user, or throws. Use in Route Handlers and Server Actions before any data access. */
export async function requireUser() {
  const user = await currentUser();
  if (!user) throw new UnauthorizedError();
  return user;
}

export class UnauthorizedError extends Error {
  constructor() {
    super("Unauthorized");
    this.name = "UnauthorizedError";
  }
}
