"use client";

import { useEffect, useRef, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { useAuthForm } from "@/features/auth/use-auth-form";

import { deleteClubAction, saveClubSettingsAction } from "./actions";

export type ClubValues = {
  name: string;
  shortName: string;
  city: string;
  stadiumName: string;
  crestPrompt: string;
};

/** Figma "Club settings – edit team": identity fields. */
export function ClubSettingsForm({ initial }: { initial: ClubValues }) {
  const { state, pending, formProps } = useAuthForm(saveClubSettingsAction);
  const f = state?.fields ?? {};
  const v = (k: keyof ClubValues) => (state?.values?.[k] as string | undefined) ?? initial[k];
  return (
    <form {...formProps} className="flex flex-col gap-4">
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      {state?.ok && <Alert tone="success">Club settings saved.</Alert>}
      <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_200px]">
        <TextField
          label="Club name"
          name="name"
          defaultValue={v("name")}
          helper="In matches, tables and search"
          error={f.name}
          maxLength={30}
          required
        />
        <TextField
          label="Short name"
          name="shortName"
          defaultValue={v("shortName")}
          helper="3 letters, on the scoreboard"
          error={f.shortName}
          maxLength={3}
          required
        />
        <TextField
          label="Home city (optional)"
          name="city"
          defaultValue={v("city")}
          error={f.city}
          maxLength={40}
        />
        <TextField
          label="Ground (optional)"
          name="stadiumName"
          defaultValue={v("stadiumName")}
          error={f.stadiumName}
          maxLength={40}
        />
      </div>
      <TextField
        label="Crest description (optional)"
        name="crestPrompt"
        defaultValue={v("crestPrompt")}
        helper="What your crest shows, up to 120 characters. Empty: the simple shield."
        error={f.crestPrompt}
        maxLength={120}
      />
      <div className="flex justify-end">
        <Button type="submit" disabled={pending} className="w-auto!">
          {pending ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}

/** Danger zone: delete the club after typing its name (Figma "Club settings – delete confirm"). */
export function DeleteClub({ clubName, matches }: { clubName: string; matches: number }) {
  const [open, setOpen] = useState(false);
  const { state, pending, formProps } = useAuthForm(deleteClubAction);
  const [typed, setTyped] = useState("");
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    if (open) dialog.current?.showModal();
    else dialog.current?.close();
  }, [open]);
  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="inline-flex h-10 items-center self-start rounded-lg bg-foul px-4 text-sm font-semibold text-white hover:brightness-110 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link"
      >
        Delete club…
      </button>
      <dialog
        ref={dialog}
        onClose={() => setOpen(false)}
        aria-labelledby="delete-club-title"
        className="m-auto w-[min(460px,calc(100vw-32px))] rounded-2xl border border-border bg-panel p-6 text-text backdrop:bg-overlay/60"
      >
        <form {...formProps} className="flex flex-col gap-4">
          <h2 id="delete-club-title" className="text-xl font-extrabold">
            Delete {clubName}?
          </h2>
          <p className="text-[13px] leading-5 text-muted">
            The club, its players and tactics are deleted. Your {matches} saved{" "}
            {matches === 1 ? "match stays" : "matches stay"} in My Games, and your profile stays.
            This can’t be undone.
          </p>
          {state?.error && <Alert tone="error">{state.error}</Alert>}
          <TextField
            label="Type the club name to confirm"
            name="confirm"
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
            error={state?.fields?.confirm}
            autoComplete="off"
          />
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="inline-flex h-11 items-center rounded-lg border border-border bg-panel-2 px-4 font-semibold hover:bg-border"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={pending || typed.trim() !== clubName}
              className="inline-flex h-11 items-center rounded-lg bg-foul px-4 font-semibold text-white hover:brightness-110 disabled:opacity-45"
            >
              {pending ? "Deleting…" : "Delete club"}
            </button>
          </div>
        </form>
      </dialog>
    </>
  );
}
