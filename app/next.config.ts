import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  experimental: {
    // Keep pages the manager just visited for 30s, so going back and forth between tabs is instant. Server Actions
    // that change data revalidate the affected paths, which drops these copies.
    staleTimes: { dynamic: 30 },
  },
  // Country flags on the manager step (features/team/countries.ts).
  images: { remotePatterns: [new URL("https://flagcdn.com/**")] },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async rewrites() {
    // Browsers ask for /favicon.ico on pages without an icon link (and some always do): serve the app icon.
    return [{ source: "/favicon.ico", destination: "/icon.png" }];
  },
  async redirects() {
    // The classic game is the default site (Mor's flow lives in tools/demo-page); the Next.js landing page is /home.
    // It uses relative URLs (sounds/...), so it is served at its file path.
    return [
      { source: "/", destination: "/classic/index.html", permanent: false },
      { source: "/classic", destination: "/classic/index.html", permanent: false },
    ];
  },
  async headers() {
    return [
      {
        // Baseline hardening for every response. (Vercel adds HSTS on its domains.)
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          // No framing: blocks clickjacking of the login and account screens.
          { key: "X-Frame-Options", value: "DENY" },
          {
            key: "Content-Security-Policy",
            value: "frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
          },
          {
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=(), payment=()",
          },
        ],
      },
      {
        // Engine builds live in content-hashed folders: cache forever.
        source: "/engine/:version/:path*",
        headers: [{ key: "Cache-Control", value: "public, max-age=31536000, immutable" }],
      },
      {
        // Pointer to the current build: always revalidate.
        source: "/engine/current.json",
        headers: [{ key: "Cache-Control", value: "no-cache" }],
      },
      {
        source: "/:path*.wasm",
        headers: [{ key: "Content-Type", value: "application/wasm" }],
      },
    ];
  },
};

export default nextConfig;
