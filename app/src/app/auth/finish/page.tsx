import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { AuthFormSkeleton } from "@/features/auth/auth-skeleton";
import { signedInWith } from "@/features/auth/linking";
import { safeRedirect } from "@/features/auth/policy";
import { PROVIDER_LABEL, type SocialProvider } from "@/features/auth/providers";
import { FinishForm, type ProviderAccount } from "@/features/auth/social-forms";
import { currentUser } from "@/server/auth";
import { getProfile, isComplete } from "@/server/profiles";
import { supabaseServer } from "@/server/supabase";

export const metadata: Metadata = { title: "Almost there" };

/**
 * The social provider this session came from (the identity used most recently), and the account's other social
 * providers: a same-email sign-in is added to the existing account, so there can be several.
 */
async function providerOf(user: {
  name: string;
  email: string;
}): Promise<{ account: ProviderAccount; others: SocialProvider[] }> {
  const { data } = await (await supabaseServer()).auth.getUserIdentities();
  const { current, others } = signedInWith(data?.identities ?? []);
  return {
    account: current ? { provider: current, name: user.name, email: user.email } : null,
    others,
  };
}

function suggest(name: string): string {
  const s = name.replace(/[^A-Za-z0-9_]/g, "").slice(0, 20);
  return s.length >= 3 ? s : "";
}

async function Finish({ searchParams }: { searchParams: Promise<{ redirectTo?: string }> }) {
  const user = await currentUser();
  if (!user) redirect("/auth/sign-in");
  const profile = await getProfile(user.id);
  if (isComplete(profile)) {
    const { redirectTo } = await searchParams;
    redirect(safeRedirect(redirectTo));
  }
  const { account } = await providerOf(user);
  return (
    <>
      <AuthTitle title="Almost there">
        {account
          ? `You signed in with ${PROVIDER_LABEL[account.provider]}. Pick the name other managers will see.`
          : "Pick the name other managers will see."}
      </AuthTitle>
      <FinishForm
        account={account}
        email={user.email}
        suggestedUsername={profile?.username ?? suggest(user.name)}
        needsTerms={!profile?.termsAcceptedAt}
      />
    </>
  );
}

export default function Page(props: PageProps<"/auth/finish">) {
  return (
    <AuthShell hero="social">
      <Suspense
        fallback={
          <>
            <AuthTitle title="Almost there" />
            <AuthFormSkeleton card fields={2} checkbox />
          </>
        }
      >
        <Finish searchParams={props.searchParams as Promise<{ redirectTo?: string }>} />
      </Suspense>
    </AuthShell>
  );
}
