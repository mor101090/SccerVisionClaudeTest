"use server";

import { redirect } from "next/navigation";

import { formText } from "@/lib/form";
import { currentUser } from "@/server/auth";
import {
  clearPendingEmail,
  clearRecoveryUser,
  getPendingEmail,
  getRecoveryUser,
  nextPathAfterSignIn,
  provisionalName,
  requestOrigin,
  setPendingEmail,
  setRecoveryUser,
} from "@/server/auth-flow";
import { clearFailures, lockedMinutes, recordFailure } from "@/server/login-throttle";
import {
  acceptTerms,
  completeProfile,
  getProfile,
  setDisplayName,
  UsernameTakenError,
} from "@/server/profiles";
import { supabaseServer } from "@/server/supabase";
import { getTeam } from "@/server/teams";

import { classifyAuthError, type AuthError } from "./auth-errors";
import {
  fieldErrors,
  finishSchema,
  forgotSchema,
  resetSchema,
  safeRedirect,
  signInSchema,
  signUpSchema,
  verifySchema,
} from "./policy";
import { isRecoverySession } from "./recovery-session";

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
/** Same answer for a wrong password, an unknown address, and a pause, so the pause is not an account oracle. */
const WRONG_PASSWORD =
  "Wrong email or password. We upgraded our login: if you signed up with email before, reset your password once.";

/** Unexpected auth failures: status and code only, never user input. */
function logAuthError(step: string, e: AuthError): void {
  console.error(`[auth] ${step} failed`, { status: e?.status, code: e?.code });
}

function isRateLimited(e: AuthError): boolean {
  return classifyAuthError(e) === "rate_limited";
}

/** Sends the 6-digit sign-up code again (the "Confirm signup" email template shows {{ .Token }}). */
async function sendCode(email: string): Promise<AuthError> {
  const supabase = await supabaseServer();
  const { error } = await supabase.auth.resend({ type: "signup", email });
  return error;
}

// ------------------------------------------------------------------ sign in

export async function signInAction(_prev: AuthFormState, form: FormData): Promise<AuthFormState> {
  const values = { email: formText(form, "email") };
  const parsed = signInSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  const { email, password } = parsed.data;

  const paused = await lockedMinutes(email);
  if (paused > 0) {
    return {
      values,
      fields: { password: "Wrong email or password." },
      error: WRONG_PASSWORD,
    };
  }

  const supabase = await supabaseServer();
  const { data, error } = await supabase.auth.signInWithPassword({ email, password });
  const e: AuthError = error;
  if (e) {
    const kind = classifyAuthError(e);
    if (kind === "email_not_verified") {
      await sendCode(email);
      await setPendingEmail(email);
      redirect(withRedirect("/auth/verify-email", form));
    }
    if (isRateLimited(e)) return { values, error: RATE_LIMITED };
    const pausedNow = await lockedMinutes(email);
    if (pausedNow > 0 || kind === "invalid_credentials") {
      if (kind === "invalid_credentials") await recordFailure(email);
      return {
        values,
        fields: { password: "Wrong email or password." },
        error: WRONG_PASSWORD,
      };
    }
    logAuthError("sign-in", e);
    return { values, error: "We couldn't log you in. Try again in a moment." };
  }

  await clearFailures(email);
  const userId = data.user?.id;
  redirect(userId ? await nextPathAfterSignIn(userId, form.get("redirectTo")) : "/play");
}

// ------------------------------------------------------------------ sign up + verify

export async function signUpAction(_prev: AuthFormState, form: FormData): Promise<AuthFormState> {
  const values = { email: formText(form, "email") };
  const parsed = signUpSchema.safeParse(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  const { email, password } = parsed.data;

  const supabase = await supabaseServer();
  const { error } = await supabase.auth.signUp({
    email,
    password,
    options: {
      data: { name: provisionalName(email) },
      emailRedirectTo: `${await requestOrigin()}/auth/sign-in?verified=1`,
    },
  });
  const e: AuthError = error;
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

  // signUp has already emailed the code. With "Confirm email" on, Supabase answers a sign-up for an existing address
  // with a decoy success (no account enumeration), so nothing is written here: consent is recorded once the code is
  // verified.
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

  const supabase = await supabaseServer();
  const { data, error } = await supabase.auth.verifyOtp({
    email: parsed.data.email,
    token: parsed.data.otp,
    type: "email",
  });
  const e: AuthError = error;
  if (e) {
    if (isRateLimited(e)) return { error: RATE_LIMITED };
    return {
      fields: { otp: "That code isn't right or has expired. Check the email or send a new one." },
    };
  }

  await clearPendingEmail();
  const userId = data.user?.id ?? null;
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

/** The "Reset password" email template links to {{ .RedirectTo }}?token={{ .TokenHash }}. */
async function sendResetLink(email: string): Promise<AuthError> {
  const redirectTo = `${await requestOrigin()}/auth/reset-password`;
  const supabase = await supabaseServer();
  const { error } = await supabase.auth.resetPasswordForEmail(email, { redirectTo });
  return error;
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
  const supabase = await supabaseServer();
  // The link's token is redeemed only here, on submit, so mail scanners that open links can't use it up.
  // A retry after a rejected password keeps that recovery session — not whatever session the browser already had.
  const { data: verified, error: verifyError } = await supabase.auth.verifyOtp({
    token_hash: token,
    type: "recovery",
  });
  if (!verifyError) {
    const userId = verified.user?.id;
    const { data } = await supabase.auth.getClaims();
    if (!userId || data?.claims?.sub !== userId || !isRecoverySession(data?.claims)) {
      return { error: "This reset link expired or was already used. Request a new one." };
    }
    await setRecoveryUser(userId);
  } else if (isRateLimited(verifyError)) {
    return { error: RATE_LIMITED };
  } else if (!(await canRetryRecovery(supabase))) {
    await clearRecoveryUser();
    return { error: "This reset link expired or was already used. Request a new one." };
  }
  const { error } = await supabase.auth.updateUser({ password });
  const e: AuthError = error;
  if (e) {
    if (isRateLimited(e)) return { error: RATE_LIMITED };
    const kind = classifyAuthError(e);
    if (kind === "weak_password")
      return { fields: { password: e.message ?? "Choose another password." } };
    if (kind === "same_password")
      return { fields: { password: "Choose a password you haven't used here before." } };
    logAuthError("reset-password", e);
    return { error: "We couldn't save the new password. Try again in a moment." };
  }
  await supabase.auth.signOut();
  await clearPendingEmail();
  await clearRecoveryUser();
  redirect("/auth/sign-in?reset=1");
}

/** The token was already redeemed in this browser, and the session it created is still a recovery session. */
async function canRetryRecovery(
  supabase: Awaited<ReturnType<typeof supabaseServer>>,
): Promise<boolean> {
  const marked = await getRecoveryUser();
  if (!marked) return false;
  const { data } = await supabase.auth.getClaims();
  const sub = data?.claims?.sub;
  if (typeof sub !== "string" || sub !== marked) return false;
  return isRecoverySession(data?.claims);
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
  await setDisplayName(user.id, parsed.data.username);
  // A new manager builds the club next (Create your team); one who has a club goes where they were going.
  redirect((await getTeam(user.id)) ? safeRedirect(form.get("redirectTo")) : "/team/new");
}

/**
 * Step 1 of linking Google to an existing password account: log in with the password.
 * The browser then calls linkIdentity (it must follow the provider redirect itself).
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
  if (paused > 0) return { values, fields: { password: "Wrong password." } };
  const supabase = await supabaseServer();
  const { error } = await supabase.auth.signInWithPassword({ email, password });
  const e: AuthError = error;
  if (e) {
    if (isRateLimited(e)) return { values, error: RATE_LIMITED };
    const pausedNow = await lockedMinutes(email);
    if (!(pausedNow > 0)) await recordFailure(email);
    return { values, fields: { password: "Wrong password." } };
  }
  await clearFailures(email);
  return { ok: true, values };
}

// ------------------------------------------------------------------ sign out

export async function signOutAction(): Promise<void> {
  await (await supabaseServer()).auth.signOut();
  redirect("/");
}

/** "Change" / "Use a different account": sign out and start over at the login screen. */
export async function switchAccountAction(): Promise<void> {
  await (await supabaseServer()).auth.signOut();
  redirect("/auth/sign-in");
}

function withRedirect(path: string, form: FormData): string {
  const to = form.get("redirectTo");
  return typeof to === "string" && to
    ? `${path}?redirectTo=${encodeURIComponent(safeRedirect(to))}`
    : path;
}
