"use client";

import Link from "next/link";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { DividerText } from "@/components/ui/divider-text";
import { TextField } from "@/components/ui/text-field";

import { signUpAction } from "./actions";
import { AuthFooter } from "./auth-shell";
import { SocialButtons } from "./social-buttons";
import { RedirectField, useAuthForm } from "./use-auth-form";

/** Figma: Register / Sign up and Sign up – errors. */
export function SignUpForm() {
  const { state, pending, formProps } = useAuthForm(signUpAction);
  const f = state?.fields ?? {};
  const emailTaken = f.email === "That email is already registered.";

  return (
    <>
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <form {...formProps} className="flex flex-col gap-[18px]">
        <RedirectField />
        <TextField
          label="Email address"
          name="email"
          type="email"
          autoComplete="email"
          placeholder="you@club.com"
          defaultValue={state?.values?.email}
          error={
            emailTaken ? (
              <>
                That email is already registered.{" "}
                <Link href="/auth/sign-in" className="underline">
                  Login instead?
                </Link>
              </>
            ) : (
              f.email
            )
          }
          required
        />
        <TextField
          label="Password"
          name="password"
          revealable
          autoComplete="new-password"
          placeholder="At least 8 characters"
          error={f.password}
          required
        />
        <TextField
          label="Confirm password"
          name="confirmPassword"
          revealable
          autoComplete="new-password"
          placeholder="Repeat your password"
          error={f.confirmPassword}
          required
        />
        <div className="flex flex-col gap-[18px]">
          <Checkbox name="terms" aria-describedby={f.terms ? "terms-error" : undefined}>
            I have read and agree to the{" "}
            <Link href="/terms" className="hover:underline" target="_blank">
              Terms and Conditions
            </Link>
          </Checkbox>
          {f.terms && (
            <p id="terms-error" role="alert" className="text-xs leading-[1.45] text-foul">
              {f.terms}
            </p>
          )}
        </div>
        <Button type="submit" disabled={pending}>
          {pending ? "Creating account…" : "Sign up"}
        </Button>
      </form>
      <DividerText>or sign up with</DividerText>
      <SocialButtons />
      <AuthFooter>
        I already have an account.{" "}
        <Link href="/auth/sign-in" className="font-semibold text-link hover:underline">
          Login
        </Link>
      </AuthFooter>
    </>
  );
}
