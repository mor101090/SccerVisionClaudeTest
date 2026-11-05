import "server-only";

import { cookies, headers } from "next/headers";

import { serverEnv } from "@/env";
import { safeRedirect } from "@/features/auth/policy";

import { getProfile, isComplete } from "./profiles";

/**
 * The address a verification code or reset link was sent to, kept in a short-lived httpOnly
 * cookie so it never appears in URLs (history, logs, referrers).
 */
const PENDING_EMAIL = "sv_pending_email";

export async function setPendingEmail(email: string): Promise<void> {
  (await cookies()).set(PENDING_EMAIL, email, {
    httpOnly: true,
    sameSite: "lax",
    secure: serverEnv.NODE_ENV === "production",
    path: "/auth",
    maxAge: 30 * 60,
  });
}

export async function getPendingEmail(): Promise<string | null> {
  return (await cookies()).get(PENDING_EMAIL)?.value ?? null;
}

export async function clearPendingEmail(): Promise<void> {
  (await cookies()).delete({ name: PENDING_EMAIL, path: "/auth" });
}

/** Origin of this request (the deployment the user is on). Neon Auth checks it against trusted domains. */
export async function requestOrigin(): Promise<string> {
  const h = await headers();
  const host = h.get("x-forwarded-host") ?? h.get("host") ?? "localhost:3000";
  const proto =
    h.get("x-forwarded-proto") ??
    (host.startsWith("localhost") || host.startsWith("127.") ? "http" : "https");
  return `${proto}://${host}`;
}

/** Where a freshly signed-in user goes: the "Almost there" step until the profile is complete. */
export async function nextPathAfterSignIn(userId: string, redirectTo: unknown): Promise<string> {
  const target = safeRedirect(redirectTo);
  if (isComplete(await getProfile(userId))) return target;
  return `/auth/finish?redirectTo=${encodeURIComponent(target)}`;
}

/** Initial display name until the user picks a username: the email's local part. */
export function provisionalName(email: string): string {
  return (email.split("@")[0] ?? "Manager").slice(0, 40) || "Manager";
}
