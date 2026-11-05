import { NextResponse, type NextRequest } from "next/server";

/**
 * Neon Auth sends failed social sign-ins here (errorCallbackURL). An existing password account
 * with the same email gets the "link your Google account" screen; anything else goes back to login.
 */
export function GET(request: NextRequest) {
  const error = request.nextUrl.searchParams.get("error");
  const to = error === "account_not_linked" ? "/auth/link-account" : "/auth/sign-in?error=social";
  return NextResponse.redirect(new URL(to, request.url));
}
