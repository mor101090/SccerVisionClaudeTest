import "server-only";

import { createHash } from "node:crypto";

import { eq, sql } from "drizzle-orm";

import { LOGIN_LOCK_MINUTES, LOGIN_MAX_FAILURES } from "@/features/auth/policy";

import { db, schema } from "./db";

const { loginAttempts } = schema;
const WINDOW = sql.raw(`interval '${LOGIN_LOCK_MINUTES} minutes'`);

/**
 * Never store the address: a salted-by-purpose hash of the normalized email is the key.
 * `drizzle/0003_password_lockout_hook.sql` hashes the same string. Keep the two in step.
 */
function keyFor(email: string): string {
  return createHash("sha256").update(`login:${email.trim().toLowerCase()}`).digest("hex");
}

/** Minutes until password login opens again for this email, or 0 when it is open. */
export async function lockedMinutes(email: string): Promise<number> {
  const [row] = await db()
    .select({ lockedUntil: loginAttempts.lockedUntil })
    .from(loginAttempts)
    .where(eq(loginAttempts.key, keyFor(email)));
  const until = row?.lockedUntil?.getTime() ?? 0;
  const ms = until - Date.now();
  return ms > 0 ? Math.ceil(ms / 60_000) : 0;
}

/**
 * Counts one failed password login (atomically). A window older than LOGIN_LOCK_MINUTES restarts
 * at 1. Reaching LOGIN_MAX_FAILURES pauses password login for LOGIN_LOCK_MINUTES.
 * Returns how many tries are left before the pause (0 = paused now).
 */
export async function recordFailure(email: string): Promise<number> {
  const stale = sql`${loginAttempts.windowStartedAt} < now() - ${WINDOW}`;
  const failures = sql`case when ${stale} then 1 else ${loginAttempts.failures} + 1 end`;
  const [row] = await db()
    .insert(loginAttempts)
    .values({ key: keyFor(email), failures: 1 })
    .onConflictDoUpdate({
      target: loginAttempts.key,
      set: {
        failures,
        windowStartedAt: sql`case when ${stale} then now() else ${loginAttempts.windowStartedAt} end`,
        lockedUntil: sql`case when ${failures} >= ${LOGIN_MAX_FAILURES} then now() + ${WINDOW} else null end`,
      },
    })
    .returning({ failures: loginAttempts.failures });
  return Math.max(0, LOGIN_MAX_FAILURES - (row?.failures ?? 1));
}

/** A successful login clears the counter. */
export async function clearFailures(email: string): Promise<void> {
  await db()
    .delete(loginAttempts)
    .where(eq(loginAttempts.key, keyFor(email)));
}
