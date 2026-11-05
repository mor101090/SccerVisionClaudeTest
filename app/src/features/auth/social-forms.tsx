"use client";

import Image from "next/image";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { TextField } from "@/components/ui/text-field";

import { finishProfileAction, linkSignInAction, switchAccountAction } from "./actions";
import { PROVIDER_LABEL, type SocialProvider } from "./providers";
import { socialCallbackURL } from "./social-buttons";
import { supabaseBrowser } from "./supabase-client";
import { RedirectField, useAuthForm } from "./use-auth-form";

export type ProviderAccount = {
  provider: SocialProvider;
  name: string;
  email: string;
} | null;

/** Figma "provider account": who you signed in as, with "Change" (signs out). */
function ProviderCard({ account }: { account: NonNullable<ProviderAccount> }) {
  return (
    <div className="flex items-center gap-3 rounded-[10px] border border-border bg-panel px-3.5 py-3">
      <Image src={`/auth/${account.provider}.svg`} alt="" width={33} height={33} unoptimized />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5 leading-[1.2]">
        <span className="truncate text-[15px] font-semibold text-text">{account.name}</span>
        <span className="truncate text-[13px] text-muted">
          {account.email} · {PROVIDER_LABEL[account.provider]}
        </span>
      </div>
      <form action={switchAccountAction}>
        <button
          type="submit"
          className="text-sm leading-[1.45] font-semibold text-link hover:underline"
        >
          Change
        </button>
      </form>
    </div>
  );
}

/** Figma: Google & Facebook / Social sign-up – finish your account ("Almost there"). Also used for Discord. */
export function FinishForm({
  account,
  email,
  suggestedUsername,
  needsTerms,
}: {
  account: ProviderAccount;
  email: string;
  suggestedUsername: string;
  needsTerms: boolean;
}) {
  const { state, pending, formProps } = useAuthForm(finishProfileAction);
  const f = state?.fields ?? {};
  return (
    <>
      {account && <ProviderCard account={account} />}
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <form {...formProps} className="flex flex-col gap-[18px]">
        <RedirectField />
        <TextField
          label="Email address"
          value={email}
          readOnly
          disabled
          helper={
            account
              ? `From your ${PROVIDER_LABEL[account.provider]} account. You can change it later in Settings.`
              : "The email you signed up with."
          }
        />
        <TextField
          label="Username"
          name="username"
          autoComplete="username"
          defaultValue={state?.values?.username ?? suggestedUsername}
          helper="3–20 letters, numbers or _. Shown on the league table."
          error={f.username}
          autoFocus
          required
        />
        {needsTerms && (
          <div className="flex flex-col gap-2">
            <Checkbox name="terms">
              I have read and agree to the{" "}
              <Link href="/terms" target="_blank" className="hover:underline">
                Terms and Conditions
              </Link>
            </Checkbox>
            {f.terms && (
              <p role="alert" className="text-xs leading-[1.45] text-foul">
                {f.terms}
              </p>
            )}
          </div>
        )}
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : "Create account & build my team"}
        </Button>
      </form>
    </>
  );
}

/**
 * Figma: Google & Facebook / Social login – link existing account.
 * Step 1 logs in with the password (server action), step 2 links Google in the browser.
 */
export function LinkAccountForm() {
  const redirectTo = useSearchParams().get("redirectTo");
  const { state, pending, formProps } = useAuthForm(linkSignInAction);
  const [linkError, setLinkError] = useState<string | null>(null);
  const f = state?.fields ?? {};

  useEffect(() => {
    if (!state?.ok) return;
    supabaseBrowser()
      .auth.linkIdentity({
        provider: "google",
        options: { redirectTo: socialCallbackURL(redirectTo) },
      })
      .then(({ error }) => {
        if (error) setLinkError("We couldn't link Google. You are logged in with your password.");
      });
  }, [state, redirectTo]);

  return (
    <>
      <Alert tone="info">
        This email is already registered with a password. Enter it once to link Google — next time
        you can just tap Google.
      </Alert>
      {(state?.error || linkError) && <Alert tone="error">{linkError ?? state?.error}</Alert>}
      <form {...formProps} className="flex flex-col gap-[18px]">
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
          error={f.password}
          required
        />
        <Button type="submit" disabled={pending || state?.ok}>
          {pending || state?.ok ? "Linking…" : "Link Google & login"}
        </Button>
      </form>
      <form action={switchAccountAction}>
        <Button type="submit" variant="text">
          Use a different account
        </Button>
      </form>
      <p className="text-center text-sm leading-[1.45] text-muted">
        Forgot your password?{" "}
        <Link href="/auth/forgot-password" className="font-semibold text-link hover:underline">
          Reset it
        </Link>
      </p>
    </>
  );
}
