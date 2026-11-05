import "server-only";

import { cookies } from "next/headers";
import { connection } from "next/server";
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
import { getProfile } from "./profiles";
import { supabaseServer } from "./supabase";

export type User = { id: string; email: string; name: string };

/**
 * The signed-in user, or null. The session JWT is verified by getClaims() (locally when the project signs with
 * asymmetric keys). The name is the manager's display name, else the username, else the social profile's name,
 * else the email's local part. Cached per request.
 */
export const currentUser = cache(async (): Promise<User | null> => {
  // Stop prerendering here, in our code: the session is per request, and getClaims() reads the clock
  // (token expiry), which Next.js rejects during prerender, including the runtime-prefetch stage.
  await cookies();
  await connection();
  const supabase = await supabaseServer();
  const { data } = await supabase.auth.getClaims();
  const claims = data?.claims;
  if (!claims?.sub) return null;
  const email = typeof claims.email === "string" ? claims.email : "";
  // A password grant the app did not create (a direct Auth API call) is not a session. A password
  // session opened after this pause began is not signed in either. Social sign-in keeps working.
  if (passwordAuthenticatedAt(claims) !== null) {
    const sessionId = sessionIdOf(claims);
    if (!sessionId || !(await isAllowedPasswordSession(sessionId))) return null;
  }
  // A passkey session counts only once /auth/callback vetted it (the passkey used was approved by the app).
  if (passkeyAuthenticatedAt(claims) !== null) {
    const sessionId = sessionIdOf(claims);
    if (!sessionId || !(await isAllowedPasskeySession(sessionId))) return null;
  }
  if (email) {
    const pauseStarted = await passwordPauseStartedAt(email);
    if (sessionStartedDuringPause(passwordAuthenticatedAt(claims), pauseStarted)) return null;
  }
  const meta = (claims.user_metadata ?? {}) as { full_name?: unknown; name?: unknown };
  const socialName =
    typeof meta.full_name === "string"
      ? meta.full_name
      : typeof meta.name === "string"
        ? meta.name
        : null;
  const profile = await getProfile(claims.sub);
  const name = profile?.displayName || profile?.username || socialName || provisionalName(email);
  return { id: claims.sub, email, name };
});

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
