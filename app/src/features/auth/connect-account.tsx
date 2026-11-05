"use client";

import Image from "next/image";
import { useEffect, useState } from "react";
import { useFormStatus } from "react-dom";

import { BrandMark } from "@/components/brand";
import { Alert } from "@/components/ui/alert";
import { Button, ButtonLink } from "@/components/ui/button";

import { rejectAutoLinkAction } from "./actions";
import { AuthTitle } from "./auth-shell";
import { AuthFormSkeleton } from "./auth-skeleton";
import { clearGoogleConfirm, readGoogleConfirm } from "./google-id";
import { safeRedirect } from "./policy";
import { PROVIDER_LABEL, type SocialProvider } from "./providers";
import { supabaseBrowser } from "./supabase-client";

export type ExistingAccountView = {
  username: string | null;
  clubName: string | null;
  signedUpWith: string;
};

const afterConnect = (redirectTo: string) =>
  `/auth/callback?redirectTo=${encodeURIComponent(safeRedirect(redirectTo))}&linkConfirmed=1`;

const notConnected = (provider: SocialProvider, via: string) =>
  `/auth/sign-in?notConnected=${provider}&via=${encodeURIComponent(via)}`;

/** The existing account, as its owner will recognise it: manager name, club, how it was created. */
function AccountCard({ account }: { account: ExistingAccountView }) {
  const icon = (["Google", "Facebook", "Discord"] as const).find((p) => p === account.signedUpWith);
  return (
    <div className="flex items-center gap-3 rounded-[10px] border border-border bg-panel px-3.5 py-3">
      {icon ? (
        <Image src={`/auth/${icon.toLowerCase()}.svg`} alt="" width={33} height={33} unoptimized />
      ) : (
        <BrandMark size={33} />
      )}
      <div className="flex min-w-0 flex-1 flex-col gap-0.5 leading-[1.2]">
        <span className="truncate text-[15px] font-semibold text-text">
          {account.username ?? "Unfinished account"}
          {account.clubName ? ` · ${account.clubName}` : ""}
        </span>
        <span className="truncate text-[13px] text-muted">
          {account.username ? "Signed up" : "Started"} with{" "}
          {account.signedUpWith || "another method"}
          {account.username ? "" : " · no username yet"}
        </span>
      </div>
    </div>
  );
}

function Explainer({
  provider,
  email,
  account,
}: {
  provider: SocialProvider;
  email: string;
  account: ExistingAccountView;
}) {
  const label = PROVIDER_LABEL[provider];
  // An account whose "Almost there" step (username + terms) was never done goes there next, whatever the provider.
  if (!account.username) {
    return (
      <>
        <AuthTitle title="Finish your account">
          {email ? `${email} is` : "This email is"} already used by an account you started with{" "}
          {account.signedUpWith || "another method"}.
        </AuthTitle>
        <p className="text-[15px] leading-[1.45] text-muted">
          Connect {label} to it, then pick your username to finish setting it up. You can disconnect{" "}
          {label} later in Settings.
        </p>
      </>
    );
  }
  return (
    <>
      <AuthTitle title="You already have an account">
        {email ? `${email} is` : "This email is"} already used by a Soccer Vision account.
      </AuthTitle>
      <p className="text-[15px] leading-[1.45] text-muted">
        Connect {label} to it so you can log in with either. Your club, squad and matches stay the
        same, and you can disconnect {label} later in Settings.
      </p>
    </>
  );
}

function RejectButton({ label }: { label: string }) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" variant="text" disabled={pending}>
      {pending ? "Cancelling…" : `Don't connect ${label}`}
    </Button>
  );
}

/**
 * Facebook / Discord: Supabase already merged the sign-in into the existing account (same verified email).
 * "Connect" keeps it; "Don't connect" removes the new sign-in method again and signs out.
 */
export function ConfirmMergedLink({
  provider,
  identityId,
  email,
  account,
  redirectTo,
}: {
  provider: SocialProvider;
  identityId: string;
  email: string;
  account: ExistingAccountView;
  redirectTo: string;
}) {
  const label = PROVIDER_LABEL[provider];
  return (
    <>
      <Explainer provider={provider} email={email} account={account} />
      <AccountCard account={account} />
      <ButtonLink href={afterConnect(redirectTo)}>Connect {label} &amp; continue</ButtonLink>
      <form action={rejectAutoLinkAction}>
        <input type="hidden" name="identityId" value={identityId} />
        <input type="hidden" name="redirectTo" value={redirectTo} />
        <RejectButton label={label} />
      </form>
    </>
  );
}

/**
 * Google: nothing is merged yet. The verified token waits in this tab (sessionStorage) until the user answers;
 * "Connect" hands it to Supabase (which links it to the account), "Don't connect" drops it.
 */
export function ConfirmGoogleLink({ redirectTo }: { redirectTo: string }) {
  const [pending, setPending] = useState<ReturnType<typeof readGoogleConfirm> | undefined>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const p = readGoogleConfirm();
    if (!p) window.location.replace("/auth/sign-in");
    // Read once after mount: sessionStorage isn't available during prerender.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setPending(p);
  }, []);

  if (!pending) return <AuthFormSkeleton title card text={2} extraButtons={1} />;

  async function connect() {
    if (!pending) return;
    setBusy(true);
    setError(null);
    const { error } = await supabaseBrowser().auth.signInWithIdToken({
      provider: "google",
      token: pending.token,
      nonce: pending.nonce,
    });
    clearGoogleConfirm();
    if (error) {
      setBusy(false);
      setError("Google sign-in expired. Start again from the login page.");
      return;
    }
    window.location.replace(afterConnect(pending.redirectTo || redirectTo));
  }

  function cancel() {
    clearGoogleConfirm();
    window.location.replace(notConnected("google", pending?.account.signedUpWith ?? ""));
  }

  return (
    <>
      <Explainer provider="google" email={pending.email} account={pending.account} />
      {error && <Alert tone="error">{error}</Alert>}
      <AccountCard account={pending.account} />
      <Button onClick={connect} disabled={busy}>
        {busy ? "Connecting…" : "Connect Google & continue"}
      </Button>
      <Button variant="text" onClick={cancel} disabled={busy}>
        Don&apos;t connect Google
      </Button>
    </>
  );
}
