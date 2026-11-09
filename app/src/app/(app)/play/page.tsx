import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";

import { Loading, Skeleton } from "@/components/ui/skeleton";
import type { HubData } from "@/features/match-day/play-hub";
import { parsePlan, STYLE } from "@/features/tactics/plan";
import { relativeTime } from "@/lib/relative-time";
import { currentUser } from "@/server/auth";
import { countMatches, listMatches } from "@/server/matches";
import { getProfile } from "@/server/profiles";
import { engineTeamFor, getTeam } from "@/server/teams";

import { PlayClient } from "./play-client";

export const metadata: Metadata = { title: "Play" };

/** Hub data: the manager's club (if it is ready to play), their recent results and how many replays are saved. */
async function loadHub(): Promise<HubData> {
  const user = await currentUser();
  if (!user)
    return { signedIn: false, club: null, clubUnfinished: false, recent: [], savedCount: 0 };
  const [profile, team, recent, savedCount] = await Promise.all([
    getProfile(user.id),
    getTeam(user.id),
    listMatches(user.id, 4),
    countMatches(user.id),
  ]);
  const engine = await engineTeamFor(team, profile?.username ?? user.name);
  await connection();
  const now = new Date();
  let tactics = "";
  if (engine) {
    const plan = parsePlan(
      engine.plan ?? `formation=${team?.formation ?? "4-4-2"}`,
      team?.formation ?? "4-4-2",
    );
    tactics = `${plan.formation} · ${STYLE.find(([v]) => v === plan.team.special)?.[1] ?? "Balanced"}`;
  }
  return {
    signedIn: true,
    club:
      engine && team
        ? {
            name: engine.name,
            text: engine.text,
            plan: engine.plan,
            kits: team.kits
              ? {
                  home: team.kits.home.shirt,
                  away: team.kits.away.shirt,
                  keeper: team.kits.keeper.shirt,
                }
              : null,
            tactics,
          }
        : null,
    clubUnfinished: !!team && !engine,
    recent: recent.map((m) => {
      const mine = team
        ? m.homeName === team.name
          ? 0
          : m.awayName === team.name
            ? 1
            : null
        : null;
      const [us, them] = mine === 1 ? [m.awayScore, m.homeScore] : [m.homeScore, m.awayScore];
      return {
        id: m.id,
        homeName: m.homeName,
        awayName: m.awayName,
        homeScore: m.homeScore,
        awayScore: m.awayScore,
        when: `${relativeTime(m.playedAt, now)} · ${m.mode === "online" ? "Online" : "Friendly"}`,
        result: mine === null ? null : us > them ? "W" : us < them ? "L" : "D",
      };
    }),
    savedCount,
  };
}

async function Hub() {
  return <PlayClient data={await loadHub()} />;
}

export default function PlayPage() {
  return (
    <Suspense
      fallback={
        <Loading className="flex flex-col gap-6">
          <Skeleton className="h-10 w-64" />
          <div className="grid gap-4 md:grid-cols-3">
            <Skeleton className="h-52 rounded-2xl" />
            <Skeleton className="h-52 rounded-2xl" />
            <Skeleton className="h-52 rounded-2xl" />
          </div>
          <Skeleton className="h-72 rounded-2xl" />
        </Loading>
      }
    >
      <Hub />
    </Suspense>
  );
}
