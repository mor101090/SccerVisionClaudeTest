export type AuthError = { code?: string; status?: number; message?: string } | null | undefined;

export type AuthErrorKind =
  | "user_exists"
  | "email_not_verified"
  | "invalid_credentials"
  | "otp_invalid_or_expired"
  | "weak_password"
  | "same_password"
  | "rate_limited"
  | "unknown";

const RATE_LIMIT_CODES = new Set([
  "over_request_rate_limit",
  "over_email_send_rate_limit",
  "over_sms_send_rate_limit",
]);

/**
 * Maps a Supabase Auth error to what the form should say. Supabase sends a stable `code`
 * (https://supabase.com/docs/guides/auth/debugging/error-codes); the HTTP status covers rate limits without one.
 * Supabase answers a wrong and an expired code the same way (`otp_expired`), so they are one kind.
 */
export function classifyAuthError(e: AuthError): AuthErrorKind {
  if (!e) return "unknown";
  const code = e.code ?? "";
  if (e.status === 429 || RATE_LIMIT_CODES.has(code)) return "rate_limited";
  if (code === "user_already_exists" || code === "email_exists") return "user_exists";
  if (code === "email_not_confirmed") return "email_not_verified";
  if (code === "invalid_credentials") return "invalid_credentials";
  if (code === "otp_expired" || code === "otp_disabled") return "otp_invalid_or_expired";
  if (code === "weak_password") return "weak_password";
  if (code === "same_password") return "same_password";
  return "unknown";
}
