import type { Metadata } from "next";

import { AuthShell, AuthTitle } from "@/features/auth/auth-shell";
import { GoogleReturn } from "@/features/auth/google-return";

export const metadata: Metadata = { title: "Signing in with Google", robots: { index: false } };

export default function Page() {
  return (
    <AuthShell hero="social">
      <AuthTitle title="Connecting to Google" />
      <GoogleReturn />
    </AuthShell>
  );
}
