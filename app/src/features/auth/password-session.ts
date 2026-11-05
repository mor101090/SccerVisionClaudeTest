/** The Supabase session id carried in an access token, or null when the token has none. */
export function sessionIdFromAccessToken(token: string): string | null {
  const payload = token.split(".")[1];
  if (!payload) return null;
  try {
    const json = JSON.parse(Buffer.from(payload, "base64url").toString("utf8")) as {
      session_id?: unknown;
    };
    return typeof json.session_id === "string" && json.session_id.length > 0
      ? json.session_id
      : null;
  } catch {
    return null;
  }
}

/** session_id claim from getClaims(), or null. */
export function sessionIdOf(claims: object | null | undefined): string | null {
  if (!claims || !("session_id" in claims)) return null;
  const id = (claims as { session_id?: unknown }).session_id;
  return typeof id === "string" && id.length > 0 ? id : null;
}
