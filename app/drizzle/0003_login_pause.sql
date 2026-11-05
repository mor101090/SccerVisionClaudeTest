-- When a password pause begins. The app ignores a password session issued after this and leaves an
-- older session signed in. The Server Actions own the counter (app/src/server/login-throttle.ts).
-- Supabase's password-verification hook is a paid feature, so this migration does not install one.
-- Dev already had that function from an earlier draft of this file; drop it.

ALTER TABLE public.login_attempts
  ADD COLUMN IF NOT EXISTS locked_at timestamptz;--> statement-breakpoint

ALTER TABLE public.login_attempts
  DROP COLUMN IF EXISTS hook_counted_at;--> statement-breakpoint

DROP FUNCTION IF EXISTS public.hook_password_verification_attempt(jsonb);
