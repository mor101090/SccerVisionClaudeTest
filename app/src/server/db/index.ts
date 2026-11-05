import "server-only";

import { neon } from "@neondatabase/serverless";
import { drizzle } from "drizzle-orm/neon-http";

import { serverEnv } from "@/env";

import * as schema from "./schema";

function createDb() {
  const url = serverEnv.DATABASE_URL;
  if (!url) throw new Error("DATABASE_URL is not set");
  return drizzle({ client: neon(url), schema, casing: "snake_case" });
}

let instance: ReturnType<typeof createDb> | null = null;

/** Lazily created so builds without a database (e.g. static pages) do not fail. */
export function db() {
  instance ??= createDb();
  return instance;
}

export { schema };
