"use server";

import { eq } from "drizzle-orm";
import { revalidatePath } from "next/cache";

import { planTextIsSafe } from "@/features/lobby/plan-text";
import { requireUser } from "@/server/auth";
import { db, schema } from "@/server/db";
import { getTeam } from "@/server/teams";

import { parsePlan, planText } from "./plan";

export type SaveTacticsState = { error?: string; savedAt?: number } | undefined;

/** Saves the manager's tactics as the club's default plan (used by /play and every friendly). */
export async function saveTacticsAction(text: string): Promise<SaveTacticsState> {
  const user = await requireUser();
  if (typeof text !== "string" || text.length > 20_000 || !planTextIsSafe(text))
    return { error: "These tactics could not be read. Reload the page and try again." };
  const team = await getTeam(user.id);
  if (!team) return { error: "Create your club first." };
  // Re-serialize: only fields the engine knows, values clamped to their ranges.
  const plan = parsePlan(text, team.formation);
  await db()
    .update(schema.teams)
    .set({ plan: planText(plan), formation: plan.formation })
    .where(eq(schema.teams.id, team.id));
  revalidatePath("/tactics");
  revalidatePath("/home");
  return { savedAt: Date.now() };
}
