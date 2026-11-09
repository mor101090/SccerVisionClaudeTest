import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Card, EmptyState, FormDots, PageHeader } from "@/components/app-shell/ui";
import { leagueTable } from "@/server/home";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "League table" };

async function League() {
  const { team } = await loadClub("/league");
  const table = await leagueTable(team.id, 0);
  if (!table) {
    return (
      <>
        <PageHeader crumb="League table" title="League table" />
        <Card>
          <EmptyState
            icon="trophy"
            title="Leagues are coming soon"
            action={
              <ActionLink href="/play" icon="play">
                Play a friendly meanwhile
              </ActionLink>
            }
          >
            League play isn&apos;t part of the game yet. When it launches, your league table shows
            up here.
          </EmptyState>
        </Card>
      </>
    );
  }
  const n = table.all.length;
  return (
    <>
      <PageHeader crumb="League table" title={table.name} sub={`${n} clubs`} />
      <Card className="overflow-x-auto p-2!">
        <table className="w-full min-w-[640px] text-[13px]">
          <thead className="border-b border-divider text-[11px] font-bold text-muted uppercase">
            <tr>
              <th className="w-10 px-3 py-2.5 text-left">#</th>
              <th className="text-left">Club</th>
              {["P", "W", "D", "L", "Goals", "Pts"].map((h) => (
                <th key={h} className="w-14 text-right">
                  {h}
                </th>
              ))}
              <th className="w-32 pl-4 text-left">Form</th>
            </tr>
          </thead>
          <tbody>
            {table.all.map((r) => (
              <tr
                key={r.teamId}
                className={`${r.teamId === team.id ? "bg-link/16" : ""} ${r.position <= 2 ? "border-l-[3px] border-goal" : r.position > n - 2 ? "border-l-[3px] border-foul" : ""}`}
              >
                <td className="px-3 py-2.5 text-muted">{r.position}</td>
                <td className="font-semibold text-text">{r.name}</td>
                <td className="text-right text-muted">{r.played}</td>
                <td className="text-right">{r.won}</td>
                <td className="text-right">{r.drawn}</td>
                <td className="text-right">{r.lost}</td>
                <td className="text-right text-muted">
                  {r.goalsFor}:{r.goalsAgainst}
                </td>
                <td className="text-right font-bold text-text">{r.points}</td>
                <td className="pl-4">
                  <FormDots form={r.form.split("").slice(-5)} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </>
  );
}

export default function LeaguePage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <League />
    </Suspense>
  );
}
