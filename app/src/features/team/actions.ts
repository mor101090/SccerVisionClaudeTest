"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { z } from "zod";

import { formText } from "@/lib/form";
import { requireUser } from "@/server/auth";
import { isCountry } from "@/server/countries";
import {
  InviterNotFoundError,
  saveManager,
  setDisplayName,
  UsernameTakenError,
} from "@/server/profiles";
import { SquadApiError } from "@/server/squad-api";
import {
  finishTeam,
  makeSquad,
  saveClub,
  saveKits,
  setCaptain,
  setStarter,
  TeamError,
} from "@/server/teams";

import { NAME_POOLS } from "./generator";
import { KIT_COLOR_IDS, kitsValid } from "./kits";
import { fieldErrors, username } from "../auth/policy";

export type TeamFormState = {
  error?: string;
  fields?: Record<string, string>;
  values?: Record<string, string>;
} | null;

// ------------------------------------------------------------------ 1 manager

const managerSchema = z.object({
  username,
  nationality: z.string().refine(isCountry, "Pick your country."),
  invitedBy: z
    .string()
    .trim()
    .transform((v) => v || undefined)
    .pipe(username.optional()),
});

export async function saveManagerAction(
  _prev: TeamFormState,
  form: FormData,
): Promise<TeamFormState> {
  const user = await requireUser();
  const values = {
    username: formText(form, "username"),
    nationality: formText(form, "nationality"),
    invitedBy: formText(form, "invitedBy"),
  };
  const parsed = await managerSchema.safeParseAsync(Object.fromEntries(form));
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  try {
    await saveManager(user.id, parsed.data);
  } catch (err) {
    if (err instanceof UsernameTakenError) return { values, fields: { username: err.message } };
    if (err instanceof InviterNotFoundError) return { values, fields: { invitedBy: err.message } };
    throw err;
  }
  if (parsed.data.username !== user.name) await setDisplayName(user.id, parsed.data.username);
  redirect("/team/new/club");
}

// ------------------------------------------------------------------ 2 club

const clubSchema = z.object({
  name: z.string().trim().min(3, "At least 3 characters.").max(30, "At most 30 characters."),
  shortName: z
    .string()
    .trim()
    .toUpperCase()
    .regex(/^[A-Z]{3}$/, "3 letters."),
  namePools: z
    .array(z.enum(NAME_POOLS.map((p) => p.id) as [string, ...string[]]))
    .min(1, "Pick at least one."),
  crestPrompt: z.string().trim().max(120, "At most 120 characters.").optional(),
});

export async function saveClubAction(_prev: TeamFormState, form: FormData): Promise<TeamFormState> {
  const user = await requireUser();
  const values = {
    name: formText(form, "name"),
    shortName: formText(form, "shortName"),
    crestPrompt: formText(form, "crestPrompt"),
  };
  const parsed = clubSchema.safeParse({
    name: form.get("name"),
    shortName: form.get("shortName"),
    namePools: form.getAll("namePools"),
    crestPrompt: formText(form, "crestPrompt") || undefined,
  });
  if (!parsed.success) return { fields: fieldErrors(parsed.error), values };
  await saveClub(user.id, parsed.data as Parameters<typeof saveClub>[1]);
  redirect("/team/new/kits");
}

// ------------------------------------------------------------------ 3 kits

const color = z.enum(KIT_COLOR_IDS);
const kit = z.object({ shirt: color, trim: color, socks: color, number: color });
const kitsSchema = z.object({ home: kit, away: kit, keeper: kit });

export async function saveKitsAction(_prev: TeamFormState, form: FormData): Promise<TeamFormState> {
  const user = await requireUser();
  const raw = (k: string) =>
    Object.fromEntries(
      ["shirt", "trim", "socks", "number"].map((p) => [p, formText(form, `${k}.${p}`)]),
    );
  const parsed = kitsSchema.safeParse({
    home: raw("home"),
    away: raw("away"),
    keeper: raw("keeper"),
  });
  if (!parsed.success) return { error: "Pick a colour for every part of every kit." };
  if (!kitsValid(parsed.data))
    return { error: "Two kits are too close in colour. Pick another one." };
  try {
    await saveKits(user.id, parsed.data);
  } catch (err) {
    if (err instanceof TeamError) return { error: err.message };
    throw err;
  }
  redirect("/team/new/squad");
}

// ------------------------------------------------------------------ 4–6 squad

async function run(
  fn: () => Promise<void>,
  { revalidate = true }: { revalidate?: boolean } = {},
): Promise<TeamFormState> {
  try {
    await fn();
  } catch (err) {
    if (err instanceof TeamError || err instanceof SquadApiError) return { error: err.message };
    throw err;
  }
  if (revalidate) revalidatePath("/team/new/squad");
  return null;
}

/**
 * No revalidation: the Generating screen finishes its count and celebration first, then calls router.refresh()
 * itself. Revalidating here would ship the squad page with the action's response and cut the animation short.
 */
export async function generateSquadAction(): Promise<TeamFormState> {
  const user = await requireUser();
  return run(() => makeSquad(user.id, false), { revalidate: false });
}

export async function regenerateSquadAction(): Promise<TeamFormState> {
  const user = await requireUser();
  return run(() => makeSquad(user.id, true));
}

const id = z.uuid();

export async function setCaptainAction(playerId: string): Promise<TeamFormState> {
  const user = await requireUser();
  const pid = id.safeParse(playerId);
  if (!pid.success) return { error: "Unknown player." };
  return run(() => setCaptain(user.id, pid.data));
}

export async function setStarterAction(playerId: string): Promise<TeamFormState> {
  const user = await requireUser();
  const pid = id.safeParse(playerId);
  if (!pid.success) return { error: "Unknown player." };
  return run(() => setStarter(user.id, pid.data));
}

export async function startPlayingAction(): Promise<TeamFormState> {
  const user = await requireUser();
  const r = await run(() => finishTeam(user.id));
  if (r?.error) return r;
  redirect("/play");
}
