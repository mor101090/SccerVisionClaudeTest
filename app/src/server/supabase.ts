import "server-only";

import { createServerClient } from "@supabase/ssr";
import { createClient } from "@supabase/supabase-js";
import { cookies } from "next/headers";

import { clientEnv, serverEnv } from "@/env";

export function supabaseConfig() {
  const url = clientEnv.NEXT_PUBLIC_SUPABASE_URL;
  const key = clientEnv.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY;
  if (!url || !key) {
    throw new Error(
      "NEXT_PUBLIC_SUPABASE_URL and NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY must be set",
    );
  }
  return { url, key };
}

/**
 * Supabase client bound to this request's session cookies (Server Components, Server Actions, Route Handlers).
 * Server Components can't write cookies: refreshed tokens are written by proxy.ts instead, so a failed set is ignored.
 */
export async function supabaseServer() {
  const { url, key } = supabaseConfig();
  const store = await cookies();
  return createServerClient(url, key, {
    cookies: {
      getAll: () => store.getAll(),
      setAll(list) {
        try {
          for (const { name, value, options } of list) store.set(name, value, options);
        } catch {
          // Called from a Server Component: proxy.ts refreshes the session cookie.
        }
      },
    },
  });
}

/** Admin client (secret key). Server-only: user management for e2e seeding and the data migration. */
export function supabaseAdmin() {
  const { url } = supabaseConfig();
  const secret = serverEnv.SUPABASE_SECRET_KEY;
  if (!secret) throw new Error("SUPABASE_SECRET_KEY must be set");
  return createClient(url, secret, { auth: { autoRefreshToken: false, persistSession: false } });
}
