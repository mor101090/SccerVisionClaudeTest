import { Suspense } from "react";

import { ShellClient } from "@/components/app-shell/shell-client";
import { ShellSkeleton } from "@/components/app-shell/shell-skeleton";
import { SiteHeader } from "@/components/site-header";
import { SkipLink } from "@/components/ui/skip-link";
import { PasskeyOffer } from "@/features/auth/passkey-offer";
import { currentUser } from "@/server/auth";
import { getShellData } from "@/server/shell";

/** Signed in: the app shell (sidebar + top bar). Visitors (public pages such as /home and /rules): the site header. */
async function Shell({ children }: { children: React.ReactNode }) {
  const user = await currentUser();
  if (!user) {
    return (
      <>
        <SkipLink />
        <SiteHeader />
        <main id="main" className="flex flex-1 flex-col gap-5 p-4 md:p-7">
          {children}
        </main>
        <PasskeyOffer />
      </>
    );
  }
  const data = await getShellData(user);
  return (
    <ShellClient data={data} skipLink={<SkipLink />}>
      {children}
      <PasskeyOffer />
    </ShellClient>
  );
}

export default function AppLayout({ children }: LayoutProps<"/">) {
  return (
    <Suspense fallback={<ShellSkeleton />}>
      <Shell>{children}</Shell>
    </Suspense>
  );
}
