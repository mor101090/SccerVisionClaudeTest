-- Per-email password lockout for every password grant, including ones that skip the Server Actions.
-- The publishable key can call the Auth API directly. This hook is what counts those attempts.
-- Same key and same window as app/src/server/login-throttle.ts (sha256 of "login:" + normalized email,
-- 5 failures / 15 minutes). Enable it in Authentication → Hooks → Password verification attempt.
-- Constants live in app/src/features/auth/policy.ts (LOGIN_MAX_FAILURES, LOGIN_LOCK_MINUTES).

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
  prev_failures integer;
  prev_window timestamptz;
  prev_locked timestamptz;
  stale boolean;
  next_failures integer;
  max_failures integer := 5;
  lock_for interval := interval '15 minutes';
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

  SELECT la.failures, la.window_started_at, la.locked_until
    INTO prev_failures, prev_window, prev_locked
  FROM public.login_attempts AS la
  WHERE la.key = attempt_key;

  -- Already paused: reject even a correct password. Do not extend the window.
  IF prev_locked IS NOT NULL AND prev_locked > now() THEN
    RETURN jsonb_build_object(
      'decision', 'reject',
      'message', 'Logins for this email are paused.',
      'should_logout_user', false
    );
  END IF;

  IF coalesce((event->>'valid')::boolean, false) THEN
    DELETE FROM public.login_attempts WHERE key = attempt_key;
    RETURN jsonb_build_object('decision', 'continue');
  END IF;

  stale := prev_window IS NULL OR prev_window < now() - lock_for;
  next_failures := CASE WHEN stale THEN 1 ELSE coalesce(prev_failures, 0) + 1 END;

  INSERT INTO public.login_attempts AS la (key, failures, window_started_at, locked_until)
  VALUES (
    attempt_key,
    next_failures,
    now(),
    CASE WHEN next_failures >= max_failures THEN now() + lock_for ELSE NULL END
  )
  ON CONFLICT (key) DO UPDATE SET
    failures = EXCLUDED.failures,
    window_started_at = CASE WHEN stale THEN now() ELSE la.window_started_at END,
    locked_until = EXCLUDED.locked_until;

  RETURN jsonb_build_object('decision', 'continue');
END;
$$;--> statement-breakpoint

REVOKE ALL ON FUNCTION public.hook_password_verification_attempt(jsonb) FROM PUBLIC, anon, authenticated;--> statement-breakpoint
GRANT EXECUTE ON FUNCTION public.hook_password_verification_attempt(jsonb) TO supabase_auth_admin;
