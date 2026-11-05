"use client";

import Image from "next/image";
import { useState } from "react";
import { useFormStatus } from "react-dom";

import { Alert } from "@/components/ui/alert";
import { clientEnv } from "@/env";

import { unlinkProviderAction } from "./actions";
import { startGoogle } from "./google-id";
import { canDisconnect, linkReturnPath } from "./linking";
import { PROVIDER_LABEL, type SocialProvider } from "./providers";
import { socialCallbackURL } from "./social-buttons";
import { supabaseBrowser } from "./supabase-client";

export type ProviderRow = {
  provider: SocialProvider;
  /** The provider account's email when connected, else null. */
  connectedAs: string | null;
};

const action =
  "h-9 shrink-0 rounded-lg px-3.5 text-sm leading-[1.45] font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link disabled:cursor-not-allowed disabled:opacity-45";

function DisconnectButton({ label, disabled }: { label: string; disabled: boolean }) {
  const { pending } = useFormStatus();
  return (
    <button
      type="submit"
      disabled={disabled || pending}
      aria-label={`Disconnect ${label}`}
      title={disabled ? "Your only way to log in. Connect another account first." : undefined}
      className={`${action} border border-border bg-panel-2 text-text enabled:hover:bg-panel`}
    >
      {pending ? "Disconnecting…" : "Disconnect"}
    </button>
  );
}

/** Settings → Connected accounts: one row per social provider, Connect (linkIdentity) or Disconnect. */
export function ConnectedAccounts({
  rows,
  identityCount,
}: {
  rows: ProviderRow[];
  identityCount: number;
}) {
  const [connecting, setConnecting] = useState<SocialProvider | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function connect(provider: SocialProvider) {
    setError(null);
    setConnecting(provider);
    const googleClientId = clientEnv.NEXT_PUBLIC_GOOGLE_CLIENT_ID;
    if (provider === "google" && googleClientId) {
      await startGoogle(googleClientId, "link", "/settings");
      return;
    }
    const { error } = await supabaseBrowser().auth.linkIdentity({
      provider,
      options: { redirectTo: socialCallbackURL(linkReturnPath(provider)) },
    });
    // On success the browser is already on its way to the provider.
    if (error) {
      setConnecting(null);
      console.error("[auth] link start failed", { status: error.status, code: error.code });
      setError(`We couldn't start connecting ${PROVIDER_LABEL[provider]}. Try again in a moment.`);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {error && <Alert tone="error">{error}</Alert>}
      <ul className="divide-y divide-border rounded-[10px] border border-border bg-panel">
        {rows.map(({ provider, connectedAs }) => {
          const label = PROVIDER_LABEL[provider];
          return (
            <li
              key={provider}
              className="flex items-center gap-3 px-3.5 py-3"
              data-testid={`provider-${provider}`}
            >
              <Image src={`/auth/${provider}.svg`} alt="" width={28} height={28} unoptimized />
              <div className="flex min-w-0 flex-1 flex-col gap-0.5 leading-[1.2]">
                <span className="text-[15px] font-semibold text-text">{label}</span>
                <span className="truncate text-[13px] text-muted">
                  {connectedAs !== null
                    ? `Connected${connectedAs ? ` · ${connectedAs}` : ""}`
                    : "Not connected"}
                </span>
              </div>
              {connectedAs !== null ? (
                <form action={unlinkProviderAction}>
                  <input type="hidden" name="provider" value={provider} />
                  <DisconnectButton label={label} disabled={!canDisconnect(identityCount)} />
                </form>
              ) : (
                <button
                  type="button"
                  onClick={() => connect(provider)}
                  disabled={connecting !== null}
                  aria-label={`Connect ${label}`}
                  className={`${action} bg-button text-button-text enabled:hover:bg-border`}
                >
                  {connecting === provider ? "Connecting…" : "Connect"}
                </button>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
