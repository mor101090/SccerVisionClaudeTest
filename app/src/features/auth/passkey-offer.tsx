"use client";

import { useEffect, useState } from "react";

import { Button } from "@/components/ui/button";
import { PasskeyIcon } from "@/components/ui/icons";

import { approvePasskeyAction } from "./actions";
import {
  deviceLabel,
  offerDismissedKey,
  passkeyErrorMessage,
  passkeysSupported,
  shouldOfferPasskey,
} from "./passkeys";
import { supabaseBrowser } from "./supabase-client";

function dismissedFor(userId: string): boolean {
  try {
    return localStorage.getItem(offerDismissedKey(userId)) === "1";
  } catch {
    return false;
  }
}

function dismiss(userId: string) {
  try {
    localStorage.setItem(offerDismissedKey(userId), "1");
  } catch {
    // Private mode: the offer may come back after the next sign-in, which is fine.
  }
}

/**
 * "Log in faster next time?": right after a sign-in that didn't use a passkey, for a user without one, once per user
 * in this browser ("Not now" or creating one hides it). Nothing renders otherwise.
 */
export function PasskeyOffer() {
  const [userId, setUserId] = useState<string | null>(null);
  const [state, setState] = useState<"idle" | "busy" | "done">("idle");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!passkeysSupported()) return;
    let cancelled = false;
    void (async () => {
      const auth = supabaseBrowser().auth;
      const { data } = await auth.getClaims();
      const claims = data?.claims;
      const sub = typeof claims?.sub === "string" ? claims.sub : null;
      if (!sub || !shouldOfferPasskey(claims?.amr, Date.now() / 1000, dismissedFor(sub))) return;
      const { data: passkeys, error } = await auth.passkey.list();
      if (cancelled || error || (passkeys?.length ?? 0) > 0) return;
      setUserId(sub);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (!userId) return null;

  async function create() {
    if (!userId) return;
    setState("busy");
    setError(null);
    const auth = supabaseBrowser().auth;
    const { data, error } = await auth.registerPasskey();
    if (error || !data) {
      setState("idle");
      setError(passkeyErrorMessage(error, "create"));
      return;
    }
    const approval = await approvePasskeyAction(data.id);
    if (!approval.ok) {
      setState("idle");
      setError(
        approval.needsLogin
          ? "For your security, add a passkey within 10 minutes of logging in. Log in again, then add it in Settings."
          : "We couldn't create the passkey. Try again in Settings.",
      );
      return;
    }
    if (!data.friendly_name) {
      await auth.passkey.update({
        passkeyId: data.id,
        friendlyName: deviceLabel(navigator.userAgent),
      });
    }
    dismiss(userId);
    setState("done");
  }

  function notNow() {
    if (userId) dismiss(userId);
    setUserId(null);
  }

  return (
    <aside
      aria-labelledby="passkey-offer-title"
      className="fixed inset-x-4 bottom-4 z-40 flex flex-col gap-3 rounded-[14px] border border-border bg-panel p-4 shadow-[0_20px_50px_rgb(0_0_0/0.5)] sm:inset-x-auto sm:right-4 sm:w-[360px]"
    >
      {state === "done" ? (
        <>
          <p id="passkey-offer-title" className="text-[15px] font-semibold text-text">
            Passkey created
          </p>
          <p className="text-sm leading-[1.45] text-muted">
            Next time, choose &quot;Log in with a passkey&quot;. Manage it in Settings.
          </p>
          <Button variant="secondary" onClick={() => setUserId(null)}>
            Done
          </Button>
        </>
      ) : (
        <>
          <div className="flex items-start gap-3">
            <span className="mt-0.5 text-text">
              <PasskeyIcon />
            </span>
            <div className="flex flex-col gap-1">
              <p id="passkey-offer-title" className="text-[15px] font-semibold text-text">
                Log in faster next time?
              </p>
              <p className="text-sm leading-[1.45] text-muted">
                Create a passkey and log in with Face ID, Touch ID or your device PIN. No password
                needed.
              </p>
            </div>
          </div>
          {error && <p className="text-xs leading-[1.45] text-foul">{error}</p>}
          <div className="flex gap-2">
            <Button onClick={create} disabled={state === "busy"}>
              {state === "busy" ? "Waiting…" : "Create a passkey"}
            </Button>
            <Button variant="text" onClick={notNow} disabled={state === "busy"}>
              Not now
            </Button>
          </div>
        </>
      )}
    </aside>
  );
}
