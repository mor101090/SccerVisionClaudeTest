import type { Metadata } from "next";
import { Suspense } from "react";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { AuthFormSkeleton } from "@/features/auth/auth-skeleton";
import { ResetForm } from "@/features/auth/recovery-forms";

export const metadata: Metadata = { title: "Choose a new password", referrer: "no-referrer" };

export default function Page() {
  return (
    <AuthShell hero="recovery">
      <AuthTitle title="Choose a new password">
        At least 8 characters, with a number or a symbol.
      </AuthTitle>
      {/* The token is read from ?token= on the client. */}
      <Suspense fallback={<AuthFormSkeleton fields={2} />}>
        <ResetForm />
      </Suspense>
    </AuthShell>
  );
}
