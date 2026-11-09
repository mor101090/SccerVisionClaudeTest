import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { Suspense } from "react";
import { z } from "zod";

import { MatchSkeleton } from "@/components/ui/skeleton";
import type { FinalHeader } from "@/features/match-day/full-time";
import { currentUser } from "@/server/auth";
import { getMatch } from "@/server/matches";

import { ReplayClient } from "./replay-client";

export const metadata: Metadata = { title: "Replay" };

async function Replay({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await currentUser();
  if (!user) redirect(`/auth/sign-in?redirectTo=/matches/${id}`);
  if (!z.uuid().safeParse(id).success) notFound();
  const match = await getMatch(user.id, id);
  if (!match) notFound();
  const day = match.playedAt.toLocaleDateString("en-GB", {
    weekday: "short",
    day: "numeric",
    month: "short",
  });
  return (
    <>
      <h1 className="sr-only">
        {match.homeName} {match.homeScore} – {match.awayScore} {match.awayName}
      </h1>
      <ReplayClient
        matchId={match.id}
        summary={(match.summary as FinalHeader) ?? null}
        label={`Replay · seed ${match.seed} · ${day}`}
      />
    </>
  );
}

export default function ReplayPage({ params }: PageProps<"/matches/[id]">) {
  return (
    <Suspense fallback={<MatchSkeleton />}>
      <Replay params={params} />
    </Suspense>
  );
}
