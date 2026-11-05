import "server-only";

import { eq } from "drizzle-orm";

import { db, schema } from "./db";

const { passwordSessions } = schema;

/** Remember a password session the Server Action just created. */
export async function allowPasswordSession(sessionId: string, userId: string): Promise<void> {
  await db().insert(passwordSessions).values({ sessionId, userId }).onConflictDoNothing();
}

/** True when this password session was created by the app, not by a direct Auth API call. */
export async function isAllowedPasswordSession(sessionId: string): Promise<boolean> {
  const [row] = await db()
    .select({ sessionId: passwordSessions.sessionId })
    .from(passwordSessions)
    .where(eq(passwordSessions.sessionId, sessionId));
  return Boolean(row);
}

export async function forgetPasswordSession(sessionId: string): Promise<void> {
  await db().delete(passwordSessions).where(eq(passwordSessions.sessionId, sessionId));
}
