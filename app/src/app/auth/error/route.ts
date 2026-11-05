import { NextResponse, type NextRequest } from "next/server";

/**
 * Failed social sign-ins land here. Supabase links a social identity to an existing account with the same verified
 * email automatically; `account_not_linked` is kept for the manual "link your Google account" screen.
 */
export function GET(request: NextRequest) {
  const error = request.nextUrl.searchParams.get("error");
  const to = error === "account_not_linked" ? "/auth/link-account" : "/auth/sign-in?error=social";
  return NextResponse.redirect(new URL(to, request.url));
}
