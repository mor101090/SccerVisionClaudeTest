import type { NextRequest } from "next/server";

import { auth } from "@/server/auth";

/**
 * Next 16 proxy (formerly middleware): redirects signed-out users away from member pages
 * and refreshes the Neon Auth session cookie. Route Handlers and Server Actions still check
 * the session themselves (see requireUser) — this is not the only guard.
 *
 * /auth/callback must run through here: returning from Google, the middleware exchanges the
 * OAuth verifier for the session cookie (it is a public route, so it is not protected).
 * /auth/finish is protected: only a signed-in user picks a username.
 */
export default function proxy(request: NextRequest) {
  return auth().middleware({ loginUrl: "/auth/sign-in" })(request);
}

export const config = {
  matcher: [
    "/account/:path*",
    "/matches/:path*",
    "/teams/:path*",
    "/leagues/:path*",
    "/lobby/:path*",
    "/auth/callback",
    "/auth/finish",
    "/team/:path*",
  ],
};
