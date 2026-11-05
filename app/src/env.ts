import { z } from "zod";

/**
 * Typed, validated environment.
 *
 * - Server vars are only readable on the server.
 * - Client vars must be prefixed NEXT_PUBLIC_ and are inlined at build time.
 * - Neon vars (DATABASE_URL, auth, storage) are optional until phase 3 wires them;
 *   tighten them to required once the DB is live in every environment.
 */
const serverSchema = z.object({
  NODE_ENV: z.enum(["development", "test", "production"]).default("development"),
  // Vercel system vars: "production" | "preview" | "development"
  VERCEL_ENV: z.enum(["production", "preview", "development"]).optional(),
  VERCEL_URL: z.string().optional(),
  VERCEL_PROJECT_PRODUCTION_URL: z.string().optional(),
  DATABASE_URL: z.url().optional(),
  // Neon Auth (managed Better Auth). Pulled by `neon env pull` / set per Vercel environment.
  NEON_AUTH_BASE_URL: z.url().optional(),
  NEON_AUTH_COOKIE_SECRET: z.string().min(32).optional(),
  // Neon Object Storage (S3-compatible, AWS-standard names).
  AWS_ACCESS_KEY_ID: z.string().optional(),
  AWS_SECRET_ACCESS_KEY: z.string().optional(),
  AWS_ENDPOINT_URL_S3: z.url().optional(),
  AWS_REGION: z.string().optional(),
  // Neon Realtime (live SQL over WebSocket). The URL is sent to the browser by the seal endpoint.
  NEON_REALTIME_URL: z.url().optional(),
  NEON_REALTIME_SECRET: z.string().min(1).optional(),
  /** Soccer Vision team generator (squads for Create your team). */
  SQUAD_API_URL: z.url().default("https://soccer-api.dev-spirit.com"),
});

const clientSchema = z.object({
  NEXT_PUBLIC_SITE_URL: z.url().optional(),
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
});

export const serverEnv = isServer
  ? parse(serverSchema, process.env)
  : (new Proxy({} as z.infer<typeof serverSchema>, {
      get() {
        throw new Error("serverEnv was accessed on the client");
      },
    }) as z.infer<typeof serverSchema>);
