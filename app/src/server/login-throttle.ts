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

/** The counter row, as it was before a password grant, so a hook write is not counted twice. */
export type AttemptSnapshot = {
  failures: number;
  windowStartedAt: string;
  lockedUntil: string | null;
};

export async function attemptSnapshot(email: string): Promise<AttemptSnapshot | null> {
  const [row] = await db()
    .select({
      failures: loginAttempts.failures,
      windowStartedAt: loginAttempts.windowStartedAt,
      lockedUntil: loginAttempts.lockedUntil,
    })
    .from(loginAttempts)
    .where(eq(loginAttempts.key, keyFor(email)));
  if (!row) return null;
  return {
    failures: row.failures,
    windowStartedAt: row.windowStartedAt.toISOString(),
    lockedUntil: row.lockedUntil?.toISOString() ?? null,
  };
}

function sameSnapshot(a: AttemptSnapshot | null, b: AttemptSnapshot | null): boolean {
  if (a === b) return true;
  if (!a || !b) return false;
  return (
    a.failures === b.failures &&
    a.windowStartedAt === b.windowStartedAt &&
    a.lockedUntil === b.lockedUntil
  );
}

/** Tries left from a counter the password hook already updated. */
function triesLeft(snapshot: AttemptSnapshot | null): number {
  return Math.max(0, LOGIN_MAX_FAILURES - (snapshot?.failures ?? 0));
}

/**
 * One failed password grant. The Auth hook records it for every grant, including ones that skip
 * these Server Actions. When the hook did not write (it is off, or the email has no account),
 * this records the failure itself.
 */
export async function countedFailure(
  email: string,
  before: AttemptSnapshot | null,
): Promise<number> {
  if ((await lockedMinutes(email)) > 0) return 0;
  const after = await attemptSnapshot(email);
  if (!sameSnapshot(before, after)) return triesLeft(after);
  return recordFailure(email);
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
