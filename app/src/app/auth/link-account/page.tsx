import type { Metadata } from "next";
import { Suspense } from "react";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { AuthFormSkeleton } from "@/features/auth/auth-skeleton";
import { LinkAccountForm } from "@/features/auth/social-forms";

export const metadata: Metadata = { title: "Link your Google account" };

export default function Page() {
  return (
    <AuthShell hero="link">
      <AuthTitle title="Link your Google account">
        An account with this email already exists.
      </AuthTitle>
      <Suspense fallback={<AuthFormSkeleton notice fields={2} extraButtons={2} />}>
        <LinkAccountForm />
      </Suspense>
    </AuthShell>
  );
}
