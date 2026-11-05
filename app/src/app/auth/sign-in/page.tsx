import type { Metadata } from "next";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { SignInForm } from "@/features/auth/sign-in-form";

export const metadata: Metadata = { title: "Login" };

export default function Page() {
  return (
    <AuthShell hero="login">
      <AuthTitle title="Login to your account">
        Welcome back, coach. Your squad is waiting.
      </AuthTitle>
      <SignInForm />
    </AuthShell>
  );
}
