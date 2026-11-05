import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { Alert } from "@/components/ui/alert";
import { Loading, Skeleton } from "@/components/ui/skeleton";
import { ConnectedAccounts, type ProviderRow } from "@/features/auth/connected-accounts";
import { linkNotice, unlinkNotice } from "@/features/auth/linking";
import { SOCIAL_PROVIDERS } from "@/features/auth/providers";
import { currentUser } from "@/server/auth";
import { supabaseServer } from "@/server/supabase";

export const metadata: Metadata = { title: "Settings" };

type Search = { linking?: string; linkError?: string; unlinked?: string; unlinkError?: string };

const one = (v: string | string[] | undefined) => (typeof v === "string" ? v : undefined);

async function Accounts({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const user = await currentUser();
  if (!user) redirect("/auth/sign-in?redirectTo=/settings");
  const raw = await searchParams;
  const params: Search = {
    linking: one(raw.linking),
    linkError: one(raw.linkError),
    unlinked: one(raw.unlinked),
    unlinkError: one(raw.unlinkError),
  };

  const { data, error } = await (await supabaseServer()).auth.getUserIdentities();
  if (error || !data) {
    return (
      <Alert tone="error">We couldn&apos;t load your accounts. Reload the page to try again.</Alert>
    );
  }
  const identities = data.identities;
  const connected = identities.map((i) => i.provider);
  const rows: ProviderRow[] = SOCIAL_PROVIDERS.map((provider) => {
    const identity = identities.find((i) => i.provider === provider);
    const email = identity?.identity_data?.email;
    return {
      provider,
      connectedAs: identity ? (typeof email === "string" ? email : "") : null,
    };
  });
  const hasPassword = connected.includes("email");
  const notice = linkNotice(params, connected) ?? unlinkNotice(params);

  return (
    <div className="flex flex-col gap-4">
      {notice && <Alert tone={notice.tone}>{notice.text}</Alert>}
      <p className="text-sm leading-[1.45] text-muted">
        Signed in as <span className="font-semibold text-text">{user.email}</span>
        {hasPassword ? " · email and password" : ""}. Connect more accounts to log in with any of
        them. If you sign in with another account that uses this email, we ask before connecting it.
      </p>
      <ConnectedAccounts rows={rows} identityCount={identities.length} />
    </div>
  );
}

function AccountsSkeleton() {
  return (
    <Loading className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <Skeleton className="h-3.5 w-full" />
        <Skeleton className="h-3.5 w-2/3" />
      </div>
      <div className="divide-y divide-border rounded-[10px] border border-border bg-panel">
        {[0, 1, 2].map((i) => (
          <div key={i} className="flex items-center gap-3 px-3.5 py-3">
            <Skeleton className="size-7 rounded-full" />
            <div className="flex flex-1 flex-col gap-1.5">
              <Skeleton className="h-4 w-20" />
              <Skeleton className="h-3 w-28" />
            </div>
            <Skeleton className="h-9 w-24 rounded-lg" />
          </div>
        ))}
      </div>
    </Loading>
  );
}

export default function SettingsPage(props: PageProps<"/settings">) {
  return (
    <main className="mx-auto flex w-full max-w-xl flex-1 flex-col gap-6 p-4 text-text sm:p-8">
      <h1 className="text-2xl font-semibold">Settings</h1>
      <section className="flex flex-col gap-3" aria-labelledby="connected-title">
        <h2 id="connected-title" className="text-lg font-bold">
          Connected accounts
        </h2>
        <Suspense fallback={<AccountsSkeleton />}>
          <Accounts searchParams={props.searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
