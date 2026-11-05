"use client";

import Image from "next/image";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useRef, useState } from "react";

import { Button } from "@/components/ui/button";
import { SpinnerIcon } from "@/components/ui/icons";

import { safeRedirect } from "./policy";
import { PROVIDER_LABEL, SOCIAL_PROVIDERS, type SocialProvider } from "./providers";
import { supabaseBrowser } from "./supabase-client";

const PROVIDER_STROKE: Record<SocialProvider, string> = {
  google: "border-google-stroke hover:shadow-google-stroke/25",
  facebook: "border-facebook-stroke hover:shadow-facebook-stroke/25",
  discord: "border-discord-stroke hover:shadow-discord-stroke/25",
};

const social =
  "flex h-11 flex-1 items-center justify-center gap-2.5 rounded-lg border bg-white text-[15px] leading-[1.45] font-medium text-field-ink transition-shadow hover:shadow-[0_0_0_3px] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link";

/** Absolute callback on this deployment; Supabase checks it against the project's redirect allowlist. */
export function socialCallbackURL(redirectTo: string | null): string {
  const to = encodeURIComponent(safeRedirect(redirectTo));
  return `${window.location.origin}/auth/callback?redirectTo=${to}`;
}

/** Figma "Social login – connecting to Google": shown until the browser leaves for the provider. */
function ConnectingDialog({
  provider,
  onCancel,
}: {
  provider: SocialProvider;
  onCancel: () => void;
}) {
  const label = PROVIDER_LABEL[provider];
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
          <Image
            src={provider === "google" ? "/auth/google-large.svg" : `/auth/${provider}.svg`}
            alt=""
            width={36}
            height={36}
            unoptimized
          />
          <SpinnerIcon className="animate-spin motion-reduce:animate-none" />
          <span className="flex size-9 items-center justify-center rounded-lg bg-accent text-sm leading-none font-extrabold text-bg">
            SV
          </span>
        </div>
        <h2 id="connecting-title" className="text-lg leading-[1.45] font-bold text-text">
          Connecting to {label}...
        </h2>
        <p className="text-sm leading-[1.45] text-muted">
          Finish signing in in the {label} window. Don&apos;t close this page.
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
  const [connecting, setConnecting] = useState<SocialProvider | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const cancelled = useRef(false);

  async function signIn(provider: SocialProvider) {
    cancelled.current = false;
    setMessage(null);
    setConnecting(provider);
    const { error } = await supabaseBrowser().auth.signInWithOAuth({
      provider,
      options: { redirectTo: socialCallbackURL(redirectTo) },
    });
    // On success the browser is already navigating to the provider.
    if (error && !cancelled.current) {
      setConnecting(null);
      setMessage(
        `${PROVIDER_LABEL[provider]} sign-in isn't available right now. Use another option or your email.`,
      );
    }
  }

  function cancel() {
    cancelled.current = true;
    window.stop();
    setConnecting(null);
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-3">
        {SOCIAL_PROVIDERS.map((provider) => (
          <button
            key={provider}
            type="button"
            onClick={() => signIn(provider)}
            className={`${social} ${PROVIDER_STROKE[provider]}`}
          >
            <Image src={`/auth/${provider}.svg`} alt="" width={18} height={18} unoptimized />
            {PROVIDER_LABEL[provider]}
          </button>
        ))}
      </div>
      {message && (
        <p role="status" className="text-xs leading-[1.45] text-muted">
          {message}
        </p>
      )}
      {connecting && <ConnectingDialog provider={connecting} onCancel={cancel} />}
    </div>
  );
}

/** Figma "social": Google + Facebook buttons, 12px gap (plus Discord, same style). */
export function SocialButtons() {
  return (
    <Suspense fallback={<div className="h-11" />}>
      <SocialButtonsInner />
    </Suspense>
  );
}
