"use client";

import { useEffect, useRef } from "react";

import { SpinnerIcon } from "@/components/ui/icons";

import { googleAccountCheckAction } from "./actions";
import { parseGoogleReturn, saveGoogleConfirm, takePending } from "./google-id";
import { linkReturnPath } from "./linking";
import { safeRedirect } from "./policy";
import { supabaseBrowser } from "./supabase-client";

const SIGN_IN_FAILED = "/auth/sign-in?error=social";

const linkResult = (error?: string) =>
  `${linkReturnPath("google")}${error ? `&linkError=${encodeURIComponent(error)}` : ""}`;

/** Where the browser goes once Google's answer has been handed to Supabase (or refused). */
async function finish(): Promise<string> {
  const answer = parseGoogleReturn(window.location.hash);
  // The token must not stay in the address bar or history.
  window.history.replaceState(null, "", window.location.pathname);
  const pending = takePending();
  if (!pending || answer.state !== pending.state) return SIGN_IN_FAILED;
  const link = pending.mode === "link";
  if (!answer.ok) return link ? linkResult(answer.error) : SIGN_IN_FAILED;

  const credentials = { provider: "google", token: answer.idToken, nonce: pending.nonce } as const;
  const auth = supabaseBrowser().auth;
  if (link) {
    const { error } = await auth.linkIdentity(credentials);
    return linkResult(error ? (error.code ?? "link_failed") : undefined);
  }
  // Same email as an existing account? Supabase would merge silently, so ask first (connect-account.tsx).
  const check = await googleAccountCheckAction(answer.idToken, pending.nonce);
  if ("error" in check) return SIGN_IN_FAILED;
  if (check.existing) {
    saveGoogleConfirm({
      token: answer.idToken,
      nonce: pending.nonce,
      redirectTo: safeRedirect(pending.redirectTo),
      email: check.email,
      account: check.existing,
    });
    return `/auth/connect-account?redirectTo=${encodeURIComponent(safeRedirect(pending.redirectTo))}`;
  }
  const { error } = await auth.signInWithIdToken(credentials);
  if (error) {
    console.error("[auth] google id token sign-in failed", {
      status: error.status,
      code: error.code,
    });
    return SIGN_IN_FAILED;
  }
  // /auth/callback routes a signed-in user: "Almost there" until the profile is complete, then redirectTo.
  return `/auth/callback?redirectTo=${encodeURIComponent(safeRedirect(pending.redirectTo))}`;
}

/** /auth/google: Google returns here with an ID token in the fragment (see google-id.ts). */
export function GoogleReturn() {
  const started = useRef(false);
  useEffect(() => {
    // Once: the pending nonce can be redeemed a single time (React runs effects twice in development).
    if (started.current) return;
    started.current = true;
    finish().then(
      (to) => window.location.replace(to),
      () => window.location.replace(SIGN_IN_FAILED),
    );
  }, []);
  return (
    <div role="status" className="flex items-center gap-3 text-[15px] text-muted">
      <SpinnerIcon className="animate-spin motion-reduce:animate-none" />
      Signing you in with Google…
    </div>
  );
}
