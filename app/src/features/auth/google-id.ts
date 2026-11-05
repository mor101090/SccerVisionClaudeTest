/**
 * Google sign-in on this site's own domain (OpenID Connect implicit flow, `response_type=id_token`).
 *
 * Supabase's redirect flow returns to <project>.supabase.co, so Google's screens name that domain. Here Google returns
 * to /auth/google on this site with an ID token in the URL fragment; the page hands it to Supabase
 * (`signInWithIdToken`, or `linkIdentity` from Settings). Supabase checks the token's audience against the Google
 * client ID configured for the project and its nonce against the raw nonce we keep.
 */

export const GOOGLE_RETURN_PATH = "/auth/google";
const AUTHORIZE = "https://accounts.google.com/o/oauth2/v2/auth";
const PENDING_KEY = "sv_google_pending";

export type GoogleMode = "signin" | "link";

/** What we keep in sessionStorage while the browser is at Google. The raw nonce never leaves this browser. */
export type GooglePending = { state: string; nonce: string; mode: GoogleMode; redirectTo: string };

export function randomToken(bytes = 32): string {
  const b = crypto.getRandomValues(new Uint8Array(bytes));
  return Array.from(b, (x) => x.toString(16).padStart(2, "0")).join("");
}

/** Google gets sha256(nonce) as hex; Supabase gets the raw nonce and checks the hash. */
export async function sha256Hex(value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  return Array.from(new Uint8Array(digest), (x) => x.toString(16).padStart(2, "0")).join("");
}

export function googleAuthorizeURL(p: {
  clientId: string;
  origin: string;
  state: string;
  hashedNonce: string;
}): string {
  const url = new URL(AUTHORIZE);
  url.search = new URLSearchParams({
    client_id: p.clientId,
    redirect_uri: `${p.origin}${GOOGLE_RETURN_PATH}`,
    response_type: "id_token",
    scope: "openid email profile",
    nonce: p.hashedNonce,
    state: p.state,
    prompt: "select_account",
  }).toString();
  return url.toString();
}

export type GoogleReturn =
  { ok: true; idToken: string; state: string } | { ok: false; error: string; state: string | null };

/** Reads Google's answer from the URL fragment (`#id_token=…&state=…` or `#error=…`). */
export function parseGoogleReturn(hash: string): GoogleReturn {
  const params = new URLSearchParams(hash.startsWith("#") ? hash.slice(1) : hash);
  const state = params.get("state");
  const error = params.get("error");
  const idToken = params.get("id_token");
  if (error) return { ok: false, error: error.slice(0, 64), state };
  if (!idToken || !state) return { ok: false, error: "missing_token", state };
  return { ok: true, idToken, state };
}

export function savePending(pending: GooglePending): void {
  sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending));
}

/** Reads and deletes the pending sign-in: each one can be redeemed once. */
export function takePending(): GooglePending | null {
  const raw = sessionStorage.getItem(PENDING_KEY);
  sessionStorage.removeItem(PENDING_KEY);
  if (!raw) return null;
  try {
    const v = JSON.parse(raw) as Partial<GooglePending>;
    if (
      typeof v.state !== "string" ||
      typeof v.nonce !== "string" ||
      (v.mode !== "signin" && v.mode !== "link") ||
      typeof v.redirectTo !== "string"
    )
      return null;
    return v as GooglePending;
  } catch {
    return null;
  }
}

/** Leaves for Google. Resolves only if the redirect could not start. */
export async function startGoogle(clientId: string, mode: GoogleMode, redirectTo: string) {
  const state = randomToken(16);
  const nonce = randomToken(32);
  savePending({ state, nonce, mode, redirectTo });
  window.location.assign(
    googleAuthorizeURL({
      clientId,
      origin: window.location.origin,
      state,
      hashedNonce: await sha256Hex(nonce),
    }),
  );
}

/**
 * A verified Google sign-in waiting for "Connect Google?" (its email already has an account). Kept in this tab only
 * (sessionStorage), dropped after an answer or after CONFIRM_TTL_MS. The token is useless without its raw nonce.
 */
export type GoogleConfirm = {
  token: string;
  nonce: string;
  redirectTo: string;
  email: string;
  account: { username: string | null; clubName: string | null; signedUpWith: string };
  savedAt: number;
};

const CONFIRM_KEY = "sv_google_confirm";
export const CONFIRM_TTL_MS = 10 * 60_000;

export function saveGoogleConfirm(c: Omit<GoogleConfirm, "savedAt">): void {
  sessionStorage.setItem(CONFIRM_KEY, JSON.stringify({ ...c, savedAt: Date.now() }));
}

export function clearGoogleConfirm(): void {
  sessionStorage.removeItem(CONFIRM_KEY);
}

export function readGoogleConfirm(now = Date.now()): GoogleConfirm | null {
  const raw = sessionStorage.getItem(CONFIRM_KEY);
  if (!raw) return null;
  try {
    const c = JSON.parse(raw) as Partial<GoogleConfirm>;
    const fresh = typeof c.savedAt === "number" && now - c.savedAt <= CONFIRM_TTL_MS;
    if (
      fresh &&
      typeof c.token === "string" &&
      typeof c.nonce === "string" &&
      typeof c.redirectTo === "string" &&
      typeof c.email === "string" &&
      c.account &&
      typeof c.account.signedUpWith === "string"
    )
      return c as GoogleConfirm;
  } catch {
    // fall through
  }
  clearGoogleConfirm();
  return null;
}
