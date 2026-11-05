"use client";

import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useRef, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { PasskeyIcon } from "@/components/ui/icons";

import {
  passkeyAutofillSupported,
  passkeyErrorKind,
  passkeyErrorMessage,
  passkeysSupported,
} from "./passkeys";
import { safeRedirect } from "./policy";
import { supabaseBrowser } from "./supabase-client";

/** After a passkey sign-in: /auth/callback routes the session ("Almost there" until the profile is done). */
const afterSignIn = (redirectTo: string | null) =>
  `/auth/callback?redirectTo=${encodeURIComponent(safeRedirect(redirectTo))}`;

function PasskeySignInInner() {
  const redirectTo = useSearchParams().get("redirectTo");
  const [supported, setSupported] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const autofill = useRef<AbortController | null>(null);

  // Passkey suggestions in the email field's autofill (needs autocomplete="username webauthn" on it).
  useEffect(() => {
    if (!passkeysSupported()) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect -- browser capability, unknown until mounted
    setSupported(true);
    let cancelled = false;
    // Runs in the background, so it never shows an error the user didn't ask for. Its challenge expires after
    // 5 minutes while the autofill can stay open: then it starts again (a few times at most).
    async function listen(attemptsLeft: number) {
      const controller = new AbortController();
      autofill.current = controller;
      const { data, error } = await supabaseBrowser().auth.signInWithPasskey({
        options: { mediation: "conditional", signal: controller.signal },
      });
      if (cancelled || controller.signal.aborted) return;
      if (data?.session) {
        window.location.assign(afterSignIn(redirectTo));
        return;
      }
      if (passkeyErrorKind(error) === "expired" && attemptsLeft > 0) {
        void listen(attemptsLeft - 1);
        return;
      }
      if (error && passkeyErrorKind(error) !== "cancelled") {
        console.warn("[auth] passkey autofill stopped", {
          code: (error as { code?: unknown }).code,
        });
      }
    }
    void passkeyAutofillSupported().then((ok) => {
      if (ok && !cancelled) void listen(3);
    });
    return () => {
      cancelled = true;
      autofill.current?.abort();
    };
  }, [redirectTo]);

  async function signIn() {
    // One WebAuthn request at a time: stop the autofill one before opening the picker.
    autofill.current?.abort();
    setBusy(true);
    setError(null);
    const { data, error } = await supabaseBrowser().auth.signInWithPasskey();
    if (data?.session) {
      window.location.assign(afterSignIn(redirectTo));
      return;
    }
    setBusy(false);
    setError(passkeyErrorMessage(error, "signin"));
  }

  if (!supported) return null;
  return (
    <div className="flex flex-col gap-2">
      <Button variant="secondary" onClick={signIn} disabled={busy}>
        <PasskeyIcon />
        {busy ? "Waiting for your passkey…" : "Log in with a passkey"}
      </Button>
      {error && <Alert tone="error">{error}</Alert>}
    </div>
  );
}

/** "Log in with a passkey" (picker) plus passkey autofill in the email field. Hidden where passkeys aren't supported. */
export function PasskeySignIn() {
  return (
    <Suspense fallback={null}>
      <PasskeySignInInner />
    </Suspense>
  );
}
