"use client";

import { createAuthClient } from "@neon/auth/next";

/** Browser auth client. Talks to the same-origin proxy at /api/auth/*. */
export const authClient = createAuthClient();
