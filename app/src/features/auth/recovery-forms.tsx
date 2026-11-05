"use client";

import { useSearchParams } from "next/navigation";
import { useState, useTransition } from "react";

import { Alert } from "@/components/ui/alert";
import { Button, ButtonLink } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";

import {
  forgotPasswordAction,
  resendResetAction,
  resetPasswordAction,
  type AuthFormState,
} from "./actions";
import { AuthTitle } from "./auth-shell";
import { passwordStrength, RESEND_RESET_SECONDS, RESET_LINK_MINUTES } from "./policy";
import { formatSeconds, useAuthForm, useCountdown } from "./use-auth-form";

/** Figma: Sign in / Forgot password. */
export function ForgotForm() {
  const { state, pending, formProps } = useAuthForm(forgotPasswordAction);
  return (
    <>
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <form {...formProps} className="flex flex-col gap-[18px]">
        <TextField
          label="Email address"
          name="email"
          type="email"
          autoComplete="email"
          placeholder="you@club.com"
          defaultValue={state?.values?.email}
          error={state?.fields?.email}
          autoFocus
          required
        />
        <Button type="submit" disabled={pending}>
          {pending ? "Sending…" : "Send reset link"}
        </Button>
      </form>
      <ButtonLink href="/auth/sign-in" variant="text">
        ← Back to login
      </ButtonLink>
    </>
  );
}

/** Figma: Sign in / Forgot password – email sent. */
export function ResetSent({ email }: { email: string }) {
  const { left, restart } = useCountdown(RESEND_RESET_SECONDS);
  const [result, setResult] = useState<AuthFormState>(null);
  const [pending, start] = useTransition();
  const waiting = left > 0;

  return (
    <>
      <AuthTitle title="Check your email" />
      <Alert tone="success">
        We sent a reset link to {email}. It expires in {RESET_LINK_MINUTES} minutes.
      </Alert>
      {result?.error && <Alert tone="error">{result.error}</Alert>}
      <p className="text-[15px] leading-[1.45] text-muted" aria-live="polite">
        {waiting
          ? `Didn't get it? Check your spam folder, or send it again in ${formatSeconds(left)}.`
          : (result?.notice ?? "Didn't get it? Check your spam folder, or send it again.")}
      </p>
      <ButtonLink href="/auth/sign-in">Back to login</ButtonLink>
      <Button
        disabled={waiting || pending}
        onClick={() =>
          start(async () => {
            const r = await resendResetAction();
            setResult(r);
            if (r?.ok) restart();
          })
        }
      >
        {waiting ? `Resend link (${formatSeconds(left)})` : "Resend link"}
      </Button>
    </>
  );
}

const BAR_COLOR = ["", "bg-foul", "bg-chance", "bg-goal", "bg-goal"] as const;
const LABEL_COLOR = ["", "text-foul", "text-chance", "text-goal", "text-goal"] as const;

/** Figma "strength": four 4px bars, filled ones color/goal when strong, label on the right. */
function StrengthMeter({ password }: { password: string }) {
  const { score, label } = passwordStrength(password);
  return (
    <div className="flex items-center gap-1.5" aria-live="polite">
      {[1, 2, 3, 4].map((i) => (
        <span
          key={i}
          className={`h-1 flex-1 rounded-sm ${i <= score ? BAR_COLOR[score] : "bg-panel"}`}
        />
      ))}
      <span
        className={`w-12 text-right text-xs leading-[1.45] font-semibold ${LABEL_COLOR[score]}`}
      >
        {label}
      </span>
    </div>
  );
}

/** Figma: Sign in / Reset password – new password. Token comes from the emailed link. */
export function ResetForm() {
  const q = useSearchParams();
  const token = q.get("token");
  const { state, pending, formProps } = useAuthForm(resetPasswordAction);
  const [password, setPassword] = useState("");
  const f = state?.fields ?? {};

  if (!token || q.get("error")) {
    return (
      <>
        <Alert tone="error">This reset link expired or was already used. Request a new one.</Alert>
        <ButtonLink href="/auth/forgot-password">Send a new link</ButtonLink>
      </>
    );
  }

  return (
    <>
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <form {...formProps} className="flex flex-col gap-[18px]">
        <input type="hidden" name="token" value={token} />
        <TextField
          label="New password"
          name="password"
          revealable
          autoComplete="new-password"
          autoFocus
          error={f.password}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        >
          <div className="pt-3">
            <StrengthMeter password={password} />
          </div>
        </TextField>
        <TextField
          label="Confirm new password"
          name="confirmPassword"
          revealable
          autoComplete="new-password"
          error={f.confirmPassword}
          required
        />
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : "Save new password"}
        </Button>
      </form>
    </>
  );
}
