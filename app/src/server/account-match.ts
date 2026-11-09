import "server-only";

import { sql } from "drizzle-orm";

import { signUpMethod } from "@/features/auth/linking";

import { db } from "./db";

/** What the "You already have an account" screen shows: enough to recognise the account, nothing more. */
export type ExistingAccount = {
  username: string | null;
  clubName: string | null;
  /** How the account was created ("Facebook", "email and password"). */
  signedUpWith: string;
};

type Row = {
  username: string | null;
  club_name: string | null;
  identities: { provider: string; created_at: string | null; provider_id: string }[];
};

/**
 * The confirmed account a Google sign-in would be merged into, or null (new account, a returning Google user, or an
 * unconfirmed address). Call only with an email proven by a verified Google ID token (`verifyGoogleIdToken`): the
 * result describes that address's account to whoever holds it.
 */
export async function accountForGoogleSignIn(email: string): Promise<ExistingAccount | null> {
  const rows = (await db().execute(sql`
    select p.username,
           t.name as club_name,
           coalesce(json_agg(json_build_object(
             'provider', i.provider, 'created_at', i.created_at, 'provider_id', i.provider_id
           )) filter (where i.id is not null), '[]') as identities
    from auth.users u
    left join auth.identities i on i.user_id = u.id
    left join public.profiles p on p.user_id = u.id
    left join public.teams t on t.owner_id = u.id
    where lower(u.email) = ${email.toLowerCase()}
      and u.email_confirmed_at is not null
      and u.deleted_at is null
    group by u.id, p.username, t.name
    limit 1
  `)) as unknown as Row[];
  const row = rows[0];
  if (!row) return null;
  // Already signs in with Google (this Google account or another one): nothing to ask, Supabase decides.
  if (row.identities.some((i) => i.provider === "google")) return null;
  return {
    username: row.username,
    clubName: row.club_name,
    signedUpWith: signUpMethod(row.identities),
  };
}

/** The same summary for the signed-in user (after Supabase merged a Facebook/Discord sign-in). */
export async function accountSummary(
  userId: string,
  excludeIdentityId: string,
): Promise<ExistingAccount> {
  const rows = (await db().execute(sql`
    select p.username,
           t.name as club_name,
           coalesce(json_agg(json_build_object(
             'provider', i.provider, 'created_at', i.created_at, 'provider_id', i.provider_id
           )) filter (where i.id is not null and i.id::text <> ${excludeIdentityId}), '[]') as identities
    from auth.users u
    left join auth.identities i on i.user_id = u.id
    left join public.profiles p on p.user_id = u.id
    left join public.teams t on t.owner_id = u.id
    where u.id = ${userId}
    group by u.id, p.username, t.name
  `)) as unknown as Row[];
  const row = rows[0];
  return {
    username: row?.username ?? null,
    clubName: row?.club_name ?? null,
    signedUpWith: signUpMethod(row?.identities ?? []),
  };
}
