"use client";

import Link from "next/link";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { useAuthForm } from "@/features/auth/use-auth-form";
import type { Country } from "@/features/team/countries";
import { CountryField } from "@/features/team/ui/country-field";

import { saveProfileAction } from "./actions";
import { Switch } from "./switch";

export type EditValues = {
  displayName: string;
  username: string;
  nationality: string;
  bio: string;
  favouriteClub: string;
  showProfile: boolean;
  showHistory: boolean;
};

/** Figma "Profile – edit": name, username, country, about, privacy. Email and log-in methods live in Settings. */
export function EditProfileForm({
  initial,
  countries,
}: {
  initial: EditValues;
  countries: Country[];
}) {
  const { state, pending, formProps } = useAuthForm(saveProfileAction);
  const f = state?.fields ?? {};
  const v = (k: keyof EditValues) =>
    (state?.values?.[k] as string | undefined) ?? (initial[k] as string);
  return (
    <form {...formProps} className="flex flex-col gap-4">
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <section className="flex flex-col gap-4 rounded-2xl border border-border bg-panel-2 p-5">
        <h2 className="text-[13px] font-bold tracking-[0.06em] text-muted uppercase">
          Name & country
        </h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Display name"
            name="displayName"
            defaultValue={v("displayName")}
            helper="Shown on your profile and in matches"
            error={f.displayName}
            maxLength={40}
            required
          />
          <TextField
            label="Username"
            name="username"
            autoComplete="username"
            defaultValue={v("username")}
            helper="3–20 letters, numbers or _. Your invite link and profile address use it."
            error={f.username}
            required
          />
          <CountryField
            label="Country"
            name="nationality"
            defaultValue={v("nationality")}
            countries={countries}
            helper="Your flag next to your name"
            error={f.nationality}
          />
          <TextField
            label="Favourite real club (optional)"
            name="favouriteClub"
            defaultValue={v("favouriteClub")}
            error={f.favouriteClub}
            maxLength={40}
          />
        </div>
        <TextField
          label="About you (optional)"
          name="bio"
          defaultValue={v("bio")}
          helper="Up to 120 characters"
          error={f.bio}
          maxLength={120}
        />
      </section>
      <section className="flex flex-col gap-2 rounded-2xl border border-border bg-panel-2 p-5">
        <h2 className="text-[13px] font-bold tracking-[0.06em] text-muted uppercase">Privacy</h2>
        <Switch
          name="showProfile"
          label="Other managers can see my profile"
          hint="Off: they only see your club."
          defaultChecked={initial.showProfile}
        />
        <Switch
          name="showHistory"
          label="Show my match results on my profile"
          defaultChecked={initial.showHistory}
        />
        <p className="text-xs text-muted">Your email is never shown to other managers.</p>
      </section>
      <div className="flex flex-wrap justify-end gap-2">
        <Link
          href="/profile"
          className="inline-flex h-11 items-center rounded-lg border border-border bg-panel-2 px-4 text-[15px] font-semibold text-text hover:bg-panel"
        >
          Cancel
        </Link>
        <Button type="submit" disabled={pending} className="w-auto!">
          {pending ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}
