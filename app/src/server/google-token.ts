import "server-only";

import { createRemoteJWKSet, jwtVerify } from "jose";

import { clientEnv } from "@/env";
import { sha256Hex } from "@/features/auth/google-id";

/** Google's signing keys (cached and rotated by jose). */
const GOOGLE_KEYS = createRemoteJWKSet(new URL("https://www.googleapis.com/oauth2/v3/certs"));

export type GoogleIdentity = { sub: string; email: string; name: string | null };

/**
 * Verifies a Google ID token before we act on its email: Google's signature, issuer, audience (our client ID),
 * expiry, a verified email, and the nonce bound to this browser's sign-in (sha256 of the raw nonce it kept), so a
 * token issued elsewhere or replayed can't be used to look up an account. Null when anything doesn't hold.
 */
export async function verifyGoogleIdToken(
  token: string,
  rawNonce: string,
): Promise<GoogleIdentity | null> {
  const audience = clientEnv.NEXT_PUBLIC_GOOGLE_CLIENT_ID;
  if (!audience || token.length > 4096 || rawNonce.length > 256) return null;
  try {
    const { payload } = await jwtVerify(token, GOOGLE_KEYS, {
      issuer: ["https://accounts.google.com", "accounts.google.com"],
      audience,
      algorithms: ["RS256"],
    });
    if (payload.nonce !== (await sha256Hex(rawNonce))) return null;
    if (payload.email_verified !== true || typeof payload.email !== "string") return null;
    if (typeof payload.sub !== "string") return null;
    return {
      sub: payload.sub,
      email: payload.email.toLowerCase(),
      name: typeof payload.name === "string" ? payload.name : null,
    };
  } catch {
    return null;
  }
}
