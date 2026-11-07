import "server-only";

import { redirect } from "next/navigation";

import { achievementsOf, recordOf } from "@/features/profile/achievements";

import { currentUser } from "./auth";
import { getCountries } from "./countries";
import { clubOf, invitedManagers, isSupporter, managerById, matchFacts } from "./managers";
import { getSquad } from "./teams";

/** Everything the profile pages show about one manager, from stored rows. */
export async function profileData(userId: string) {
  const manager = await managerById(userId);
  if (!manager) return null;
  const club = await clubOf(userId);
  const [{ facts, recent }, invited, supporter, squad, countries] = await Promise.all([
    matchFacts(userId, club?.name ?? null),
    invitedManagers(userId),
    isSupporter(userId),
    club ? getSquad(club.id) : Promise.resolve([]),
    getCountries(),
  ]);
  const invitedWhoPlayed = invited.filter((i) => i.played).length;
  return {
    manager,
    club,
    country: countries.find((c) => c.code === manager.nationality)?.name ?? null,
    record: recordOf(facts),
    recent,
    invited,
    supporter,
    achievements: achievementsOf(facts, {
      squadComplete: squad.length >= 22,
      invitedWhoPlayed,
      supporter,
    }),
  };
}

/** The signed-in manager's own data; a manager without a username finishes sign-up first. */
export async function myProfileData(path: string) {
  const user = await currentUser();
  if (!user) redirect(`/auth/sign-in?redirectTo=${encodeURIComponent(path)}`);
  const data = await profileData(user.id);
  if (!data) redirect(`/auth/finish?redirectTo=${encodeURIComponent(path)}`);
  return { user, ...data };
}
