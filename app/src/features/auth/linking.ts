import { isSocialProvider, PROVIDER_LABEL, type SocialProvider } from "./providers";

/** Where a "Connect" round trip returns: /settings?linking=<provider>. */
export const linkReturnPath = (provider: SocialProvider) => `/settings?linking=${provider}`;

/**
 * A user may disconnect a provider only while another way to sign in remains
 * (another identity: a social one, or email + password). Supabase refuses the last one too.
 */
export const canDisconnect = (identityCount: number) => identityCount >= 2;

export type LinkNotice = { tone: "success" | "error"; text: string } | null;

/**
 * The banner after a Connect round trip. Success is decided by the identities the account now has, never by the URL
 * alone: a failed or cancelled link can come back without an error in the query (e.g. in the fragment).
 */
export function linkNotice(
  params: { linking?: string; linkError?: string },
  connected: readonly string[],
): LinkNotice {
  const { linking, linkError } = params;
  if (!linking || !isSocialProvider(linking)) return null;
  const label = PROVIDER_LABEL[linking];
  if (!linkError && connected.includes(linking)) {
    return { tone: "success", text: `${label} is connected. You can now log in with it.` };
  }
  if (linkError === "identity_already_exists") {
    return {
      tone: "error",
      text: `That ${label} account is already used by another Soccer Vision account. Log in with it there, or use a different ${label} account.`,
    };
  }
  if (linkError === "access_denied") {
    return { tone: "error", text: `${label} wasn't connected: you cancelled at ${label}.` };
  }
  return { tone: "error", text: `We couldn't connect ${label}. Try again in a moment.` };
}

type IdentityLike = { provider: string; last_sign_in_at?: string | null };

/**
 * The social provider this session came from: the identity signed in with most recently. An account can hold
 * several (same-email sign-ins are linked automatically), so the first identity is not necessarily the current one.
 * Also returns the account's other social providers.
 */
export function signedInWith(identities: readonly IdentityLike[]): {
  current: SocialProvider | null;
  others: SocialProvider[];
} {
  const social = identities.filter((i): i is IdentityLike & { provider: SocialProvider } =>
    isSocialProvider(i.provider),
  );
  const time = (i: IdentityLike) => Date.parse(i.last_sign_in_at ?? "") || 0;
  const latest = social.reduce<(typeof social)[number] | null>(
    (best, i) => (best === null || time(i) > time(best) ? i : best),
    null,
  );
  const current = latest?.provider ?? null;
  return { current, others: social.map((i) => i.provider).filter((p) => p !== current) };
}

/** How long after Supabase merged a new sign-in method into an account we still ask the user to confirm it. */
export const AUTO_LINK_WINDOW_MS = 10 * 60_000;

/**
 * Supabase merges a sign-in whose verified email matches an existing account (automatic identity linking; it can't
 * be turned off). Returns the identity this sign-in just added to an older account, so the user can be asked
 * "connect it?" — or null for a returning sign-in, a brand-new account, or a link older than the window.
 */
export function justAutoLinked(
  identities: readonly (IdentityLike & { created_at?: string | null; identity_id?: string })[],
  now: number = Date.now(),
): { provider: SocialProvider; identityId: string } | null {
  const lastSignIn = (i: IdentityLike) => Date.parse(i.last_sign_in_at ?? "") || 0;
  const created = (i: { created_at?: string | null }) => Date.parse(i.created_at ?? "") || 0;
  const current = identities.reduce<(typeof identities)[number] | null>(
    (best, i) => (best === null || lastSignIn(i) > lastSignIn(best) ? i : best),
    null,
  );
  if (!current || !isSocialProvider(current.provider) || !current.identity_id) return null;
  const addedAt = created(current);
  if (!addedAt || now - addedAt > AUTO_LINK_WINDOW_MS || addedAt - now > 60_000) return null;
  // Only a merge: the account already had another way in before this identity was added.
  const older = identities.some(
    (i) => i !== current && created(i) > 0 && created(i) < addedAt - 1000,
  );
  return older ? { provider: current.provider, identityId: current.identity_id } : null;
}

/** "Facebook", "email and password": how an account was first created, from its oldest identity. */
export function signUpMethod(
  identities: readonly { provider: string; created_at?: string | null }[],
): string {
  const first = [...identities].sort(
    (a, b) => (Date.parse(a.created_at ?? "") || 0) - (Date.parse(b.created_at ?? "") || 0),
  )[0];
  if (!first) return "";
  return isSocialProvider(first.provider) ? PROVIDER_LABEL[first.provider] : "email and password";
}

/** Message after Disconnect (the unlink Server Action redirects with ?unlinked= or ?unlinkError=). */
export function unlinkNotice(params: { unlinked?: string; unlinkError?: string }): LinkNotice {
  if (params.unlinked && isSocialProvider(params.unlinked)) {
    return { tone: "success", text: `${PROVIDER_LABEL[params.unlinked]} is disconnected.` };
  }
  if (params.unlinkError === "last") {
    return {
      tone: "error",
      text: "That's your only way to log in. Connect another account first.",
    };
  }
  if (params.unlinkError) {
    return { tone: "error", text: "We couldn't disconnect that account. Try again in a moment." };
  }
  return null;
}
