import { RESET_LINK_MINUTES } from "./policy";

/**
 * AMR methods Supabase puts on a session created by `verifyOtp` (recovery uses `otp`).
 * A password login is `password` and a social login is `oauth`; neither may set a new password.
 */
const RECOVERY_METHODS = new Set(["otp", "magiclink", "recovery"]);

/** Clock skew allowed when the AMR timestamp is slightly ahead of this server. */
const SKEW_SEC = 60;

/**
 * True when every AMR entry is a recovery method and was issued within the reset-link window.
 * An ordinary signed-in session (password or social) is not a recovery session.
 */
export function isRecoverySession(
  claims: { amr?: unknown } | null | undefined,
  nowSec = Math.floor(Date.now() / 1000),
): boolean {
  const amr = claims?.amr;
  if (!Array.isArray(amr) || amr.length === 0) return false;
  const windowSec = RESET_LINK_MINUTES * 60;
  for (const entry of amr) {
    if (!entry || typeof entry !== "object" || !("method" in entry)) return false;
    const method = (entry as { method?: unknown }).method;
    const timestamp = (entry as { timestamp?: unknown }).timestamp;
    if (typeof method !== "string" || !RECOVERY_METHODS.has(method)) return false;
    if (typeof timestamp !== "number" || !Number.isFinite(timestamp)) return false;
    const age = nowSec - timestamp;
    if (age < -SKEW_SEC || age > windowSec) return false;
  }
  return true;
}
