"use client";

import { useSearchParams } from "next/navigation";
import { startTransition, Suspense, useActionState, useEffect, useRef, useState } from "react";

import type { AuthFormState } from "./actions";

type Action = (prev: AuthFormState, form: FormData) => Promise<AuthFormState>;

/**
 * Wraps a server action for an auth form. Submitting through onSubmit (not the form's action
 * prop alone) keeps what the user typed after an error: React resets forms after a form action.
 * The action prop stays as the no-JS fallback (a POST, so passwords never land in a URL).
 */
export function useAuthForm(action: Action) {
  const [state, dispatch, pending] = useActionState<AuthFormState, FormData>(action, null);
  const ref = useRef<HTMLFormElement>(null);
  // After a rejected submit, focus goes to the first field with an error (its message is read with it).
  useEffect(() => {
    if (state?.fields && Object.keys(state.fields).length > 0) {
      ref.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
    }
  }, [state]);
  const onSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    startTransition(() => dispatch(form));
  };
  return { state, pending, formProps: { ref, action: dispatch, onSubmit, noValidate: true } };
}

function RedirectFieldInner() {
  const redirectTo = useSearchParams().get("redirectTo");
  return redirectTo ? <input type="hidden" name="redirectTo" value={redirectTo} /> : null;
}

/** Carries ?redirectTo= into the form. The server validates it (same-site paths only). */
export function RedirectField() {
  return (
    <Suspense fallback={null}>
      <RedirectFieldInner />
    </Suspense>
  );
}

/** Seconds left of a countdown; `restart()` starts it again. */
export function useCountdown(seconds: number) {
  const [left, setLeft] = useState(seconds);
  useEffect(() => {
    if (left <= 0) return;
    const t = setTimeout(() => setLeft((s) => s - 1), 1000);
    return () => clearTimeout(t);
  }, [left]);
  return { left, restart: () => setLeft(seconds) };
}

export function formatSeconds(s: number): string {
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}
