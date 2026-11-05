import { describe, expect, it } from "vitest";

import { getSiteUrl } from "./site-url";

describe("getSiteUrl", () => {
  it("prefers the explicit site URL and strips the trailing slash", () => {
    expect(getSiteUrl({ NEXT_PUBLIC_SITE_URL: "https://soccer.dev-spirit.com/" })).toBe(
      "https://soccer.dev-spirit.com",
    );
  });

  it("uses the production domain in production", () => {
    expect(
      getSiteUrl({
        VERCEL_ENV: "production",
        VERCEL_PROJECT_PRODUCTION_URL: "soccer.dev-spirit.com",
        VERCEL_URL: "soccer-vision-abc123.vercel.app",
      }),
    ).toBe("https://soccer.dev-spirit.com");
  });

  it("uses the deployment URL on previews", () => {
    expect(
      getSiteUrl({ VERCEL_ENV: "preview", VERCEL_URL: "soccer-vision-git-feat.vercel.app" }),
    ).toBe("https://soccer-vision-git-feat.vercel.app");
  });

  it("falls back to localhost", () => {
    expect(getSiteUrl({})).toBe("http://localhost:3000");
  });
});
