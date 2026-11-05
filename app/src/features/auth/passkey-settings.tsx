"use client";

import { useCallback, useEffect, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { PasskeyIcon } from "@/components/ui/icons";
import { Loading, Skeleton } from "@/components/ui/skeleton";

import { approvePasskeyAction, listPasskeysAction, reauthenticateAction } from "./actions";
import { deviceLabel, loggedInRecently, passkeyErrorMessage, passkeysSupported } from "./passkeys";
import { supabaseBrowser } from "./supabase-client";

type Passkey = {
  id: string;
  friendly_name?: string;
  created_at: string;
  last_used_at?: string;
  approved: boolean;
};

const action =
  "h-9 shrink-0 rounded-lg px-3.5 text-sm leading-[1.45] font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link disabled:cursor-not-allowed disabled:opacity-45";
const secondary = `${action} border border-border bg-panel-2 text-text enabled:hover:bg-panel`;
const primary = `${action} bg-button text-button-text enabled:hover:bg-border`;

const day = new Intl.DateTimeFormat("en", { dateStyle: "medium" });
const when = (iso?: string) => (iso ? day.format(new Date(iso)) : null);

function Row({
  passkey,
  busy,
  onRename,
  onRemove,
}: {
  passkey: Passkey;
  busy: boolean;
  onRename: (name: string) => Promise<boolean>;
  onRemove: () => void;
}) {
  const [mode, setMode] = useState<"view" | "rename" | "remove">("view");
  const [name, setName] = useState(passkey.friendly_name ?? "");
  const label = passkey.friendly_name || "Passkey";
  const used = when(passkey.last_used_at);

  return (
    <li className="flex flex-col gap-2.5 px-3.5 py-3" data-testid="passkey-row">
      <div className="flex items-center gap-3">
        <span className="flex size-7 items-center justify-center text-text">
          <PasskeyIcon />
        </span>
        <div className="flex min-w-0 flex-1 flex-col gap-0.5 leading-[1.2]">
          <span className="truncate text-[15px] font-semibold text-text">{label}</span>
          <span className="truncate text-[13px] text-muted">
            {passkey.approved
              ? `Added ${when(passkey.created_at)} · ${used ? `Last used ${used}` : "Not used yet"}`
              : "Not confirmed: can't be used to log in. Remove it and add it again."}
          </span>
        </div>
        {mode === "view" && (
          <div className="flex gap-2">
            <button
              type="button"
              className={secondary}
              onClick={() => setMode("rename")}
              disabled={busy}
            >
              Rename
            </button>
            <button
              type="button"
              className={secondary}
              onClick={() => setMode("remove")}
              disabled={busy}
            >
              Remove
            </button>
          </div>
        )}
      </div>
      {mode === "rename" && (
        <form
          className="flex gap-2"
          onSubmit={async (e) => {
            e.preventDefault();
            if (await onRename(name.trim())) setMode("view");
          }}
        >
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={120}
            required
            autoFocus
            aria-label={`New name for ${label}`}
            className="h-9 min-w-0 flex-1 rounded-lg border border-border bg-white px-3 text-sm text-field-ink focus-visible:outline-2 focus-visible:outline-link"
          />
          <button type="submit" className={primary} disabled={busy || !name.trim()}>
            Save
          </button>
          <button
            type="button"
            className={secondary}
            onClick={() => setMode("view")}
            disabled={busy}
          >
            Cancel
          </button>
        </form>
      )}
      {mode === "remove" && (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted">
          <span className="flex-1">Remove this passkey? You can still log in your other ways.</span>
          <button type="button" className={primary} onClick={onRemove} disabled={busy}>
            Remove
          </button>
          <button
            type="button"
            className={secondary}
            onClick={() => setMode("view")}
            disabled={busy}
          >
            Cancel
          </button>
        </div>
      )}
    </li>
  );
}

/** Settings → Passkeys: the signed-in user's passkeys (Supabase Auth), add / rename / remove. */
export function PasskeySettings() {
  const [supported, setSupported] = useState<boolean | null>(null);
  const [passkeys, setPasskeys] = useState<Passkey[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [needsLogin, setNeedsLogin] = useState(false);
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string } | null>(null);

  // Through the server: it marks approved passkeys and removes ones never approved (server/passkeys.ts).
  const load = useCallback(async () => {
    const result = await listPasskeysAction();
    if ("error" in result) {
      if (result.error === "disabled") setSupported(false);
      else
        setNotice({
          tone: "error",
          text: "We couldn't load your passkeys. Reload the page to try again.",
        });
      setPasskeys([]);
      return;
    }
    setPasskeys(result.passkeys);
  }, []);

  useEffect(() => {
    // Browser capability and the user's passkeys are only known in the browser.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSupported(passkeysSupported());
    void load();
  }, [load]);

  async function add() {
    setBusy(true);
    setNotice(null);
    const auth = supabaseBrowser().auth;
    // A passkey outlives a password reset: adding one needs a login from the last few minutes.
    const { data: claims } = await auth.getClaims();
    if (!loggedInRecently(claims?.claims?.amr, Date.now() / 1000)) {
      setBusy(false);
      setNeedsLogin(true);
      return;
    }
    const { data, error } = await auth.registerPasskey();
    if (error || !data) {
      const text = passkeyErrorMessage(error, "create");
      if (text) setNotice({ tone: "error", text });
      setBusy(false);
      return;
    }
    // The server approves it only after checking the recent login itself; otherwise it deletes it again.
    const approval = await approvePasskeyAction(data.id);
    if (!approval.ok) {
      setBusy(false);
      await load();
      if (approval.needsLogin) setNeedsLogin(true);
      else
        setNotice({ tone: "error", text: "We couldn't add the passkey. Try again in a moment." });
      return;
    }
    if (!data.friendly_name) {
      await auth.passkey.update({
        passkeyId: data.id,
        friendlyName: deviceLabel(navigator.userAgent),
      });
    }
    await load();
    setBusy(false);
    setNotice({
      tone: "success",
      text: "Passkey added. Next time, log in with it from the login page.",
    });
  }

  async function rename(id: string, friendlyName: string): Promise<boolean> {
    setBusy(true);
    setNotice(null);
    const { error } = await supabaseBrowser().auth.passkey.update({ passkeyId: id, friendlyName });
    setBusy(false);
    if (error) {
      setNotice({ tone: "error", text: "We couldn't rename the passkey. Try again in a moment." });
      return false;
    }
    await load();
    return true;
  }

  async function remove(id: string) {
    setBusy(true);
    setNotice(null);
    const { error } = await supabaseBrowser().auth.passkey.delete({ passkeyId: id });
    if (error)
      setNotice({ tone: "error", text: "We couldn't remove the passkey. Try again in a moment." });
    else setNotice({ tone: "success", text: "Passkey removed." });
    await load();
    setBusy(false);
  }

  if (passkeys === null || supported === null) {
    return (
      <Loading className="rounded-[10px] border border-border bg-panel px-3.5 py-3">
        <div className="flex items-center gap-3">
          <Skeleton className="size-7 rounded-full" />
          <div className="flex flex-1 flex-col gap-1.5">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-3 w-48" />
          </div>
          <Skeleton className="h-9 w-28 rounded-lg" />
        </div>
      </Loading>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      {notice && <Alert tone={notice.tone}>{notice.text}</Alert>}
      <p className="text-sm leading-[1.45] text-muted">
        Log in with Face ID, Touch ID, Windows Hello or your phone instead of a password. A passkey
        stays on your device and can&apos;t be phished.
      </p>
      {passkeys.length > 0 && (
        <ul className="divide-y divide-border rounded-[10px] border border-border bg-panel">
          {passkeys.map((p) => (
            <Row
              key={p.id}
              passkey={p}
              busy={busy}
              onRename={(name) => rename(p.id, name)}
              onRemove={() => remove(p.id)}
            />
          ))}
        </ul>
      )}
      {needsLogin && (
        <div className="flex flex-col gap-3 rounded-lg border border-info/45 bg-info/10 px-3 py-2.5 text-sm leading-[1.45] text-text">
          <p>
            For your security, log in again before adding a passkey. A passkey keeps working even
            after a password reset, so we make sure it&apos;s really you.
          </p>
          <form action={reauthenticateAction}>
            <input type="hidden" name="redirectTo" value="/settings" />
            <button type="submit" className={primary}>
              Log in again
            </button>
          </form>
        </div>
      )}
      {supported ? (
        <div>
          <button type="button" className={primary} onClick={add} disabled={busy}>
            {busy
              ? "Waiting for your device…"
              : passkeys.length
                ? "Add another passkey"
                : "Add a passkey"}
          </button>
        </div>
      ) : (
        <p className="text-sm text-muted">Passkeys aren&apos;t available in this browser yet.</p>
      )}
    </div>
  );
}
