import "server-only";

import { cookies } from "next/headers";

/** The inviter's username from an invite link, kept for 30 days until Create your team, step 1, uses it. */
export const INVITE_COOKIE = "sv_invite";
const USERNAME = /^[A-Za-z0-9_]{3,20}$/;

export async function inviterFromCookie(): Promise<string | null> {
  const v = (await cookies()).get(INVITE_COOKIE)?.value ?? null;
  return v && USERNAME.test(v) ? v : null;
}

export function isInviteUsername(v: string): boolean {
  return USERNAME.test(v);
}
