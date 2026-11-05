import "server-only";

import { createNeonAuth } from "@neon/auth/next/server";
import { cookies } from "next/headers";

import { serverEnv } from "@/env";

function createAuth() {
  const baseUrl = serverEnv.NEON_AUTH_BASE_URL;
  const secret = serverEnv.NEON_AUTH_COOKIE_SECRET;
  if (!baseUrl || !secret) {
    throw new Error("NEON_AUTH_BASE_URL and NEON_AUTH_COOKIE_SECRET must be set");
  }
  return createNeonAuth({ baseUrl, cookies: { secret } });
}

type Auth = ReturnType<typeof createAuth>;
let instance: Auth | null = null;

/** Lazily created so `next build` does not need auth env for static pages. */
export function auth(): Auth {
  instance ??= createAuth();
  return instance;
}

type AuthResult<T = unknown> = Promise<{
  data: T | null;
  error: { code?: string; status?: number; message?: string } | null;
}>;

/**
 * Email OTP endpoints (Managed plugin). The server proxy implements them (`email-otp/*` in the
 * SDK's endpoint table) but the SDK's types leave `emailOtp` as `unknown`, so type them here.
 */
export function emailOtp() {
  return (
    auth() as unknown as {
      emailOtp: {
        sendVerificationOtp(body: {
          email: string;
          type: "email-verification" | "sign-in" | "forget-password";
        }): AuthResult;
        verifyEmail(body: { email: string; otp: string }): AuthResult<{ user?: { id: string } }>;
      };
    }
  ).emailOtp;
}

/** The signed-in user, or null. */
export async function currentUser() {
  // Touch cookies() here first: during prerender this suspends in our code, not inside the
  // SDK's own try/catch (which would log a HANGING_PROMISE_REJECTION).
  await cookies();
  const { data } = await auth().getSession();
  return data?.user ?? null;
}

/** The signed-in user, or throws. Use in Route Handlers and Server Actions before any data access. */
export async function requireUser() {
  const user = await currentUser();
  if (!user) throw new UnauthorizedError();
  return user;
}

export class UnauthorizedError extends Error {
  constructor() {
    super("Unauthorized");
    this.name = "UnauthorizedError";
  }
}
