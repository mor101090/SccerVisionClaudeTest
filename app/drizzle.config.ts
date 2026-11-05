import { config } from "dotenv";
import { defineConfig } from "drizzle-kit";

config({ path: ".env.local", quiet: true });
config({ quiet: true });

const url = process.env.DATABASE_URL_UNPOOLED ?? process.env.DATABASE_URL;
if (!url) throw new Error("DATABASE_URL_UNPOOLED or DATABASE_URL is required for drizzle-kit");

export default defineConfig({
  schema: "./src/server/db/schema.ts",
  out: "./drizzle",
  dialect: "postgresql",
  // `auth` is owned by Supabase Auth (and `storage`/`realtime` by Supabase): never generate migrations for them.
  schemaFilter: ["public"],
  dbCredentials: { url },
  strict: true,
  verbose: true,
});
