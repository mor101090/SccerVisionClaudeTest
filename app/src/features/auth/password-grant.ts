/** True when this request is a Supabase password grant (`/token?grant_type=password`). */
export function isPasswordGrant(input: RequestInfo | URL): boolean {
  const raw = input instanceof Request ? input.url : input instanceof URL ? input.href : input;
  try {
    const url = new URL(raw, "http://localhost");
    return url.pathname.endsWith("/token") && url.searchParams.get("grant_type") === "password";
  } catch {
    return false;
  }
}

/**
 * Fetch for the browser Supabase client. Password grants are refused here: they would skip the
 * per-email lockout in the Server Actions. Session refresh, OAuth and Realtime still go through.
 * A script that calls the Auth API directly is counted by `hook_password_verification_attempt`.
 */
export function browserAuthFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  if (isPasswordGrant(input)) {
    return Promise.resolve(
      Response.json(
        {
          error: "password_grant_disabled",
          error_description: "Password sign-in goes through the server",
          msg: "Password sign-in goes through the server",
        },
        { status: 403 },
      ),
    );
  }
  return fetch(input, init);
}
