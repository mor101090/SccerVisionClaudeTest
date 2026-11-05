"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { DividerText } from "@/components/ui/divider-text";
import { TextField } from "@/components/ui/text-field";

import { signInAction } from "./actions";
import { AuthFooter } from "./auth-shell";
import { SocialButtons } from "./social-buttons";
import { RedirectField, useAuthForm } from "./use-auth-form";

/** Banners for arrivals from other flows (?reset=1, ?verified=1, ?error=social). */
function ArrivalNotice() {
  const q = useSearchParams();
  if (q.get("reset"))
    return <Alert tone="success">Password updated. Login with your new password.</Alert>;
  if (q.get("verified")) return <Alert tone="success">Email verified. Login to continue.</Alert>;
  if (q.get("error") === "social") {
    return (
      <Alert tone="error">Google sign-in didn&apos;t finish. Try again or use your email.</Alert>
    );
  }
  return null;
}

/** Figma: Sign in / Login and Login – wrong password. */
export function SignInForm() {
  const { state, pending, formProps } = useAuthForm(signInAction);
  const f = state?.fields ?? {};

  return (
    <>
      {state?.error ? (
        <Alert tone="error">{state.error}</Alert>
      ) : (
        <Suspense fallback={null}>
          <ArrivalNotice />
        </Suspense>
      )}
      <form {...formProps} className="flex flex-col gap-[18px]">
        <RedirectField />
        <TextField
          label="Email address"
          name="email"
          type="email"
          autoComplete="email"
          placeholder="you@club.com"
          defaultValue={state?.values?.email}
          error={f.email}
          required
        />
        <TextField
          label="Password"
          name="password"
          revealable
          autoComplete="current-password"
          placeholder="Your password"
          error={f.password}
          required
        />
        <div className="flex items-center justify-between">
          <Checkbox name="rememberMe" defaultChecked>
            Remember me
          </Checkbox>
          <Link
            href="/auth/forgot-password"
            className="text-sm leading-[1.45] font-semibold text-link hover:underline"
          >
            Forgot password?
          </Link>
        </div>
        <Button type="submit" disabled={pending}>
          {pending ? "Logging in…" : "Login"}
        </Button>
      </form>
      <DividerText>or login with</DividerText>
      <SocialButtons />
      <AuthFooter>
        Don&apos;t have an account yet?{" "}
        <Link href="/auth/sign-up" className="font-semibold text-link hover:underline">
          Sign up
        </Link>
      </AuthFooter>
    </>
  );
}
