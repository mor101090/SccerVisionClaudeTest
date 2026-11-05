import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { Loading, Skeleton } from "@/components/ui/skeleton";
import { currentUser } from "@/server/auth";
import { getProfile } from "@/server/profiles";
import { engineTeamFor, getTeam } from "@/server/teams";

import { PlayClient } from "./play-client";

export const metadata: Metadata = { title: "Play" };

/** Signed in with a ready club: it can play at home. Signed in without one: a link to Create your team. */
async function Match() {
  const user = await currentUser();
  if (!user) return <PlayClient />;
  const [profile, team] = await Promise.all([getProfile(user.id), getTeam(user.id)]);
  const myTeam = await engineTeamFor(team, profile?.username ?? user.name);
  return (
    <>
      {!myTeam && (
        <p className="text-sm text-muted">
          {team ? "Your club is almost ready." : "No club yet."}{" "}
          <Link href="/team/new" className="font-semibold text-link hover:underline">
            {team ? "Finish your team" : "Create your team"}
          </Link>
        </p>
      )}
      <PlayClient myTeam={myTeam} />
    </>
  );
}

export default function PlayPage() {
  return (
    <main className="flex flex-1 flex-col items-center gap-6 p-4 sm:p-8">
      <h1 className="text-2xl font-semibold">Match</h1>
      <Suspense
        fallback={
          <Loading className="flex w-full max-w-4xl flex-col items-center gap-4">
            <Skeleton className="h-8 w-72" />
            <Skeleton className="aspect-[105/68] w-full rounded-lg" />
          </Loading>
        }
      >
        <Match />
      </Suspense>
    </main>
  );
}
