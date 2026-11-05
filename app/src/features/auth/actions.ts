"use server";

import { redirect } from "next/navigation";

import { formText } from "@/lib/form";
import { auth, currentUser, emailOtp } from "@/server/auth";
import {
  clearPendingEmail,
  getPendingEmail,
  nextPathAfterSignIn,
  provisionalName,
  requestOrigin,
  setPendingEmail,
} from "@/server/auth-flow";
import { clearFailures, lockedMinutes, recordFailure } from "@/server/login-throttle";
import { acceptTerms, completeProfile, getProfile, UsernameTakenError } from "@/server/profiles";
import { getTeam } from "@/server/teams";

import { classifyAuthError, type AuthError } from "./auth-errors";
import {
  fieldErrors,
  finishSchema,
  forgotSchema,
  LOGIN_LOCK_MINUTES,
  resetSchema,
  safeRedirect,
  signInSchema,
  signUpSchema,
  verifySchema,
} from "./policy";

/** Result of every auth form action. `fields` holds per-field messages, `error` a banner. */
export type AuthFormState = {
  error?: string;
  fields?: Record<string, string>;
  /** Echoed back so the form keeps what the user typed (never passwords). */
  values?: Record<string, string>;
  notice?: string;
  ok?: boolean;
} | null;

const RATE_LIMITED = "Too many requests. Wait a minute and try again.";

/** Unexpected auth failures: status and code only, never user input. */
function logAuthError(step: string, e: AuthError): void {
  console.error(`[auth] ${step} failed`, { status: e?.status, code: e?.code });
}

function isRateLimited(e: AuthError): boolean {
  return classifyAuthError(e) === "rate_limited";
}

async function sendCode(email: string): Promise<AuthError> {
  const { error } = await emailOtp().sendVerificationOtp({
    email,
    type: "email-verification",
  });
  return error as AuthError;
}

// ------------------------------------------------------------------ sign in

export async function signInAction(_prev: AuthFormState, form: FormData): Promise<AuthFormState> {
  const values = { email: formText(form, "email") };
  const parsed = signInSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  const { email, password, rememberMe } = parsed.data;

  const paused = await lockedMinutes(email);
  if (paused > 0) {
    return {
      values,
      error: `Too many wrong passwords. Logins for this email are paused for ${paused} more minute${paused === 1 ? "" : "s"}. Reset your password or use Google.`,
    };
  }

  const { data, error } = await auth().signIn.email({
    email,
    password,
    rememberMe: rememberMe === "on",
  });
  const e = error as AuthError;
  if (e) {
    const kind = classifyAuthError(e);
    if (kind === "email_not_verified") {
      await sendCode(email);
      await setPendingEmail(email);
      redirect(withRedirect("/auth/verify-email", form));
    }
    if (isRateLimited(e)) return { values, error: RATE_LIMITED };
    if (kind === "invalid_credentials" || e.status === 401) {
      const left = await recordFailure(email);
      return {
        values,
        fields: { password: "Wrong email or password." },
        error:
          left > 0
            ? `Wrong email or password. ${left} ${left === 1 ? "try" : "tries"} left before we pause logins for ${LOGIN_LOCK_MINUTES} minutes.`
            : `Wrong email or password. Logins for this email are paused for ${LOGIN_LOCK_MINUTES} minutes.`,
      };
    }
    logAuthError("sign-in", e);
    return { values, error: "We couldn't log you in. Try again in a moment." };
  }

  await clearFailures(email);
  const userId = (data as { user?: { id: string } } | null)?.user?.id;
  redirect(userId ? await nextPathAfterSignIn(userId, form.get("redirectTo")) : "/play");
}

// ------------------------------------------------------------------ sign up + verify

export async function signUpAction(_prev: AuthFormState, form: FormData): Promise<AuthFormState> {
  const values = { email: formText(form, "email") };
  const parsed = signUpSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  const { email, password } = parsed.data;

  const { error } = await auth().signUp.email({
    email,
    password,
    name: provisionalName(email),
  });
  const e = error as AuthError;
  if (e) {
    const kind = classifyAuthError(e);
    if (kind === "user_exists") {
      return { values, fields: { email: "That email is already registered." } };
    }
    if (isRateLimited(e)) return { values, error: RATE_LIMITED };
    if (kind === "weak_password") {
      return { values, fields: { password: e.message ?? "Choose another password." } };
    }
    logAuthError("sign-up", e);
    return { values, error: "We couldn't create the account. Try again in a moment." };
  }

  // With "verify email" required, Better Auth answers a sign-up for an existing address with a decoy success
  // (no account enumeration), so nothing is written for this id yet: consent is recorded once the code is verified.
  await sendCode(email);
  await setPendingEmail(email);
  redirect(withRedirect("/auth/verify-email", form));
}

export async function verifyEmailAction(
  _prev: AuthFormState,
  form: FormData,
): Promise<AuthFormState> {
  const email = await getPendingEmail();
  if (!email) return { error: "Your code expired. Sign up or log in again to get a new one." };
  const parsed = verifySchema.safeParse({ email, otp: formText(form, "otp") });
  if (!parsed.success) return { fields: fieldErrors(parsed.error) };

  const { data, error } = await emailOtp().verifyEmail(parsed.data);
  const e = error as AuthError;
  if (e) {
    const kind = classifyAuthError(e);
    if (kind === "otp_expired") return { fields: { otp: "That code expired. Send a new one." } };
    if (kind === "too_many_attempts") {
      return { fields: { otp: "Too many wrong codes. Send a new one." } };
    }
    if (isRateLimited(e)) return { error: RATE_LIMITED };
    return { fields: { otp: "That code isn't right. Check the email and try again." } };
  }

  await clearPendingEmail();
  const userId =
    (data as { user?: { id: string } } | null)?.user?.id ?? (await currentUser())?.id ?? null;
  // Every email account was created through the sign-up form, which requires accepting the terms.
  if (userId) await acceptTerms(userId);
  if (!userId) {
    // Verified, but this branch does not sign in after verification: log in with the password.
    redirect(`/auth/sign-in?verified=1`);
  }
  redirect(await nextPathAfterSignIn(userId, form.get("redirectTo")));
}

export async function resendCodeAction(): Promise<AuthFormState> {
  const email = await getPendingEmail();
  if (!email) return { error: "Your session expired. Sign up or log in again to get a new code." };
  const e = await sendCode(email);
  if (isRateLimited(e)) return { error: RATE_LIMITED };
  if (e) return { error: "We couldn't send a new code. Try again in a moment." };
  return { ok: true, notice: "New code sent." };
}

// ------------------------------------------------------------------ password reset

async function sendResetLink(email: string): Promise<AuthError> {
  const redirectTo = `${await requestOrigin()}/auth/reset-password`;
  const { error } = await auth().requestPasswordReset({ email, redirectTo });
  return error as AuthError;
}

export async function forgotPasswordAction(
  _prev: AuthFormState,
  form: FormData,
): Promise<AuthFormState> {
  const values = { email: formText(form, "email") };
  const parsed = forgotSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  const e = await sendResetLink(parsed.data.email);
  if (isRateLimited(e)) return { values, error: RATE_LIMITED };
  // Same answer whether or not the account exists: no account enumeration.
  await setPendingEmail(parsed.data.email);
  redirect("/auth/forgot-password/sent");
}

export async function resendResetAction(): Promise<AuthFormState> {
  const email = await getPendingEmail();
  if (!email) return { error: "Your session expired. Enter your email again." };
  const e = await sendResetLink(email);
  if (isRateLimited(e)) return { error: RATE_LIMITED };
  return { ok: true, notice: "We sent a new link." };
}

export async function resetPasswordAction(
  _prev: AuthFormState,
  form: FormData,
): Promise<AuthFormState> {
  const parsed = resetSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error) };
  const { token, password } = parsed.data;
  const { error } = await auth().resetPassword({ newPassword: password, token });
  const e = error as AuthError;
  if (e) {
    if (isRateLimited(e)) return { error: RATE_LIMITED };
    if (classifyAuthError(e) === "weak_password")
      return { fields: { password: e.message ?? "Choose another password." } };
    return { error: "This reset link expired or was already used. Request a new one." };
  }
  await clearPendingEmail();
  redirect("/auth/sign-in?reset=1");
}

// ------------------------------------------------------------------ social: finish + link

export async function finishProfileAction(
  _prev: AuthFormState,
  form: FormData,
): Promise<AuthFormState> {
  const user = await currentUser();
  if (!user) redirect("/auth/sign-in");
  const values = { username: formText(form, "username") };
  const parsed = finishSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };

  const profile = await getProfile(user.id);
  const needsTerms = !profile?.termsAcceptedAt;
  if (needsTerms && parsed.data.terms !== "on") {
    return { values, fields: { terms: "Please accept the Terms and Conditions to continue." } };
  }
  try {
    await completeProfile(user.id, parsed.data.username, needsTerms);
  } catch (err) {
    if (err instanceof UsernameTakenError) return { values, fields: { username: err.message } };
    throw err;
  }
  await auth().updateUser({ name: parsed.data.username });
  // A new manager builds the club next (Create your team); one who has a club goes where they were going.
  redirect((await getTeam(user.id)) ? safeRedirect(form.get("redirectTo")) : "/team/new");
}

/**
 * Step 1 of linking Google to an existing password account: log in with the password.
 * The browser then calls linkSocial (it must follow the provider redirect itself).
 */
export async function linkSignInAction(
  _prev: AuthFormState,
  form: FormData,
): Promise<AuthFormState> {
  const values = { email: formText(form, "email") };
  const parsed = signInSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  const { email, password } = parsed.data;
  const paused = await lockedMinutes(email);
  if (paused > 0) {
    return { values, error: `Logins for this email are paused for ${paused} more minutes.` };
  }
  const { error } = await auth().signIn.email({ email, password });
  const e = error as AuthError;
  if (e) {
    if (isRateLimited(e)) return { values, error: RATE_LIMITED };
    const left = await recordFailure(email);
    return {
      values,
      fields: { password: "Wrong password." },
      error:
        left > 0
          ? undefined
          : `Logins for this email are paused for ${LOGIN_LOCK_MINUTES} minutes.`,
    };
  }
  await clearFailures(email);
  return { ok: true, values };
}

// ------------------------------------------------------------------ sign out

export async function signOutAction(): Promise<void> {
  await auth().signOut();
  redirect("/");
}

/** "Change" / "Use a different account": sign out and start over at the login screen. */
export async function switchAccountAction(): Promise<void> {
  await auth().signOut();
  redirect("/auth/sign-in");
}

function withRedirect(path: string, form: FormData): string {
  const to = form.get("redirectTo");
  return typeof to === "string" && to
    ? `${path}?redirectTo=${encodeURIComponent(safeRedirect(to))}`
    : path;
}
