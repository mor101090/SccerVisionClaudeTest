-- While an email is paused, a correct password does not get a session, including a direct call to the
-- Auth API. A wrong password is left to Supabase (its normal invalid-credentials error), so the pause
-- is not a signal that the address is registered. The Server Actions own the counter
-- (app/src/server/login-throttle.ts, same email hash): this hook only reads it.
-- Enable it in Authentication → Hooks → Password verification attempt.

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
  -- Wrong password: same response whether or not the address is registered.
  IF coalesce(event->>'user_id', '') = '' OR NOT coalesce((event->>'valid')::boolean, false) THEN
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

  -- Correct password during a pause: no session. The message matches a wrong password.
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
