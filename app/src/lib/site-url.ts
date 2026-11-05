export type SiteUrlEnv = {
  NEXT_PUBLIC_SITE_URL?: string;
  VERCEL_ENV?: "production" | "preview" | "development";
  VERCEL_URL?: string;
  VERCEL_PROJECT_PRODUCTION_URL?: string;
};

/**
 * Absolute base URL of the current deployment.
 *
 * Order: explicit NEXT_PUBLIC_SITE_URL → production domain on prod →
 * per-deployment URL on previews → localhost.
 * Auth callbacks and trusted origins depend on this being right per environment.
 */
export function getSiteUrl(env: SiteUrlEnv): string {
  if (env.NEXT_PUBLIC_SITE_URL) return stripSlash(env.NEXT_PUBLIC_SITE_URL);
  if (env.VERCEL_ENV === "production" && env.VERCEL_PROJECT_PRODUCTION_URL) {
    return `https://${env.VERCEL_PROJECT_PRODUCTION_URL}`;
  }
  if (env.VERCEL_URL) return `https://${env.VERCEL_URL}`;
  return "http://localhost:3000";
}

function stripSlash(url: string): string {
  return url.endsWith("/") ? url.slice(0, -1) : url;
}
