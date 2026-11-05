import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { Alert } from "@/components/ui/alert";
import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { AuthFormSkeleton } from "@/features/auth/auth-skeleton";
import { VerifyForm } from "@/features/auth/verify-form";
import { getPendingEmail } from "@/server/auth-flow";

export const metadata: Metadata = { title: "Check your inbox" };

async function Verify() {
  const email = await getPendingEmail();
  if (!email) {
    return (
      <>
        <AuthTitle title="Check your inbox" />
        <Alert tone="error">
          Your code expired.{" "}
          <Link href="/auth/sign-in" className="underline">
            Log in
          </Link>{" "}
          to get a new one.
        </Alert>
      </>
    );
  }
  return (
    <>
      <AuthTitle title="Check your inbox">
        We sent a 6-digit code to {email}. Type it in to activate your account.
      </AuthTitle>
      <VerifyForm />
    </>
  );
}

export default function Page() {
  return (
    <AuthShell hero="verify">
      <Suspense
        fallback={
          <>
            <AuthTitle title="Check your inbox" />
            <AuthFormSkeleton code extraButtons={1} />
          </>
        }
      >
        <Verify />
      </Suspense>
    </AuthShell>
  );
}
