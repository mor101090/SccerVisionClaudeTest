"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { z } from "zod";

import { formText } from "@/lib/form";
import { requireUser } from "@/server/auth";
import { requestOrigin } from "@/server/auth-flow";
import { isCountry } from "@/server/countries";
import {
  DONATION_AMOUNTS,
  donationsOpen,
  MAX_EUROS,
  MIN_EUROS,
  startDonation,
} from "@/server/donations";
import { setFollow } from "@/server/leagues";
import { updateProfile, UsernameTakenError } from "@/server/profiles";
import { deleteClub, getTeam, TeamError, updateClubDetails } from "@/server/teams";

import { fieldErrors, username } from "../auth/policy";

export type FormState = {
  error?: string;
  fields?: Record<string, string>;
  values?: Record<string, string>;
  ok?: boolean;
} | null;

const optionalText = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `At most ${max} characters.`)
    .transform((v) => v || null);

// ------------------------------------------------------------------ profile

const profileSchema = z.object({
  displayName: z.string().trim().min(2, "At least 2 characters.").max(40, "At most 40 characters."),
  username,
  nationality: z.string().refine(isCountry, "Pick your country."),
  bio: optionalText(120),
  favouriteClub: optionalText(40),
  showProfile: z.boolean(),
  showHistory: z.boolean(),
});

export async function saveProfileAction(_prev: FormState, form: FormData): Promise<FormState> {
  const user = await requireUser();
  const values = Object.fromEntries(
    ["displayName", "username", "nationality", "bio", "favouriteClub"].map((k) => [
      k,
      formText(form, k),
    ]),
  );
  const parsed = await profileSchema.safeParseAsync({
    ...values,
    showProfile: form.get("showProfile") === "on",
    showHistory: form.get("showHistory") === "on",
  });
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  try {
    await updateProfile(user.id, parsed.data);
  } catch (err) {
    if (err instanceof UsernameTakenError) return { fields: { username: err.message }, values };
    throw err;
  }
  revalidatePath("/", "layout");
  redirect("/profile");
}

// ------------------------------------------------------------------ club settings

const clubSchema = z.object({
  name: z.string().trim().min(3, "At least 3 characters.").max(30, "At most 30 characters."),
  shortName: z
    .string()
    .trim()
    .toUpperCase()
    .regex(/^[A-Z]{3}$/, "3 letters."),
  city: optionalText(40),
  stadiumName: optionalText(40),
  crestPrompt: optionalText(120),
});

export async function saveClubSettingsAction(_prev: FormState, form: FormData): Promise<FormState> {
  const user = await requireUser();
  const values = Object.fromEntries(
    ["name", "shortName", "city", "stadiumName", "crestPrompt"].map((k) => [k, formText(form, k)]),
  );
  const parsed = clubSchema.safeParse(values);
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  try {
    await updateClubDetails(user.id, parsed.data);
  } catch (err) {
    if (err instanceof TeamError) return { error: err.message, values };
    throw err;
  }
  revalidatePath("/", "layout");
  return { ok: true, values };
}

export async function deleteClubAction(_prev: FormState, form: FormData): Promise<FormState> {
  const user = await requireUser();
  const team = await getTeam(user.id);
  if (!team) return { error: "You have no club." };
  if (formText(form, "confirm").trim() !== team.name)
    return { fields: { confirm: `Type ${team.name} exactly.` } };
  await deleteClub(user.id);
  revalidatePath("/", "layout");
  redirect("/team/new");
}

// ------------------------------------------------------------------ support

export async function donateAction(_prev: FormState, form: FormData): Promise<FormState> {
  const user = await requireUser();
  if (!donationsOpen())
    return { error: "Donations aren't open yet. Thank you for wanting to help!" };
  const preset = Number(formText(form, "amount"));
  const other = Number(formText(form, "other").replace(",", "."));
  const euros = formText(form, "other") ? other : preset;
  if (!formText(form, "other") && !(DONATION_AMOUNTS as readonly number[]).includes(preset))
    return { error: "Pick an amount." };
  if (!Number.isFinite(euros) || euros < MIN_EUROS || euros > MAX_EUROS)
    return { fields: { other: `Between €${MIN_EUROS} and €${MAX_EUROS}.` } };
  let url: string;
  try {
    url = await startDonation({
      userId: user.id,
      euros: Math.round(euros * 100) / 100,
      message: formText(form, "message").trim(),
      showBadge: form.get("showBadge") === "on",
      origin: await requestOrigin(),
    });
  } catch (err) {
    return {
      error: err instanceof Error ? err.message : "The payment page didn't open. Try again.",
    };
  }
  redirect(url);
}

// ------------------------------------------------------------------ leagues

export async function followLeagueAction(leagueId: string, follow: boolean): Promise<void> {
  const user = await requireUser();
  if (!z.uuid().safeParse(leagueId).success) return;
  await setFollow(user.id, leagueId, follow);
  revalidatePath("/leagues");
}
