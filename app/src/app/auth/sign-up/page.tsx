import type { Metadata } from "next";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { SignUpForm } from "@/features/auth/sign-up-form";

export const metadata: Metadata = { title: "Create your account" };

export default function Page() {
  return (
    <AuthShell hero="signup">
      <AuthTitle title="Create your account">
        Free to play. Your club, your kits, your 22 players.
      </AuthTitle>
      <SignUpForm />
    </AuthShell>
  );
}
