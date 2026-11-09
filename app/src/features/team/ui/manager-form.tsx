"use client";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { useAuthForm } from "@/features/auth/use-auth-form";

import { saveManagerAction } from "../actions";
import type { Country } from "../countries";
import { CountryField } from "./country-field";

/** Figma: Create team – 1 Manager. */
export function ManagerForm({
  username,
  nationality,
  countries,
  invitedBy,
}: {
  username: string;
  /** From an invite link (/i/<username>), if the manager opened one. */
  invitedBy?: string;
  nationality: string | null;
  /** From flagcdn.com, loaded on the server. */
  countries: Country[];
}) {
  const { state, pending, formProps } = useAuthForm(saveManagerAction);
  const f = state?.fields ?? {};

  return (
    <form {...formProps} className="flex flex-col gap-[22px]">
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <TextField
        label="Username"
        name="username"
        autoComplete="username"
        defaultValue={state?.values?.username ?? username}
        helper="3–20 letters, numbers or _. Shown on the league table and to friends you play online."
        error={f.username}
        required
      />
      <CountryField
        label="Nationality"
        name="nationality"
        defaultValue={state?.values?.nationality ?? nationality ?? ""}
        countries={countries}
        helper="Your flag, next to your name."
        error={f.nationality}
      />
      <TextField
        label="User that invited you (optional)"
        name="invitedBy"
        placeholder="Their username"
        defaultValue={state?.values?.invitedBy ?? invitedBy}
        helper="They earn the Recruiter badge once you play your first match."
        error={f.invitedBy}
      />
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Continue"}
      </Button>
    </form>
  );
}
