import { NextResponse, type NextRequest } from "next/server";

import { auth } from "@/server/auth";

type Ctx = { params: Promise<{ path: string[] }> };

/**
 * The browser talks to Neon Auth through this proxy ONLY for what the client SDK needs: the session, its token,
 * Google sign-in / account linking and sign-out. Every password, sign-up, OTP and reset path goes through our Server
 * Actions (features/auth/actions.ts), which enforce the per-email lockout (server/login-throttle.ts) and the
 * anti-enumeration answers; forwarding them here would let anyone skip both. Anything not listed is a 404.
 */
const ALLOWED: Record<"GET" | "POST", ReadonlySet<string>> = {
  GET: new Set(["get-session", "token"]),
  POST: new Set(["sign-in/social", "link-social", "sign-out"]),
};

const notFound = () => NextResponse.json({ error: "Not found" }, { status: 404 });

// Resolved per request so the build does not need auth env vars.
const handle = (method: "GET" | "POST") => async (req: NextRequest, ctx: Ctx) => {
  const { path } = await ctx.params;
  if (!ALLOWED[method].has(path.join("/"))) return notFound();
  return auth().handler()[method](req, ctx);
};

export const GET = handle("GET");
export const POST = handle("POST");
