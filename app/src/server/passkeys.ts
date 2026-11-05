import "server-only";

import { and, eq, inArray } from "drizzle-orm";

import { loggedInRecently, passkeyAuthenticatedAt, passkeyUsedAt } from "@/features/auth/passkeys";

import { db, schema } from "./db";
import { supabaseServer } from "./supabase";

const { approvedPasskeys, passkeySessions } = schema;

/**
 * Supabase registers a passkey for any valid session, through its API, with no recent-login rule. So passkeys are
 * approved here: only when the server (not the browser) sees a recent login. A passkey sign-in counts only when the
 * passkey it used is approved; others are deleted. Same idea as password_sessions.
 */

/** How long a just-registered passkey may wait for approval before it is treated as not approved. */
const APPROVAL_WINDOW_MS = 10 * 60_000;

type Supabase = Awaited<ReturnType<typeof supabaseServer>>;
type ListedPasskey = {
  id: string;
  friendly_name?: string;
  created_at: string;
  last_used_at?: string;
};

async function listPasskeys(supabase: Supabase): Promise<ListedPasskey[] | null> {
  const { data, error } = await supabase.auth.passkey.list();
  return error ? null : (data ?? []);
}

async function approvedIds(userId: string, ids: string[]): Promise<Set<string>> {
  if (ids.length === 0) return new Set();
  const rows = await db()
    .select({ id: approvedPasskeys.passkeyId })
    .from(approvedPasskeys)
    .where(and(eq(approvedPasskeys.userId, userId), inArray(approvedPasskeys.passkeyId, ids)));
  return new Set(rows.map((r) => r.id));
}

export type ApproveResult = "approved" | "needs_login" | "not_found";

/**
 * Approve a passkey the signed-in user just registered. Needs a recent login in the verified session token; if not,
 * the passkey is deleted again.
 */
export async function approvePasskey(userId: string, passkeyId: string): Promise<ApproveResult> {
  const supabase = await supabaseServer();
  const { data } = await supabase.auth.getClaims();
  const claims = data?.claims;
  if (claims?.sub !== userId) return "not_found";
  const passkeys = await listPasskeys(supabase);
  const passkey = passkeys?.find((p) => p.id === passkeyId);
  if (!passkey) return "not_found";
  const fresh = Date.now() - Date.parse(passkey.created_at) <= APPROVAL_WINDOW_MS;
  if (!fresh || !loggedInRecently(claims?.amr, Date.now() / 1000)) {
    await supabase.auth.passkey.delete({ passkeyId });
    return "needs_login";
  }
  await db().insert(approvedPasskeys).values({ passkeyId, userId }).onConflictDoNothing();
  return "approved";
}

/**
 * The signed-in user's passkeys, each marked approved or not. Passkeys never approved within the window (registered
 * around the app, or a tab closed before approval) are deleted here.
 */
export async function approvedPasskeyList(
  userId: string,
): Promise<(ListedPasskey & { approved: boolean })[] | null> {
  const supabase = await supabaseServer();
  const passkeys = await listPasskeys(supabase);
  if (!passkeys) return null;
  const approved = await approvedIds(
    userId,
    passkeys.map((p) => p.id),
  );
  const kept: (ListedPasskey & { approved: boolean })[] = [];
  for (const p of passkeys) {
    const ok = approved.has(p.id);
    if (!ok && Date.now() - Date.parse(p.created_at) > APPROVAL_WINDOW_MS) {
      await supabase.auth.passkey.delete({ passkeyId: p.id });
      continue;
    }
    kept.push({ ...p, approved: ok });
  }
  return kept;
}

/**
 * /auth/callback after a passkey sign-in: count the session only if the passkey it used is approved. An unapproved
 * passkey is deleted. Returns false when the session must not be signed in.
 */
export async function vetPasskeySession(
  claims: { sub?: unknown; session_id?: unknown; amr?: unknown } | null | undefined,
): Promise<boolean> {
  const at = passkeyAuthenticatedAt(claims);
  const userId = typeof claims?.sub === "string" ? claims.sub : null;
  const sessionId = typeof claims?.session_id === "string" ? claims.session_id : null;
  if (at === null || !userId || !sessionId) return false;
  if (await isAllowedPasskeySession(sessionId)) return true;
  const supabase = await supabaseServer();
  const passkeys = await listPasskeys(supabase);
  const usedId = passkeys ? passkeyUsedAt(passkeys, at) : null;
  if (!usedId) return false;
  if (!(await approvedIds(userId, [usedId])).has(usedId)) {
    await supabase.auth.passkey.delete({ passkeyId: usedId });
    return false;
  }
  await db().insert(passkeySessions).values({ sessionId, userId }).onConflictDoNothing();
  return true;
}

/** True when this passkey session was vetted by the app. */
export async function isAllowedPasskeySession(sessionId: string): Promise<boolean> {
  const [row] = await db()
    .select({ sessionId: passkeySessions.sessionId })
    .from(passkeySessions)
    .where(eq(passkeySessions.sessionId, sessionId));
  return Boolean(row);
}

export async function forgetPasskeySession(sessionId: string): Promise<void> {
  await db().delete(passkeySessions).where(eq(passkeySessions.sessionId, sessionId));
}

/** True when Supabase answers that passkeys are switched off for the project. */
export async function passkeysDisabled(): Promise<boolean> {
  const { error } = await (await supabaseServer()).auth.passkey.list();
  return (error as { code?: unknown } | null)?.code === "passkey_disabled";
}
