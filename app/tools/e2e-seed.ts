/* eslint-disable no-console -- CLI script: progress goes to stdout. */
/**
 * Prepares the e2e accounts on the Supabase dev project. Idempotent; the daily e2e workflow runs it before Playwright.
 * - TEST_USER, GUEST_USER: confirmed email, known password, finished profile (username + terms), so sign-in lands on /play.
 * - SIGNUP_EMAIL: exists and is confirmed, so the sign-up test gets the decoy verify screen and no email is sent.
 *
 *   yarn e2e:seed   (reads NEXT_PUBLIC_SUPABASE_URL, SUPABASE_SECRET_KEY, DATABASE_URL from .env.local)
 *
 * Never run against production: the passwords are in the repo (tests/e2e/fixtures.ts).
 */
import { randomBytes } from "node:crypto";

import { createClient } from "@supabase/supabase-js";
import postgres from "postgres";

import { GUEST_USER, SIGNUP_EMAIL, TEST_USER } from "../tests/e2e/fixtures.ts";

const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
const secret = process.env.SUPABASE_SECRET_KEY;
const dbUrl = process.env.DATABASE_URL;
if (!url || !secret || !dbUrl) {
  throw new Error("NEXT_PUBLIC_SUPABASE_URL, SUPABASE_SECRET_KEY and DATABASE_URL are required");
}
if (process.env.VERCEL_ENV === "production")
  throw new Error("Refusing to seed test accounts on production");

const admin = createClient(url, secret, { auth: { persistSession: false } }).auth.admin;

async function findByEmail(email: string) {
  for (let page = 1; ; page++) {
    const { data, error } = await admin.listUsers({ page, perPage: 1000 });
    if (error) throw error;
    const user = data.users.find((u) => u.email?.toLowerCase() === email.toLowerCase());
    if (user || data.users.length < 1000) return user ?? null;
  }
}

/** Creates the account or resets it to a confirmed email with this password. Returns its id. */
async function upsertAccount(email: string, password: string, name: string): Promise<string> {
  const existing = await findByEmail(email);
  if (existing) {
    const { error } = await admin.updateUserById(existing.id, { password, email_confirm: true });
    if (error) throw error;
    return existing.id;
  }
  const { data, error } = await admin.createUser({
    email,
    password,
    email_confirm: true,
    user_metadata: { name },
  });
  if (error) throw error;
  return data.user.id;
}

const sql = postgres(dbUrl, { prepare: false, max: 1 });
try {
  for (const account of [TEST_USER, GUEST_USER]) {
    const id = await upsertAccount(account.email, account.password, account.name);
    await sql`
      insert into public.profiles (user_id, username, display_name, terms_accepted_at)
      values (${id}, ${account.username}, ${account.username}, now())
      on conflict (user_id) do update
        set username = excluded.username,
            display_name = excluded.display_name,
            terms_accepted_at = coalesce(public.profiles.terms_accepted_at, now())`;
    console.log(`ready: ${account.username}`);
  }
  await upsertAccount(SIGNUP_EMAIL, randomBytes(18).toString("base64url"), "e2e-signup");
  console.log("ready: sign-up address");
} finally {
  await sql.end();
}
