import type { Metadata } from "next";
import { Suspense } from "react";

import { AuthShell } from "@/features/auth/auth-shell";
import { AuthFormSkeleton } from "@/features/auth/auth-skeleton";
import { ConfirmGoogleLink, ConfirmMergedLink } from "@/features/auth/connect-account";
import { justAutoLinked } from "@/features/auth/linking";
import { safeRedirect } from "@/features/auth/policy";
import { accountSummary } from "@/server/account-match";
import { currentUser } from "@/server/auth";
import { supabaseServer } from "@/server/supabase";

export const metadata: Metadata = {
  title: "You already have an account",
  robots: { index: false },
};

/**
 * "You already have an account": a sign-in whose verified email belongs to an existing account.
 * Signed in with a sign-in method Supabase just merged (Facebook, Discord) → confirm or undo it.
 * Otherwise a Google sign-in waiting in this tab, not handed to Supabase yet → connect or drop it.
 */
async function Connect({ searchParams }: { searchParams: Promise<{ redirectTo?: string }> }) {
  const redirectTo = safeRedirect((await searchParams).redirectTo);
  const user = await currentUser();
  if (user) {
    const { data } = await (await supabaseServer()).auth.getUserIdentities();
    const linked = data ? justAutoLinked(data.identities) : null;
    if (linked) {
      return (
        <ConfirmMergedLink
          provider={linked.provider}
          identityId={linked.identityId}
          email={user.email}
          account={await accountSummary(user.id, linked.identityId)}
          redirectTo={redirectTo}
        />
      );
    }
  }
  return <ConfirmGoogleLink redirectTo={redirectTo} />;
}

export default function Page(props: PageProps<"/auth/connect-account">) {
  return (
    <AuthShell hero="social">
      <Suspense fallback={<AuthFormSkeleton title card text={2} extraButtons={1} />}>
        <Connect searchParams={props.searchParams as Promise<{ redirectTo?: string }>} />
      </Suspense>
    </AuthShell>
  );
}
