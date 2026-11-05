import { z } from "zod";

/** Failed password logins allowed per email before a pause. */
export const LOGIN_MAX_FAILURES = 5;
/** Length of the failure window and of the pause, in minutes. */
export const LOGIN_LOCK_MINUTES = 15;
/** Neon Auth reset links expire after 15 minutes. */
export const RESET_LINK_MINUTES = 15;
/** Email verification codes expire after 10 minutes. */
export const VERIFY_CODE_MINUTES = 10;
/** Seconds before "resend" is offered again. */
export const RESEND_RESET_SECONDS = 45;
export const RESEND_CODE_SECONDS = 60;

export const PASSWORD_MIN = 8;
export const PASSWORD_MAX = 128;

const email = z
  .string()
  .trim()
  .toLowerCase()
  .pipe(z.email("Enter a valid email address."))
  .pipe(z.string().max(254, "Enter a valid email address."));

/** At least 8 characters, with a number or a symbol. */
export const newPassword = z
  .string()
  .min(PASSWORD_MIN, `At least ${PASSWORD_MIN} characters.`)
  .max(PASSWORD_MAX, `At most ${PASSWORD_MAX} characters.`)
  .regex(/[^A-Za-z]/, "Add a number or a symbol.");

/** 3–20 letters, numbers or _. */
export const username = z
  .string()
  .trim()
  .regex(/^[A-Za-z0-9_]{3,20}$/, "3–20 letters, numbers or _.");

const checked = z.literal("on", { error: "Please accept the Terms and Conditions to continue." });

/** Run the match check even when other fields failed, so every error shows at once. */
function bothPasswords({ value }: { value: unknown }): boolean {
  const v = value as { password?: unknown; confirmPassword?: unknown } | null;
  return typeof v?.password === "string" && typeof v.confirmPassword === "string";
}

export const signInSchema = z.object({
  email,
  password: z.string().min(1, "Enter your password.").max(PASSWORD_MAX),
  rememberMe: z.literal("on").optional(),
});

export const signUpSchema = z
  .object({ email, password: newPassword, confirmPassword: z.string(), terms: checked })
  .refine((v) => v.password === v.confirmPassword, {
    path: ["confirmPassword"],
    message: "The passwords don't match.",
    when: bothPasswords,
  });

export const forgotSchema = z.object({ email });

export const resetSchema = z
  .object({ token: z.string().min(1).max(512), password: newPassword, confirmPassword: z.string() })
  .refine((v) => v.password === v.confirmPassword, {
    path: ["confirmPassword"],
    message: "The passwords don't match.",
    when: bothPasswords,
  });

export const verifySchema = z.object({
  email,
  otp: z.string().regex(/^\d{6}$/, "Enter the 6-digit code."),
});

export const finishSchema = z.object({ username, terms: checked.optional() });

export type Strength = {
  score: 0 | 1 | 2 | 3 | 4;
  label: "" | "Weak" | "Fair" | "Good" | "Strong";
};

/** Four-bar meter (Figma: Reset password – strength). Advisory; the policy above is what is enforced. */
export function passwordStrength(pw: string): Strength {
  if (!pw) return { score: 0, label: "" };
  let score = 0;
  if (pw.length >= PASSWORD_MIN) score++;
  if (/[^A-Za-z]/.test(pw)) score++;
  if (pw.length >= 12) score++;
  if (/[a-z]/.test(pw) && /[A-Z]/.test(pw) && /\d/.test(pw) && /[^A-Za-z0-9]/.test(pw)) score++;
  const s = Math.max(1, score) as Strength["score"];
  return { score: s, label: (["", "Weak", "Fair", "Good", "Strong"] as const)[s] };
}

/** Field errors keyed by form field name, first message per field. */
export function fieldErrors(error: z.ZodError): Record<string, string> {
  const out: Record<string, string> = {};
  for (const issue of error.issues) {
    const key = String(issue.path[0] ?? "form");
    out[key] ??= issue.message;
  }
  return out;
}

/** Only same-site relative paths; anything else falls back to the default. */
export function safeRedirect(value: unknown, fallback = "/play"): string {
  const v = typeof value === "string" ? value : "";
  // Browsers drop tabs/newlines and read "\" as "/" in URLs ("/\t/evil.com" is "//evil.com"): refuse both outright,
  // then accept only a path that stays on this origin once parsed.
  if (!v.startsWith("/") || /[\u0000-\u001f\u007f\\]/.test(v)) return fallback;
  // Parse only to check: dot segments can normalize "/.//evil.com" to the path "//evil.com", which a second parse
  // reads as another host. Refuse that, and return the value as given (never the normalized form).
  const base = "http://same.invalid";
  const url = new URL(v, base);
  return url.origin === base && !url.pathname.startsWith("//") ? v : fallback;
}
