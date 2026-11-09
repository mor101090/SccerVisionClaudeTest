import { z } from "zod";

/**
 * Typed, validated environment.
 *
 * - Server vars are only readable on the server.
 * - Client vars must be prefixed NEXT_PUBLIC_ and are inlined at build time.
 * - Supabase vars (database, auth, storage) are optional so builds without them still work;
 *   the code that needs one throws a clear error when it is missing.
 */
const serverSchema = z.object({
  NODE_ENV: z.enum(["development", "test", "production"]).default("development"),
  // Vercel system vars: "production" | "preview" | "development"
  VERCEL_ENV: z.enum(["production", "preview", "development"]).optional(),
  VERCEL_URL: z.string().optional(),
  VERCEL_PROJECT_PRODUCTION_URL: z.string().optional(),
  // Supabase Postgres through the transaction pooler (Supavisor, port 6543).
  DATABASE_URL: z.url().optional(),
  // Supabase secret key (sb_secret_…): server-only admin calls (Storage, e2e seeding). Never sent to browsers.
  SUPABASE_SECRET_KEY: z.string().min(1).optional(),
  /** Soccer Vision team generator (squads for Create your team). */
  /** Stripe secret key for donations (/support). Unset: the donate button explains donations aren't open yet. */
  STRIPE_SECRET_KEY: z.string().min(1).optional(),
  SQUAD_API_URL: z.url().default("https://soccer-api.dev-spirit.com"),
});

const clientSchema = z.object({
  NEXT_PUBLIC_SITE_URL: z.url().optional(),
  // Supabase project URL and publishable key (sb_publishable_…): safe in the browser, RLS guards the data.
  NEXT_PUBLIC_SUPABASE_URL: z.url().optional(),
  NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY: z.string().min(1).optional(),
  // Google OAuth web client ID (public). Set: Google sign-in returns to this site (/auth/google) with an ID token, so
  // Google's screens name this site. Unset: Google sign-in goes through Supabase's redirect flow.
  NEXT_PUBLIC_GOOGLE_CLIENT_ID: z
    .string()
    .regex(/^[\w-]+\.apps\.googleusercontent\.com$/)
    .optional(),
  // Support address for "Contact Us" on the auth screens. Unset: the link is not shown.
  NEXT_PUBLIC_SUPPORT_EMAIL: z.email().optional(),
});

const isServer = typeof window === "undefined";

/**
 * Empty values count as unset. `vercel pull` writes Sensitive variables as "" (their values can't be read back), so
 * a CI build with the pulled env would otherwise fail on them; the deployment gets the real values at runtime.
 */
export function blankToUnset(values: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(Object.entries(values).map(([k, v]) => [k, v === "" ? undefined : v]));
}

function parse<T extends z.ZodType>(schema: T, values: Record<string, unknown>): z.infer<T> {
  const result = schema.safeParse(blankToUnset(values));
  if (!result.success) {
    console.error("Invalid environment variables:", z.flattenError(result.error).fieldErrors);
    throw new Error("Invalid environment variables");
  }
  return result.data;
}

// Client vars must be referenced explicitly so Next.js can inline them.
export const clientEnv = parse(clientSchema, {
  NEXT_PUBLIC_SITE_URL: process.env.NEXT_PUBLIC_SITE_URL,
  NEXT_PUBLIC_SUPABASE_URL: process.env.NEXT_PUBLIC_SUPABASE_URL,
  NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY: process.env.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY,
  NEXT_PUBLIC_GOOGLE_CLIENT_ID: process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID,
  NEXT_PUBLIC_SUPPORT_EMAIL: process.env.NEXT_PUBLIC_SUPPORT_EMAIL,
});

export const serverEnv = isServer
  ? parse(serverSchema, process.env)
  : (new Proxy({} as z.infer<typeof serverSchema>, {
      get() {
        throw new Error("serverEnv was accessed on the client");
      },
    }) as z.infer<typeof serverSchema>);
