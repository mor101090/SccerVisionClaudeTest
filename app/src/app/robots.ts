import type { MetadataRoute } from "next";

import { clientEnv } from "@/env";

const SITE = clientEnv.NEXT_PUBLIC_SITE_URL ?? "https://soccer.dev-spirit.com";

/** Crawlers: the public pages yes; the API and the auth callbacks no (members' pages redirect to login anyway). */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", allow: "/", disallow: ["/api/", "/auth/callback", "/auth/finish"] },
    sitemap: `${SITE}/sitemap.xml`,
  };
}
