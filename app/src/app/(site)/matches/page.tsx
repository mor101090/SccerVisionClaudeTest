import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { Loading, Skeleton } from "@/components/ui/skeleton";
import { currentUser } from "@/server/auth";
import { listMatches } from "@/server/matches";

export const metadata: Metadata = { title: "My matches" };

async function MatchList() {
  const user = await currentUser();
  if (!user) redirect("/auth/sign-in?redirectTo=/matches");
  const rows = await listMatches(user.id);
  if (rows.length === 0) {
    return (
      <p className="text-zinc-400">
        No saved matches yet.{" "}
        <Link className="text-emerald-400 underline" href="/play">
          Play one
        </Link>
        .
      </p>
    );
  }
  return (
    <ul
      className="divide-y divide-zinc-800 rounded border border-zinc-800"
      data-testid="match-list"
    >
      {rows.map((m) => (
        <li key={m.id}>
          <Link
            href={`/matches/${m.id}`}
            className="flex items-center justify-between gap-4 px-4 py-3 hover:bg-zinc-900"
          >
            <span>
              {m.homeName}{" "}
              <strong className="font-mono">
                {m.homeScore} – {m.awayScore}
              </strong>{" "}
              {m.awayName}
            </span>
            <span className="text-sm text-zinc-500">
              {m.mode} · seed {m.seed} · {m.playedAt.toISOString().slice(0, 16).replace("T", " ")}
            </span>
          </Link>
        </li>
      ))}
    </ul>
  );
}

export default function MatchesPage() {
  return (
    <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6 p-4 text-zinc-50 sm:p-8">
      <h1 className="text-2xl font-semibold">My matches</h1>
      <Suspense
        fallback={
          <Loading className="divide-y divide-border rounded border border-border">
            {[0, 1, 2, 3].map((i) => (
              <div key={i} className="flex items-center justify-between gap-4 px-4 py-3">
                <Skeleton className="h-4 w-56" />
                <Skeleton className="h-3.5 w-40" />
              </div>
            ))}
          </Loading>
        }
      >
        <MatchList />
      </Suspense>
    </main>
  );
}
