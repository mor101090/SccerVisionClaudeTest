-- While an email is paused, a correct password does not clear the pause. Wrong passwords are left to
-- Supabase's normal invalid-credentials response, for a registered address and an unknown one alike,
-- so the pause is not an account oracle. The Server Actions own the counter
-- (app/src/server/login-throttle.ts). This hook only reads it.
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
  -- A wrong password, or a call with no user, gets Supabase's normal result.
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

  -- Correct password during a pause: do not clear the counter. The app refuses the session.
  IF prev_locked IS NOT NULL AND prev_locked > now() THEN
    RETURN jsonb_build_object('decision', 'continue');
  END IF;

  DELETE FROM public.login_attempts WHERE key = attempt_key;
  RETURN jsonb_build_object('decision', 'continue');
END;
$$;--> statement-breakpoint

REVOKE ALL ON FUNCTION public.hook_password_verification_attempt(jsonb) FROM PUBLIC, anon, authenticated;--> statement-breakpoint
GRANT EXECUTE ON FUNCTION public.hook_password_verification_attempt(jsonb) TO supabase_auth_admin;
