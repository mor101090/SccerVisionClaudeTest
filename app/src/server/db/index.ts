import "server-only";

import { drizzle } from "drizzle-orm/postgres-js";
import postgres from "postgres";

import { serverEnv } from "@/env";

import * as schema from "./schema";

function createDb() {
  const url = serverEnv.DATABASE_URL;
  if (!url) throw new Error("DATABASE_URL is not set");
  // DATABASE_URL is Supabase's transaction pooler (Supavisor, port 6543): it does not support
  // prepared statements, and each serverless instance needs only a few connections.
  const client = postgres(url, { prepare: false, max: 5 });
  return drizzle({ client, schema, casing: "snake_case" });
}

let instance: ReturnType<typeof createDb> | null = null;

/** Lazily created so builds without a database (e.g. static pages) do not fail. */
export function db() {
  instance ??= createDb();
  return instance;
}

export { schema };
