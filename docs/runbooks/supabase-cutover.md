# Runbook: production cutover from Neon to Supabase

Moves soccer.dev-spirit.com from Neon to Supabase. Takes about 1 hour, including a maintenance window of about 15 minutes.
Run every step on **dev first** (Supabase dev project `mcrglsqpxyrbazwzrnzg`, Neon branch `dev`), then on prod.

## Before the day

1. **Create the prod Supabase project** (`soccer-vision-prod`, region eu-central-1). Repeat the dev setup:
   - Auth: Site URL, redirect allowlist, Confirm email ON, OTP length 6, OTP expiry 900 s, manual linking ON.
   - Custom SMTP (Resend, sender "Soccer-Vision by Devspirit" `no-reply@dev-spirit.com`), plus the two templates below.
   - Providers Google, Facebook and Discord, with the redirect `https://<prod-ref>.supabase.co/auth/v1/callback`
     added to each OAuth app.
   - Data API: remove `public` from the exposed schemas.
   - Realtime → Settings: turn off "Allow public access" (only private channels; lobbies use `lobby:<id>`).
2. **Email templates** (Auth → Email Templates). Paste the HTML and subject from `docs/email-templates/`
   (`confirm-signup.html`, `reset-password.html`, and under Security notifications, turned on: `identity-linked.html`,
   `identity-unlinked.html`); never leave Supabase's defaults (they link instead of showing the code, and mention
   Supabase):
   - Confirm signup: show the code `{{ .Token }}` (the site asks for the 6-digit code).
   - Reset password: link to `{{ .RedirectTo }}?token={{ .TokenHash }}`.
3. **Migrate the prod schema:** `DATABASE_URL_UNPOOLED=<prod direct url> yarn db:migrate` from `app/`. This creates the
   tables, the `replays` bucket and the lobby Realtime trigger. Password lockout stays in the app. Supabase's
   password-verification hook is a paid feature and is not part of this cutover.
4. **Set the Vercel Production env and GitHub secrets** (see "Environment" in CLAUDE.md), including
   `SUPABASE_PROD_DATABASE_URL_UNPOOLED`. The pull request build fails until that secret exists, because the
   production deploy migrates with it and does not run on the pull request. Keep the Neon values until step 3 of
   the cutover.
5. **Rehearse on dev:** delete the `yarn e2e:seed` accounts on Supabase dev, run the migration script against Neon `dev`, then `yarn e2e:seed` again. Then sign in with Google, play a
   saved match's replay, and play an online match.

## Cutover

1. **Announce maintenance.** Post a notice, then make sure no lobby is active: `select count(*) from lobbies where status in ('open','ready','playing')` on Neon `main`.
2. **Copy the data** from `app/`:
   ```bash
   NEON_DATABASE_URL=<neon main, unpooled> \
   SUPABASE_DB_URL=<supabase prod, direct :5432> \
   NEXT_PUBLIC_SUPABASE_URL=https://<prod-ref>.supabase.co SUPABASE_SECRET_KEY=<prod sb_secret_…> \
   NEON_S3_ENDPOINT=<neon storage endpoint> NEON_S3_ACCESS_KEY_ID=… NEON_S3_SECRET_ACCESS_KEY=… \
   yarn migrate:neon-to-supabase --replays
   ```
   The script prints row counts for both sides. It stops if a table has fewer rows on Supabase. It is safe to re-run.
3. **Switch Vercel Production env** to the Supabase values: `DATABASE_URL`, `NEXT_PUBLIC_SUPABASE_URL`,
   `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY`, `SUPABASE_SECRET_KEY`. Remove the `NEON_*` and `AWS_*` variables.
4. **Merge PR #6.** `vercel.yml` then migrates (a no-op, already done), builds, deploys and runs `production-check`.
5. **Smoke test on soccer.dev-spirit.com:**
   - Google sign-in
   - Facebook and Discord sign-in
   - Email sign-up with the code
   - Password reset
   - One replay plays
   - One online match between two browsers shows `sync realtime`
6. **Tell password users.** Either `SITE_URL=https://soccer.dev-spirit.com yarn migrate:neon-to-supabase --send-reset-emails`
   (needs SMTP), or rely on the hint on the login screen. Their old passwords can't be moved, so each one must
   set a new password once.

## Rollback (within 14 days)

Neon stays untouched. To go back:

1. Restore the Neon values in the Vercel Production env.
2. Redeploy the last pre-Supabase commit: `git revert` the merge and push to `main`.

Accounts created on Supabase after the cutover would be lost, so roll back only in the first hours.

## After 14 days

Delete the Neon project. Remove `@aws-sdk/client-s3` and this script (`app/tools/migrate-neon-to-supabase.ts`).
