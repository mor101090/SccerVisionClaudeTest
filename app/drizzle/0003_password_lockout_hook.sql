-- Counts a failed password grant and enforces the pause for every password verification, including a
-- direct Auth API call that never hits the Server Actions. The key matches app/src/server/login-throttle.ts
-- (sha256 of "login:" + the normalized email). A wrong password gets Supabase's normal
-- invalid-credentials result, whether or not the address is registered. A correct password during a pause
-- is rejected with that same message and does not sign out a session that already exists.
-- Enable it in Authentication → Hooks → Password verification attempt.

ALTER TABLE public.login_attempts
  ADD COLUMN IF NOT EXISTS hook_counted_at timestamptz,
  ADD COLUMN IF NOT EXISTS locked_at timestamptz;--> statement-breakpoint

CREATE OR REPLACE FUNCTION public.hook_password_verification_attempt(event jsonb)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
  user_id uuid;
  user_email text;
  attempt_key text;
  prev_locked timestamptz;
BEGIN
  IF coalesce(event->>'user_id', '') = '' THEN
    RETURN jsonb_build_object('decision', 'continue');
  END IF;

  user_id := (event->>'user_id')::uuid;

  SELECT u.email INTO user_email
  FROM auth.users AS u
  WHERE u.id = user_id;

  IF user_email IS NULL OR btrim(user_email) = '' THEN
    RETURN jsonb_build_object('decision', 'continue');
  END IF;

  attempt_key := encode(
    extensions.digest(convert_to('login:' || lower(btrim(user_email)), 'UTF8'), 'sha256'),
    'hex'
  );

  SELECT la.locked_until INTO prev_locked
  FROM public.login_attempts AS la
  WHERE la.key = attempt_key;

  -- Wrong password: count it, then let Supabase return its normal invalid-credentials error.
  IF NOT coalesce((event->>'valid')::boolean, false) THEN
    INSERT INTO public.login_attempts AS la (key, failures, window_started_at, locked_until, locked_at, hook_counted_at)
    VALUES (attempt_key, 1, now(), NULL, NULL, clock_timestamp())
    ON CONFLICT (key) DO UPDATE SET
      failures = CASE
        WHEN la.window_started_at < now() - interval '15 minutes' THEN 1
        ELSE la.failures + 1
      END,
      window_started_at = CASE
        WHEN la.window_started_at < now() - interval '15 minutes' THEN now()
        ELSE la.window_started_at
      END,
      locked_until = CASE
        WHEN (
          CASE
            WHEN la.window_started_at < now() - interval '15 minutes' THEN 1
            ELSE la.failures + 1
          END
        ) >= 5 THEN now() + interval '15 minutes'
        ELSE NULL
      END,
      locked_at = CASE
        WHEN (
          CASE
            WHEN la.window_started_at < now() - interval '15 minutes' THEN 1
            ELSE la.failures + 1
          END
        ) >= 5 THEN coalesce(
          CASE WHEN la.locked_until > now() THEN la.locked_at ELSE NULL END,
          clock_timestamp()
        )
        ELSE NULL
      END,
      hook_counted_at = clock_timestamp();
    RETURN jsonb_build_object('decision', 'continue');
  END IF;

  -- Correct password during a pause: no new session, and no session that already exists is signed out.
  IF prev_locked IS NOT NULL AND prev_locked > now() THEN
    RETURN jsonb_build_object(
      'decision', 'reject',
      'message', 'Invalid login credentials',
      'should_logout_user', false
    );
  END IF;

  DELETE FROM public.login_attempts WHERE key = attempt_key;
  RETURN jsonb_build_object('decision', 'continue');
END;
$$;--> statement-breakpoint

REVOKE ALL ON FUNCTION public.hook_password_verification_attempt(jsonb) FROM PUBLIC, anon, authenticated;--> statement-breakpoint
GRANT EXECUTE ON FUNCTION public.hook_password_verification_attempt(jsonb) TO supabase_auth_admin;
