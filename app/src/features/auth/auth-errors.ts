export type AuthError = { code?: string; status?: number; message?: string } | null | undefined;

export type AuthErrorKind =
  | "user_exists"
  | "email_not_verified"
  | "invalid_credentials"
  | "invalid_otp"
  | "otp_expired"
  | "too_many_attempts"
  | "invalid_token"
  | "weak_password"
  | "rate_limited"
  | "unknown";

/**
 * Neon's server SDK rewrites Better Auth's error code from the HTTP status (a 422 "User already
 * exists" arrives as code `validation_failed`), so classify by the original code OR its message.
 */
export function classifyAuthError(e: AuthError): AuthErrorKind {
  if (!e) return "unknown";
  const code = (e.code ?? "").toUpperCase();
  const msg = (e.message ?? "").toLowerCase();
  if (e.status === 429 || code === "TOO_MANY_REQUESTS") return "rate_limited";
  if (
    code.startsWith("USER_ALREADY_EXISTS") ||
    code === "EMAIL_EXISTS" ||
    msg.includes("already exists")
  ) {
    return "user_exists";
  }
  if (code === "EMAIL_NOT_VERIFIED" || msg.includes("email not verified"))
    return "email_not_verified";
  if (
    code === "INVALID_EMAIL_OR_PASSWORD" ||
    code === "INVALID_CREDENTIALS" ||
    msg.includes("invalid email or password")
  ) {
    return "invalid_credentials";
  }
  if (code === "OTP_EXPIRED" || msg.includes("otp expired")) return "otp_expired";
  if (code === "TOO_MANY_ATTEMPTS" || msg.includes("too many attempts")) return "too_many_attempts";
  if (code === "INVALID_OTP" || msg.includes("invalid otp")) return "invalid_otp";
  if (code === "INVALID_TOKEN" || msg.includes("invalid token")) return "invalid_token";
  if (code.startsWith("PASSWORD_TOO_") || msg.startsWith("password too")) return "weak_password";
  return "unknown";
}
