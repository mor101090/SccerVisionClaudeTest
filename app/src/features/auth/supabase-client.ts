"use client";

import { createBrowserClient } from "@supabase/ssr";
import type { SupabaseClient } from "@supabase/supabase-js";

import { clientEnv } from "@/env";

let client: SupabaseClient | null = null;

/** Browser Supabase client (session in cookies, shared with the server). Used for OAuth and Realtime. */
export function supabaseBrowser(): SupabaseClient {
  const url = clientEnv.NEXT_PUBLIC_SUPABASE_URL;
  const key = clientEnv.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY;
  if (!url || !key) throw new Error("Supabase is not configured");
  client ??= createBrowserClient(url, key);
  return client;
}
