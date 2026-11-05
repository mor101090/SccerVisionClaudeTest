"use client";

import Link from "next/link";
import { useRef, useState, useTransition } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

import { resendCodeAction, verifyEmailAction, type AuthFormState } from "./actions";
import { RESEND_CODE_SECONDS, VERIFY_CODE_MINUTES } from "./policy";
import { formatSeconds, RedirectField, useAuthForm, useCountdown } from "./use-auth-form";

const LENGTH = 6;

/** Six one-digit boxes (Figma: Code Digit 52×60). Typing advances, Backspace goes back, paste fills all. */
function CodeInput({
  digits,
  setDigits,
  invalid,
  describedBy,
}: {
  digits: string[];
  setDigits: React.Dispatch<React.SetStateAction<string[]>>;
  invalid: boolean;
  describedBy?: string;
}) {
  const refs = useRef<(HTMLInputElement | null)[]>([]);

  function fill(from: number, text: string) {
    const clean = text.replace(/\D/g, "").slice(0, LENGTH - from);
    if (!clean) return;
    setDigits((d) => {
      const next = [...d];
      for (let i = 0; i < clean.length; i++) next[from + i] = clean[i] ?? "";
      return next;
    });
    refs.current[Math.min(from + clean.length, LENGTH - 1)]?.focus();
  }

  return (
    <div className="flex justify-between gap-3" role="group" aria-label="6-digit code">
      <input type="hidden" name="otp" value={digits.join("")} />
      {digits.map((d, i) => (
        <input
          key={i}
          ref={(el) => {
            refs.current[i] = el;
          }}
          value={d}
          inputMode="numeric"
          autoComplete={i === 0 ? "one-time-code" : "off"}
          maxLength={LENGTH}
          aria-label={`Digit ${i + 1}`}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          autoFocus={i === 0}
          onChange={(e) => {
            const v = e.target.value;
            if (v === "") setDigits((ds) => ds.map((x, j) => (j === i ? "" : x)));
            else fill(i, v);
          }}
          onKeyDown={(e) => {
            if (e.key === "Backspace" && !digits[i] && i > 0) refs.current[i - 1]?.focus();
            if (e.key === "ArrowLeft" && i > 0) refs.current[i - 1]?.focus();
            if (e.key === "ArrowRight" && i < LENGTH - 1) refs.current[i + 1]?.focus();
          }}
          onPaste={(e) => {
            e.preventDefault();
            fill(i, e.clipboardData.getData("text"));
          }}
          className={`h-[60px] w-[52px] min-w-0 rounded-lg border bg-white text-center text-2xl leading-none font-semibold text-field-ink caret-link outline-none focus:border-2 focus:border-link focus:shadow-[0_0_0_3px] focus:shadow-link/25 ${
            invalid ? "border-foul" : "border-border"
          }`}
        />
      ))}
    </div>
  );
}

/** Figma: Register / Verify email. */
export function VerifyForm() {
  const { state, pending, formProps } = useAuthForm(verifyEmailAction);
  const { left, restart } = useCountdown(RESEND_CODE_SECONDS);
  const [resend, setResend] = useState<AuthFormState>(null);
  const [resending, startResend] = useTransition();
  const otpError = state?.fields?.otp;
  const [digits, setDigits] = useState<string[]>(Array(LENGTH).fill(""));
  const complete = digits.every((d) => d !== "");

  return (
    <>
      {(state?.error || resend?.error) && (
        <Alert tone="error">{state?.error ?? resend?.error}</Alert>
      )}
      {resend?.notice && <Alert tone="success">{resend.notice}</Alert>}
      <form {...formProps} className="flex flex-col gap-[18px]">
        <RedirectField />
        <CodeInput
          digits={digits}
          setDigits={setDigits}
          invalid={Boolean(otpError)}
          describedBy="otp-note"
        />
        <p
          id="otp-note"
          role={otpError ? "alert" : undefined}
          className={`text-[13px] leading-[1.45] ${otpError ? "text-foul" : "text-muted"}`}
        >
          {otpError ?? `The code expires in ${VERIFY_CODE_MINUTES} minutes.`}
        </p>
        <Button type="submit" disabled={pending || !complete}>
          {pending ? "Verifying…" : "Verify & continue"}
        </Button>
      </form>
      <Button
        variant="text"
        disabled={left > 0 || resending}
        onClick={() =>
          startResend(async () => {
            const r = await resendCodeAction();
            setResend(r);
            if (r?.ok) restart();
          })
        }
      >
        {left > 0 ? `Resend code in ${formatSeconds(left)}` : "Resend code"}
      </Button>
      <p className="text-center text-sm leading-[1.45] text-muted">
        Wrong email?{" "}
        <Link href="/auth/sign-up" className="font-semibold text-link hover:underline">
          Change it
        </Link>
      </p>
    </>
  );
}
