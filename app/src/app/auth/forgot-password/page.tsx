import type { Metadata } from "next";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { ForgotForm } from "@/features/auth/recovery-forms";

export const metadata: Metadata = { title: "Reset your password" };

export default function Page() {
  return (
    <AuthShell hero="recovery">
      <AuthTitle title="Reset your password">
        Enter the email you signed up with and we&apos;ll send you a link to choose a new password.
      </AuthTitle>
      <ForgotForm />
    </AuthShell>
  );
}
