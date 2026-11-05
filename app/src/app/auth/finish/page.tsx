import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { safeRedirect } from "@/features/auth/policy";
import { FinishForm, type ProviderAccount } from "@/features/auth/social-forms";
import { auth, currentUser } from "@/server/auth";
import { getProfile, isComplete } from "@/server/profiles";

export const metadata: Metadata = { title: "Almost there" };

/** The social provider this user signed in with, if any (credential = email/password). */
async function providerOf(user: { name: string; email: string }): Promise<ProviderAccount> {
  const { data } = await auth().listAccounts();
  const ids = ((data ?? []) as { providerId: string }[]).map((a) => a.providerId);
  const provider = ids.find((p): p is "google" | "facebook" => p === "google" || p === "facebook");
  return provider ? { provider, name: user.name, email: user.email } : null;
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
  const account = await providerOf(user);
  return (
    <>
      <AuthTitle title="Almost there">
        {account
          ? `You signed in with ${account.provider === "google" ? "Google" : "Facebook"}. Pick the name other managers will see.`
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
      <Suspense fallback={<AuthTitle title="Almost there" />}>
        <Finish searchParams={props.searchParams as Promise<{ redirectTo?: string }>} />
      </Suspense>
    </AuthShell>
  );
}
