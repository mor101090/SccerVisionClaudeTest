import "server-only";

import { drizzle } from "drizzle-orm/postgres-js";
import postgres from "postgres";

import { serverEnv } from "@/env";

import * as schema from "./schema";

function createDb() {
  const url = serverEnv.DATABASE_URL;
  if (!url) throw new Error("DATABASE_URL is not set");
  // DATABASE_URL is Supabase's transaction pooler (Supavisor, port 6543): it does not support
  // prepared statements. Connections are cheap there; 10 lets the sidebar's parallel route prefetches
  // (each a handful of queries) run side by side instead of queuing behind each other.
  const client = postgres(url, { prepare: false, max: 10 });
  return drizzle({ client, schema, casing: "snake_case" });
}

let instance: ReturnType<typeof createDb> | null = null;

/** Lazily created so builds without a database (e.g. static pages) do not fail. */
export function db() {
  instance ??= createDb();
  return instance;
}

export { schema };
