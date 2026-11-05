import "server-only";

import { redirect } from "next/navigation";

import { currentUser } from "./auth";
import { getProfile, isComplete } from "./profiles";
import { currentStep, getTeam, type Step } from "./teams";

const ORDER: Step[] = ["manager", "club", "kits", "squad"];

/**
 * Loads the wizard state for a step page. Signed out → login; no username yet → "Almost there";
 * a step later than the manager has reached → that step; a finished club → its squad stays open.
 */
export async function loadStep(step: Step) {
  const user = await currentUser();
  if (!user) redirect(`/auth/sign-in?redirectTo=${encodeURIComponent(`/team/new/${step}`)}`);
  const profile = await getProfile(user.id);
  if (!isComplete(profile)) redirect(`/auth/finish?redirectTo=${encodeURIComponent("/team/new")}`);
  const team = await getTeam(user.id);
  const reached = currentStep(profile, team);
  if (ORDER.indexOf(step) > ORDER.indexOf(reached)) redirect(`/team/new/${reached}`);
  return { user, profile: profile!, team, reached };
}
