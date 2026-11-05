import "server-only";

import { cookies } from "next/headers";
import { cache } from "react";

import { provisionalName } from "./auth-flow";
import { getProfile } from "./profiles";
import { supabaseServer } from "./supabase";

export type User = { id: string; email: string; name: string };

/**
 * The signed-in user, or null. The session JWT is verified by getClaims() (locally when the project signs with
 * asymmetric keys). The name is the manager's display name, else the username, else the social profile's name,
 * else the email's local part. Cached per request.
 */
export const currentUser = cache(async (): Promise<User | null> => {
  // Touch cookies() first: during prerender this suspends in our code, not inside the SDK.
  await cookies();
  const supabase = await supabaseServer();
  const { data } = await supabase.auth.getClaims();
  const claims = data?.claims;
  if (!claims?.sub) return null;
  const email = typeof claims.email === "string" ? claims.email : "";
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
