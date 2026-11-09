/**
 * Passkeys (Supabase Auth, WebAuthn). Bound to the relying party soccer.dev-spirit.com: a passkey made there only works
 * there (not on localhost or *.vercel.app). The browser runs the ceremony, so these calls use the browser client.
 */

export type PasskeyErrorKind = "cancelled" | "expired" | "already_registered" | "failed";

type ErrorLike = { code?: unknown; name?: unknown; cause?: unknown } | null | undefined;

/**
 * Sorts a passkey error so the UI can stay quiet when the user simply closed the prompt, and retry when the challenge
 * expired (Supabase's challenge lives 5 minutes; an autofill prompt can stay open longer).
 */
export function passkeyErrorKind(error: unknown): PasskeyErrorKind {
  const e = error as ErrorLike;
  const code = typeof e?.code === "string" ? e.code : "";
  const causeName = (e?.cause as ErrorLike)?.name;
  const name = typeof e?.name === "string" ? e.name : "";
  if (code === "webauthn_challenge_expired") return "expired";
  if (code === "ERROR_AUTHENTICATOR_PREVIOUSLY_REGISTERED") return "already_registered";
  if (
    code === "ERROR_CEREMONY_ABORTED" ||
    name === "NotAllowedError" ||
    name === "AbortError" ||
    causeName === "NotAllowedError" ||
    causeName === "AbortError"
  )
    return "cancelled";
  return "failed";
}

/** Text for a passkey error, or null when nothing should be shown (the user cancelled). */
export function passkeyErrorMessage(error: unknown, action: "signin" | "create"): string | null {
  switch (passkeyErrorKind(error)) {
    case "cancelled":
      return null;
    case "expired":
      return "That took a little too long. Try again.";
    case "already_registered":
      return "This device already has a passkey for your account.";
    default:
      return action === "signin"
        ? "We couldn't log you in with a passkey. Try again, or use your email or another option."
        : "We couldn't create the passkey. Try again in a moment.";
  }
}

/** The relying party every passkey is bound to. Changing it breaks every existing passkey. */
export const PASSKEY_RP_ID = "soccer.dev-spirit.com";

/** True on the relying party or a subdomain of it: the only hosts where a passkey ceremony can succeed. */
export function passkeyHostAllowed(hostname: string): boolean {
  const host = hostname.toLowerCase();
  return host === PASSKEY_RP_ID || host.endsWith(`.${PASSKEY_RP_ID}`);
}

/**
 * True when passkeys can work here: the browser supports WebAuthn and the page is on the relying party's host.
 * Elsewhere (localhost, *.vercel.app) every ceremony fails with an RP ID error, so passkey UI stays hidden.
 */
export function passkeysSupported(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.PublicKeyCredential === "function" &&
    passkeyHostAllowed(window.location.hostname)
  );
}

/** True when the browser can offer passkeys in the email field's autofill (WebAuthn Conditional UI). */
export async function passkeyAutofillSupported(): Promise<boolean> {
  if (!passkeysSupported()) return false;
  const check = window.PublicKeyCredential.isConditionalMediationAvailable;
  return typeof check === "function"
    ? check.call(window.PublicKeyCredential).catch(() => false)
    : false;
}

const AMR_PASSKEY = new Set(["passkey", "webauthn"]);
/** "Log in faster next time?" is offered for this long after a sign-in. */
export const OFFER_WINDOW_SECONDS = 10 * 60;

/**
 * Whether to offer "create a passkey" now: a sign-in in the last few minutes that didn't use a passkey (from the
 * session's AMR claim), not dismissed before. Whether the user already has a passkey is checked separately.
 */
export function shouldOfferPasskey(amr: unknown, nowSeconds: number, dismissed: boolean): boolean {
  if (dismissed || !Array.isArray(amr) || amr.length === 0) return false;
  let latest = 0;
  for (const entry of amr) {
    const method = (entry as { method?: unknown })?.method;
    const ts = (entry as { timestamp?: unknown })?.timestamp;
    if (typeof method === "string" && AMR_PASSKEY.has(method)) return false;
    if (typeof ts === "number" && ts > latest) latest = ts;
  }
  return latest > 0 && nowSeconds - latest <= OFFER_WINDOW_SECONDS;
}

/** How recent a login must be before a passkey can be added (like GitHub's "sudo mode"). */
export const RECENT_LOGIN_SECONDS = 10 * 60;

/**
 * True when the session's latest authentication (AMR claim timestamps) is within RECENT_LOGIN_SECONDS. A passkey keeps
 * working after a password reset, so adding one asks for a fresh login: someone at an unlocked, signed-in device can't
 * quietly add their own.
 */
export function loggedInRecently(amr: unknown, nowSeconds: number): boolean {
  if (!Array.isArray(amr)) return false;
  let latest = 0;
  for (const entry of amr) {
    const ts = (entry as { timestamp?: unknown })?.timestamp;
    if (typeof ts === "number" && ts > latest) latest = ts;
  }
  return latest > 0 && nowSeconds - latest <= RECENT_LOGIN_SECONDS && latest - nowSeconds < 60;
}

/** Remembered per user in this browser: "Not now" hides the offer for good. */
export const offerDismissedKey = (userId: string) => `sv_passkey_offer_dismissed:${userId}`;

/** A readable default name for a new passkey ("Chrome on Mac"), from the browser's user agent. Max 120 chars. */
export function deviceLabel(userAgent: string): string {
  const ua = userAgent;
  const browser = /Edg\//.test(ua)
    ? "Edge"
    : /OPR\//.test(ua)
      ? "Opera"
      : /Firefox\//.test(ua)
        ? "Firefox"
        : /Chrome\//.test(ua)
          ? "Chrome"
          : /Safari\//.test(ua)
            ? "Safari"
            : "Browser";
  const os = /iPhone/.test(ua)
    ? "iPhone"
    : /iPad/.test(ua)
      ? "iPad"
      : /Android/.test(ua)
        ? "Android"
        : /Mac OS X|Macintosh/.test(ua)
          ? "Mac"
          : /Windows/.test(ua)
            ? "Windows"
            : /Linux/.test(ua)
              ? "Linux"
              : "";
  return (os ? `${browser} on ${os}` : browser).slice(0, 120);
}

/**
 * When the session was opened with a passkey (Supabase's AMR method "passkey", internal/models/factor.go), else null.
 * Such a session counts only once the app vetted it (server/passkeys.ts).
 */
export function passkeyAuthenticatedAt(
  claims: { amr?: unknown } | null | undefined,
): number | null {
  const amr = claims?.amr;
  if (!Array.isArray(amr)) return null;
  let at: number | null = null;
  for (const entry of amr) {
    const method = (entry as { method?: unknown })?.method;
    const ts = (entry as { timestamp?: unknown })?.timestamp;
    if (method !== "passkey") continue;
    const t = typeof ts === "number" && Number.isFinite(ts) ? ts : 0;
    at = at === null ? t : Math.max(at, t);
  }
  return at;
}

/** Seconds between a passkey login and the passkey's last_used_at for them to be the same event. */
export const PASSKEY_USE_TOLERANCE_SECONDS = 120;

/**
 * The passkey a login used: Supabase doesn't put its id in the session, so take the passkey whose last use matches the
 * login time. Null when none matches (the session can't be vetted).
 */
export function passkeyUsedAt(
  passkeys: readonly { id: string; last_used_at?: string | null }[],
  authenticatedAt: number,
): string | null {
  let best: { id: string; diff: number } | null = null;
  for (const p of passkeys) {
    const used = Date.parse(p.last_used_at ?? "") / 1000;
    if (!Number.isFinite(used)) continue;
    const diff = Math.abs(used - authenticatedAt);
    if (diff <= PASSKEY_USE_TOLERANCE_SECONDS && (best === null || diff < best.diff)) {
      best = { id: p.id, diff };
    }
  }
  return best?.id ?? null;
}
