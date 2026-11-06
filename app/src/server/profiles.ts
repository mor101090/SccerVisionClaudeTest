import "server-only";

import { eq, sql } from "drizzle-orm";
import { cacheLife } from "next/cache";
import { cache } from "react";

import { db, schema } from "./db";
import { isUniqueViolation } from "./db/errors";

const { profiles } = schema;

export type Profile = typeof profiles.$inferSelect;

export async function getProfile(userId: string): Promise<Profile | null> {
  "use cache: private";
  cacheLife("minutes");
  const [row] = await db().select().from(profiles).where(eq(profiles.userId, userId));
  return row ?? null;
}

/**
 * getProfile once per request (layout, page and currentUser share one query). Renders only: a Server Action that
 * writes the profile and reads it back must call getProfile.
 */
export const profileForRequest = cache(getProfile);

/** True once the account has a username and accepted the terms (the "Almost there" step is done). */
export function isComplete(p: Profile | null): boolean {
  return Boolean(p?.username && p.termsAcceptedAt);
}

/** Records terms consent at email sign-up. Idempotent. */
export async function acceptTerms(userId: string): Promise<void> {
  await db()
    .insert(profiles)
    .values({ userId, termsAcceptedAt: new Date() })
    .onConflictDoUpdate({
      target: profiles.userId,
      set: { termsAcceptedAt: sql`coalesce(${profiles.termsAcceptedAt}, now())` },
    });
}

/** The manager name shown in the header and lobbies (Supabase's auth.users has no name column). */
export async function setDisplayName(userId: string, displayName: string): Promise<void> {
  await db()
    .insert(profiles)
    .values({ userId, displayName })
    .onConflictDoUpdate({ target: profiles.userId, set: { displayName } });
}

export class UsernameTakenError extends Error {
  constructor() {
    super("That username is taken. Try another one.");
  }
}

/** Sets the username (unique ignoring case) and, when given, terms consent. */
export async function completeProfile(
  userId: string,
  username: string,
  acceptedTerms: boolean,
): Promise<void> {
  const terms = acceptedTerms ? sql`coalesce(${profiles.termsAcceptedAt}, now())` : undefined;
  try {
    await db()
      .insert(profiles)
      .values({ userId, username, termsAcceptedAt: acceptedTerms ? new Date() : null })
      .onConflictDoUpdate({
        target: profiles.userId,
        set: terms ? { username, termsAcceptedAt: terms } : { username },
      });
  } catch (err) {
    if (isUniqueViolation(err)) throw new UsernameTakenError();
    throw err;
  }
}

export class InviterNotFoundError extends Error {
  constructor() {
    super("No manager with that username.");
  }
}

/** Step 1 of Create your team: username (unique), nationality and, once, who invited this manager. */
export async function saveManager(
  userId: string,
  input: { username: string; nationality: string; invitedBy?: string },
): Promise<void> {
  let invitedBy: string | null | undefined;
  if (input.invitedBy) {
    const [inviter] = await db()
      .select({ userId: profiles.userId })
      .from(profiles)
      .where(sql`lower(${profiles.username}) = lower(${input.invitedBy})`);
    if (!inviter || inviter.userId === userId) throw new InviterNotFoundError();
    invitedBy = inviter.userId;
  }
  try {
    await db()
      .insert(profiles)
      .values({ userId, username: input.username, nationality: input.nationality, invitedBy })
      .onConflictDoUpdate({
        target: profiles.userId,
        set: {
          username: input.username,
          nationality: input.nationality,
          // the inviter is recorded once and never changed afterwards
          ...(invitedBy
            ? { invitedBy: sql`coalesce(${profiles.invitedBy}, ${invitedBy}::uuid)` }
            : {}),
        },
      });
  } catch (err) {
    if (isUniqueViolation(err)) throw new UsernameTakenError();
    throw err;
  }
}
