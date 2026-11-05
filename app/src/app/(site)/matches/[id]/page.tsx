import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { Suspense } from "react";
import { z } from "zod";

import { Loading, Skeleton } from "@/components/ui/skeleton";
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
  return (
    <>
      <h1 className="text-2xl font-semibold">
        {match.homeName} {match.homeScore} – {match.awayScore} {match.awayName}
      </h1>
      <p className="text-sm text-zinc-500">
        {match.mode} · seed {match.seed} · engine {match.engineVersion}
      </p>
      <ReplayClient matchId={match.id} />
    </>
  );
}

export default function ReplayPage({ params }: PageProps<"/matches/[id]">) {
  return (
    <main className="flex flex-1 flex-col items-center gap-4 p-4 text-zinc-50 sm:p-8">
      <Suspense
        fallback={
          <Loading className="flex w-full max-w-4xl flex-col items-center gap-4">
            <Skeleton className="h-8 w-72" />
            <Skeleton className="aspect-[105/68] w-full rounded-lg" />
          </Loading>
        }
      >
        <Replay params={params} />
      </Suspense>
    </main>
  );
}
