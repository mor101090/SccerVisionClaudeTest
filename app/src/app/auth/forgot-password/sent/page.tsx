import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { ResetSent } from "@/features/auth/recovery-forms";
import { getPendingEmail } from "@/server/auth-flow";

export const metadata: Metadata = { title: "Check your email" };

async function Sent() {
  const email = await getPendingEmail();
  if (!email) redirect("/auth/forgot-password");
  return <ResetSent email={email} />;
}

export default function Page() {
  return (
    <AuthShell hero="recovery">
      <Suspense fallback={<AuthTitle title="Check your email" />}>
        <Sent />
      </Suspense>
    </AuthShell>
  );
}
