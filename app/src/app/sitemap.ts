import type { MetadataRoute } from "next";

import { clientEnv } from "@/env";

const SITE = clientEnv.NEXT_PUBLIC_SITE_URL ?? "https://soccer.dev-spirit.com";

/** The pages a visitor can open without an account. */
export default function sitemap(): MetadataRoute.Sitemap {
  return [
    "/home",
    "/classic/index.html",
    "/play",
    "/rules",
    "/terms",
    "/auth/sign-up",
    "/auth/sign-in",
  ].map((path) => ({ url: `${SITE}${path}` }));
}
