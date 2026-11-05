"use client";

import Image from "next/image";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useRef, useState } from "react";

import { Button } from "@/components/ui/button";
import { SpinnerIcon } from "@/components/ui/icons";

import { authClient } from "./auth-client";
import { safeRedirect } from "./policy";

const social =
  "flex h-11 flex-1 items-center justify-center gap-2.5 rounded-lg border bg-white text-[15px] leading-[1.45] font-medium text-field-ink transition-shadow hover:shadow-[0_0_0_3px] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link";

/** Absolute callback on this deployment; Neon Auth checks it against the branch's trusted domains. */
export function socialCallbackURL(redirectTo: string | null): string {
  const to = encodeURIComponent(safeRedirect(redirectTo));
  return `${window.location.origin}/auth/callback?redirectTo=${to}`;
}

/** Figma "Social login – connecting to Google": shown until the browser leaves for Google. */
function ConnectingDialog({ onCancel }: { onCancel: () => void }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    ref.current?.showModal();
  }, []);
  return (
    <dialog
      ref={ref}
      onCancel={onCancel}
      aria-labelledby="connecting-title"
      className="m-auto w-[380px] max-w-[calc(100%-32px)] rounded-[14px] border border-border bg-panel p-8 text-center shadow-[0_20px_50px_rgb(0_0_0/0.5)] backdrop:bg-black/60"
    >
      <div className="flex flex-col items-center gap-4">
        <div className="flex items-center gap-3.5">
          <Image src="/auth/google-large.svg" alt="" width={36} height={36} unoptimized />
          <SpinnerIcon className="animate-spin motion-reduce:animate-none" />
          <span className="flex size-9 items-center justify-center rounded-lg bg-accent text-sm leading-none font-extrabold text-bg">
            SV
          </span>
        </div>
        <h2 id="connecting-title" className="text-lg leading-[1.45] font-bold text-text">
          Connecting to Google...
        </h2>
        <p className="text-sm leading-[1.45] text-muted">
          Finish signing in in the Google window. Don&apos;t close this page.
        </p>
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </dialog>
  );
}

function SocialButtonsInner() {
  const redirectTo = useSearchParams().get("redirectTo");
  const [connecting, setConnecting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const cancelled = useRef(false);

  async function google() {
    cancelled.current = false;
    setMessage(null);
    setConnecting(true);
    const callbackURL = socialCallbackURL(redirectTo);
    const { error } = await authClient.signIn.social({
      provider: "google",
      callbackURL,
      errorCallbackURL: `${window.location.origin}/auth/error`,
    });
    // On success the browser is already navigating to Google.
    if (error && !cancelled.current) {
      setConnecting(false);
      setMessage("Google sign-in isn't available right now. Use your email and password.");
    }
  }

  function cancel() {
    cancelled.current = true;
    window.stop();
    setConnecting(false);
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-3">
        <button
          type="button"
          onClick={google}
          className={`${social} border-google-stroke hover:shadow-google-stroke/25`}
        >
          <Image src="/auth/google.svg" alt="" width={18} height={18} unoptimized />
          Google
        </button>
        <button
          type="button"
          aria-disabled
          onClick={() =>
            setMessage("Facebook login is coming soon. Use Google or your email for now.")
          }
          className={`${social} border-facebook-stroke hover:shadow-facebook-stroke/25`}
        >
          <Image src="/auth/facebook.svg" alt="" width={18} height={18} unoptimized />
          Facebook
        </button>
      </div>
      {message && (
        <p role="status" className="text-xs leading-[1.45] text-muted">
          {message}
        </p>
      )}
      {connecting && <ConnectingDialog onCancel={cancel} />}
    </div>
  );
}

/** Figma "social": Google + Facebook buttons, 12px gap. */
export function SocialButtons() {
  return (
    <Suspense fallback={<div className="h-11" />}>
      <SocialButtonsInner />
    </Suspense>
  );
}
