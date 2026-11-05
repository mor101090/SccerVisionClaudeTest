# Migrate Soccer Vision from Neon to Supabase (Auth, Postgres, Storage, Realtime)

## Context

Neon Managed Auth has no Facebook provider, so the Facebook button says "coming soon". The decision is to move
the whole stack to Supabase. Supabase Auth has Google, Facebook and Discord built in.
User decisions:

- Providers at launch: **Google, Facebook, Discord**.
- Environments: **two Supabase projects**, `soccer-vision-prod` and `soccer-vision-dev`. All PR previews share dev, like today. The `own-db` label (a separate database per PR) is dropped.
- Region: eu-central-1 (Frankfurt), same as Neon.

Facts from the inventory that shape the plan:

- **Passwords can't be migrated.** Better Auth stores scrypt hashes, and Supabase can't import that format. Users who signed up with email and password must set a new password once. Google users re-link automatically by matching email.
- **Supabase `auth.users` has no `name` column.** `neon_auth.user.name` (set by `updateUser({name})`) moves to a new `profiles.display_name` column.
- **8 foreign keys point at `neon_auth.user`.** They move to `auth.users(id)`. User ids (uuid) stay the same, so no data rows change.
- **Storage is only 3 S3 calls** (`server/matches.ts` → `server/storage.ts`), and Supabase Storage is S3-compatible. The `media` bucket is unused, and no presigned URLs are created.
- **Realtime is the riskiest part.** The lockstep lobby relies on ordered, atomic delivery. The Supabase replacement is Broadcast-from-Database on a private channel, with gap detection that falls back to a full refetch.
- **`currentUser()` / `requireUser()` have about 31 call sites.** Keeping their return shape `{ id, name, email }` means those call sites don't change.

## What changed from this plan during implementation

- **Dev region:** the dev project (`mcrglsqpxyrbazwzrnzg`) is in eu-west-2 (London). Prod can still be eu-central-1.
- **Storage:** uses `supabase-js` with the secret key, not the S3 endpoint, so no S3 keys are needed. The bucket is created by migration `0001_storage_buckets`.
- **Realtime:** the trigger sends a "changed" signal, not row data, and the client re-reads `GET /api/lobbies/[id]`. This is consistent by construction, with no gap guard needed.
- **Old migrations:** deleted rather than archived (git history keeps them).
- **Password reset:** no `/auth/confirm` route. The reset email links to `/auth/reset-password?token=<token hash>`, and the token is redeemed on submit.
- **E2E accounts:** `yarn e2e:seed` creates them (admin API). The workflow runs it.

## Step 0: Worktree (first action after approval)

1. `git fetch origin`
2. `git worktree add ../soccer-vision-supabase -b feat/supabase-migration origin/main` (based on updated main, not the current `fix/production-deploy`)
3. `cd ../soccer-vision-supabase/app && yarn install`
4. Open a draft PR right away (rule: every branch gets its own PR). The Vercel preview points at Supabase **dev**.
5. **Supabase MCP + skills** (in the worktree root):
   - Run `claude mcp add --scope project --transport http supabase "https://mcp.supabase.com/mcp?project_ref=mcrglsqpxyrbazwzrnzg&features=docs%2Caccount%2Cdatabase%2Cdebugging%2Cdevelopment%2Cfunctions%2Cbranching%2Cfeedback%2Cstorage"`. This writes `.mcp.json` and replaces the Neon entry.
   - Run `npx skills add supabase/agent-skills`.
   - **You:** run `claude /mcp` in a regular terminal (not the VS Code extension), select `supabase`, then Authenticate. Claude can't run the OAuth flow from here.
   - From then on, Phase 1 settings, migrations, RLS and Realtime are applied and checked through the MCP where it supports them; the rest is done in the dashboard.

## Phase 1: Supabase projects and dashboard config (about 2 h, needs you in the dashboards)

1. **Dev project already exists:** ref `mcrglsqpxyrbazwzrnzg`. Confirm its region is eu-central-1. Create `soccer-vision-prod` before Phase 6.
2. **Branding: dev-spirit.com everywhere, app name "Soccer-Vision by Devspirit".**
   - **Deferred: all Pro features, including the custom domain.** Until then, OAuth callbacks and `NEXT_PUBLIC_SUPABASE_URL` use `https://mcrglsqpxyrbazwzrnzg.supabase.co`. Switching later means adding the new callback URL to each OAuth app and changing one env var.
   - **Auth domain (later).** Supabase Custom Domain `auth.soccer.dev-spirit.com` for prod and `auth-dev.soccer.dev-spirit.com` for dev.
     - Needs the Pro plan plus the Custom Domain add-on.
     - Add the CNAME and TXT records at the dev-spirit.com DNS host, then activate with `supabase domains activate`.
     - **Set this up before the providers**, so every OAuth callback is `https://auth.soccer.dev-spirit.com/auth/v1/callback`. Users then see dev-spirit.com, never `*.supabase.co`.
     - `NEXT_PUBLIC_SUPABASE_URL` uses the custom domain.
   - **Email.** Verify `dev-spirit.com` in Resend (SPF, DKIM and DMARC records at the DNS host). In Supabase custom SMTP:
     - Sender name: `Soccer-Vision by Devspirit`
     - Sender address: `no-reply@dev-spirit.com`
     - Rebrand every template (confirm, reset, magic link, email change, identity linked), using subject prefix "Soccer-Vision by Devspirit"
   - **OAuth apps** are all named `Soccer-Vision by Devspirit` and use the Soccer Vision logo (`logo2.png`).
     - Home page: `https://soccer.dev-spirit.com`
     - Privacy policy and terms: the site's `/terms` page
     - Authorized domain: `dev-spirit.com`
     - Google: OAuth consent screen app name, plus the authorized domain. Publish the app; verification may be needed because of the logo.
     - Meta: app display name, app domains, and the Facebook Login redirect.
     - Discord: application name and redirect.
3. **Auth settings** (both projects):
   - Site URL: `https://soccer.dev-spirit.com`
   - Redirect allowlist: `http://localhost:3000/**`, `http://localhost:3100/**`, Vercel preview wildcard
   - Email confirm ON, OTP length 6, OTP expiry 900 s (matches `policy.ts` "15 minutes")
   - Manual identity linking ON
4. **Custom SMTP (Resend, branding above).** The built-in SMTP sends only a few emails per hour, which is unusable. Two templates change their content:
   - Confirm signup: use `{{ .Token }}` (6-digit code, keeps `verify-form.tsx`)
   - Reset password: link to `{{ .SiteURL }}/auth/confirm?token_hash={{ .TokenHash }}&type=recovery`
5. **Providers.** Each uses the redirect `https://<ref>.supabase.co/auth/v1/callback` for now (`auth.soccer.dev-spirit.com` once the custom domain is added):
   - Google: add the new redirect URI to the existing Google OAuth client
   - Facebook: new Meta app with Facebook Login, scopes `email` and `public_profile`
   - Discord: new Discord application, scopes `identify` and `email`
6. **Storage:** create a private bucket `replays` and generate S3 access keys.
7. **Data API:** remove `public` from the exposed schemas. The server talks to Postgres directly, so nothing should reach our tables through the anon key.

## Phase 2: Database layer (about half a day)

1. **Driver.** Replace `drizzle-orm/neon-http` with `drizzle-orm/postgres-js` in `src/server/db/index.ts`:
   - `DATABASE_URL`: Supavisor transaction pooler on port 6543, with `prepare: false`
   - `DATABASE_URL_UNPOOLED`: direct connection on 5432, for migrations
2. **Schema** (`src/server/db/schema.ts`):
   - `pgSchema("neon_auth")` → `pgSchema("auth")`, table `users` with only `id`
   - Add `profiles.display_name text`
   - Keep `schemaFilter: ["public"]` in `drizzle.config.ts`
3. **Name joins.** `server/lobbies.ts:84-85` and the raw SQL in `server/realtime.ts` switch from `authUsers.name` to `profiles.display_name`.
4. **Fresh baseline migration.** The old migrations hard-code `neon_auth.user`, so they can't run on Supabase.
   - Move `drizzle/` to `drizzle-neon-archive/`
   - Run `yarn db:generate --name supabase_baseline`
   - Strip any emitted `CREATE TABLE "auth"."users"` (the same quirk as with neon_auth)
5. **Add RLS to the baseline:** `ENABLE ROW LEVEL SECURITY` on every public table, with no policies. This blocks API access. The server's `postgres` role bypasses RLS.
6. **Error unwrapping.** `src/server/db/errors.ts`: postgres-js puts `.code === "23505"` directly on the error, with no Neon `cause` wrapper.

## Phase 3: Auth (about 1.5–2 days; the biggest phase)

1. **Packages.**
   - Add `@supabase/ssr` and `@supabase/supabase-js`.
   - Add `src/server/supabase.ts` (server client wired to Next cookies, plus an admin client using the secret key) and `src/features/auth/supabase-client.ts` (browser client).
2. **`src/server/auth.ts`.**
   - `currentUser()` calls `supabase.auth.getUser()` and reads `profiles.display_name`, returning the same `{ id, name, email }` shape.
   - `requireUser()` and `UnauthorizedError` stay as they are.
   - Remove `createNeonAuth` and the `emailOtp()` casts.
3. **`src/proxy.ts`.** Use the Supabase session-refresh middleware pattern, keeping the same matcher and the `/auth/sign-in` redirect for member pages.
4. **`src/features/auth/actions.ts`** (Server Actions; every flow keeps its UX):

   | Today (Neon)                                             | Supabase                                                               |
   | -------------------------------------------------------- | ---------------------------------------------------------------------- |
   | `signIn.email`                                           | `signInWithPassword`                                                   |
   | `signUp.email` (decoy success for existing emails stays) | `signUp`                                                               |
   | `sendVerificationOtp`                                    | `resend({ type: "signup" })`                                           |
   | `verifyEmail`                                            | `verifyOtp({ email, token, type: "email" })`                           |
   | `requestPasswordReset`                                   | `resetPasswordForEmail(email, { redirectTo })`                         |
   | `resetPassword`                                          | `updateUser({ password })` after the recovery session                  |
   | `updateUser({ name })`                                   | update `profiles.display_name` (also in `features/team/actions.ts:63`) |
   | `signOut`                                                | `signOut`                                                              |

5. **New route** `src/app/auth/confirm/route.ts`: `verifyOtp({ token_hash, type })`, then redirect to `/auth/reset-password`.
6. **OAuth.**
   - `social-buttons.tsx`: `signInWithOAuth({ provider: "google" | "facebook" | "discord", options: { redirectTo: <origin>/auth/callback } })`.
   - Enable the Facebook button and add a Discord button. Its color goes in as a bound Figma variable, following the Figma color convention (no raw hex).
   - `/auth/callback/route.ts`: `exchangeCodeForSession(code)`, then `nextPathAfterSignIn()`.
7. **Account linking.**
   - `social-forms.tsx`: `linkSocial` → `linkIdentity`.
   - `/auth/finish/page.tsx`: `listAccounts()` → `getUserIdentities()` (`identity.provider`).
   - `/auth/error` and `/auth/link-account`: Supabase links identities with the same verified email automatically, so this becomes a fallback. Keep the page.
8. **Delete the browser proxy** `src/app/api/auth/[...path]`. The OAuth handshake goes straight to supabase.co.
9. **`classifyAuthError()`.** Rewrite it for Supabase `error.code` values (`invalid_credentials`, `user_already_exists`, `email_exists`, `otp_expired`, `weak_password`, `over_email_send_rate_limit`, `email_not_confirmed`) and update the unit test. `login-throttle.ts` stays as it is.
10. **Migrated password users.**
    - These accounts have no password, so sign-in returns `invalid_credentials`.
    - The sign-in form shows: "We upgraded our login. If you signed up with email, reset your password once."
    - Phase 6 also emails these users.

## Phase 4: Storage (about 1 h)

1. `src/server/storage.ts`:
   - Same S3 client, with the endpoint set to `https://<ref>.supabase.co/storage/v1/s3`.
   - Region from `AWS_REGION` (eu-central-1); keep `forcePathStyle: true`.
   - Remove the `media` entry (unused).
2. `src/env.ts`: keep the `AWS_*` names so no call site changes; only the values change.

## Phase 5: Realtime for lobbies (about 1 day; highest risk)

1. **Migration with an RLS policy.**
   - An `AFTER INSERT OR UPDATE` trigger on `lobbies` and `lobby_inputs` calls `realtime.broadcast_changes('lobby:' || lobby_id, ...)`.
   - An RLS policy on `realtime.messages` allows `SELECT` only when `auth.uid()` is the host or guest of that lobby. This is the same rule as today's SQL, so authorization stays at the database.
2. **Server.** Delete `src/server/realtime.ts` and `api/lobbies/[id]/live`. The client subscribes with its Supabase session, so there's no sealing step.
3. **`src/features/lobby/lobby-sync.ts`:**
   - `supabase.channel('lobby:<id>', { config: { private: true } }).on('broadcast', ...)`
   - Convert each row payload to a `LobbyLiveRow` and pass it to the existing `mergeLive()`
   - **Gap guard:** if `lobby_inputs.seq` jumps, or the channel reconnects, refetch `GET /api/lobbies/[id]` before applying more events. The lockstep never sees a watermark without its inputs.
   - Keep the 1500 ms polling fallback on subscribe error
   - Remove the token-renew timer (supabase-js refreshes the JWT)
4. `online-match.tsx` stays as it is.

## Phase 6: Data migration script and cutover runbook (about half a day to write, 1 h window)

Script `tools/migrate-neon-to-supabase.ts` (idempotent, `--dry-run`, run per environment, dev first):

1. **Users.** Insert into `auth.users` (same `id` and `email`; `email_confirmed_at` from `emailVerified`; `raw_user_meta_data.name`; `created_at`) and `auth.identities`:
   - `email` identity for every user
   - `google` identity built from `neon_auth.account`
   - No password hash
2. **Public data.** `pg_dump --data-only --schema=public` from Neon, then restore into Supabase with `session_replication_role=replica`. Reset sequences.
3. **Names.** Backfill `profiles.display_name` from `neon_auth.user.name`.
4. **Replays.** Copy `replays/*` from Neon S3 to Supabase S3, then compare the object counts.
5. **Reset emails.** Optional `--send-reset-emails` flag: `resetPasswordForEmail` for every user with a credential account.

**Cutover (prod, on PR merge):**

1. Announce a short maintenance window.
2. Put a Vercel maintenance flag on, or pause lobby creation.
3. Run the script against prod.
4. Swap the prod env vars in Vercel.
5. Merge to `main`, so `vercel.yml` deploys.
6. Run `production-check` and smoke-test.

**Rollback:** Neon stays untouched and read-only for 14 days. Revert the env vars and redeploy the previous commit.

## Phase 7: CI, config and docs (about half a day)

1. **`.github/workflows/e2e.yml`:**
   - Replace `neon env pull` with a `.env.local` built from GitHub secrets (`SUPABASE_DEV_*`, `DATABASE_URL*`, `AWS_*`)
   - Keep `yarn db:migrate`
   - New `yarn e2e:seed` uses the admin API to create `TEST_USER` and `GUEST_USER`, and deletes `SIGNUP_EMAIL` before each run. Today nothing seeds these users.
2. **`.github/workflows/vercel.yml`:** migrate prod using the `SUPABASE_PROD_DB_URL_UNPOOLED` secret instead of `neon connection-string main`. Remove `NEON_API_KEY` and `NEON_PROJECT_ID`.
3. **Packages.**
   - Remove `@neon/auth`, `@neon/realtime`, `@neondatabase/serverless`, `@neon/env`, `@neon/config` and the unused `@aws-sdk/s3-request-presigner`
   - Delete `app/neon.ts` and `.neon`
4. **Env.** `src/env.ts` replaces the `NEON_*` variables with `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` and `SUPABASE_SECRET_KEY`. Set them in Vercel for Preview (dev) and Production (prod).
5. **Agent tooling.** The Supabase MCP and skills were already added in Step 0. Remove the `.claude/skills/neon*` and `.agents/skills/neon*` folders and their `skills-lock.json` entries.
6. **Docs.** Update the Neon sections of `CLAUDE.md` and `AGENTS.md`: stack, environments table, Drizzle quirk, lobby sync, auth flow and error codes. Also fix two stale `AGENTS.md` lines (51, 70) and add a `CHANGELOG.md` entry.

## Verification

1. **Local, against Supabase dev:**
   - `yarn typecheck`, plus the unit tests (`auth-errors` test)
   - `yarn test:e2e`, including auth, lobby and matches specs
2. **On the PR preview, by hand:**
   - Email sign-up with the 6-digit code, then terms, then profile
   - Wrong password 5 times triggers the lockout
   - Forgot password, then the email link, then a new password
   - Google, Facebook and Discord sign-in, plus linking from the account page
   - Online lobby between two browsers: live updates arrive, and killing wifi falls back to polling and recovers
   - Save a match and play its replay back
3. **Migration dry run on dev:**
   - Row counts per table match between Neon and Supabase
   - A migrated Google user signs in and sees their teams and matches
   - A migrated password user gets the reset hint and can recover
4. **After the prod cutover:** `production-check` (`/`, `/home`, `/play`, `/auth/sign-in` return 200), plus one real Google sign-in and one replay load.

## Estimate

About **4–6 working days** total. Realtime (Phase 5) and auth (Phase 3) carry most of the risk. Every phase except Phase 1 runs on the worktree branch against Supabase dev, so it can't affect prod until cutover.
