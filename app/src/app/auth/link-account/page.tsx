import type { Metadata } from "next";
import { Suspense } from "react";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { LinkAccountForm } from "@/features/auth/social-forms";

export const metadata: Metadata = { title: "Link your Google account" };

export default function Page() {
  return (
    <AuthShell hero="link">
      <AuthTitle title="Link your Google account">
        An account with this email already exists.
      </AuthTitle>
      <Suspense fallback={null}>
        <LinkAccountForm />
      </Suspense>
    </AuthShell>
  );
}
