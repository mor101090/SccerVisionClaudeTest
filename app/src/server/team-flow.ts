import "server-only";

import { redirect } from "next/navigation";

import { currentUser } from "./auth";
import { getProfile, isComplete, profileForRequest } from "./profiles";
import { currentStep, getTeam, type Step, teamForRequest } from "./teams";

const ORDER: Step[] = ["manager", "club", "kits", "squad"];

/**
 * Loads the wizard state for a step page. Signed out → login; no username yet → "Almost there";
 * a step later than the manager has reached → that step; a finished club → its squad stays open.
 */
export async function loadStep(step: Step) {
  const user = await currentUser();
  if (!user) redirect(`/auth/sign-in?redirectTo=${encodeURIComponent(`/team/new/${step}`)}`);
  const [profile, team] = await Promise.all([profileForRequest(user.id), teamForRequest(user.id)]);
  if (!isComplete(profile)) redirect(`/auth/finish?redirectTo=${encodeURIComponent("/team/new")}`);
  const reached = currentStep(profile, team);
  if (ORDER.indexOf(step) > ORDER.indexOf(reached)) redirect(`/team/new/${reached}`);
  return { user, profile: profile!, team, reached };
}

/**
 * For the club pages (Squad, Tactics, …): signed out → login (back here after), no username → "Almost there",
 * no finished club → Create your team.
 */
export async function loadClub(path: string) {
  const user = await currentUser();
  if (!user) redirect(`/auth/sign-in?redirectTo=${encodeURIComponent(path)}`);
  const [profile, team] = await Promise.all([getProfile(user.id), getTeam(user.id)]);
  if (!isComplete(profile)) redirect(`/auth/finish?redirectTo=${encodeURIComponent(path)}`);
  if (!team || team.status !== "ready") redirect("/team/new");
  return { user, profile: profile!, team };
}
