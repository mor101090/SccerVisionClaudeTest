import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Bar, Card, Chip, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { RATED_AFTER } from "@/features/home/rules";
import { rankingAround } from "@/server/club-world";
import { clubResults } from "@/server/home";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "World rankings" };

async function Rankings() {
  const { user, team } = await loadClub("/rankings");
  const ranking = await rankingAround(team.id);
  if (!ranking) {
    const played = (await clubResults(user.id, team.id)).length;
    return (
      <>
        <PageHeader crumb="Rankings" title="World rankings" sub="Coming soon" />
        <Card>
          <EmptyState
            icon="trophy"
            title="World ratings are coming soon"
            action={
              <ActionLink href="/play" icon="play">
                Play a match
              </ActionLink>
            }
          >
            Ratings aren&apos;t calculated yet. Your club has played {Math.min(played, RATED_AFTER)}{" "}
            of the {RATED_AFTER} matches a rating will need.
          </EmptyState>
          <Bar value={Math.min(played, RATED_AFTER) / RATED_AFTER} className="mx-auto w-64" />
        </Card>
      </>
    );
  }
  return (
    <>
      <PageHeader
        crumb="Rankings"
        title="World rankings"
        sub={`${ranking.total.toLocaleString("en-US")} rated clubs`}
      />
      <Card title="Around you" className="overflow-x-auto">
        <table className="w-full min-w-[520px] text-[13px]">
          <thead className="text-[11px] font-bold text-muted uppercase">
            <tr>
              <th className="w-24 py-2 text-left">Rank</th>
              <th className="text-left">Club</th>
              <th className="w-24 text-right">Rating</th>
              <th className="w-24 text-right">Change</th>
            </tr>
          </thead>
          <tbody>
            {ranking.rows.map((r) => {
              const d = r.previousRank ? r.previousRank - r.worldRank : 0;
              return (
                <tr key={r.teamId} className={r.teamId === team.id ? "bg-link/16" : ""}>
                  <td className="py-2.5 pl-1 font-bold text-text">
                    #{r.worldRank.toLocaleString("en-US")}
                  </td>
                  <td className="font-semibold text-text">{r.name}</td>
                  <td className="text-right text-muted">{r.rating.toLocaleString("en-US")}</td>
                  <td className="text-right">
                    <Chip tone={d > 0 ? "goal" : d < 0 ? "foul" : "muted"}>
                      {d > 0 ? `+${d}` : d === 0 ? "0" : `−${-d}`}
                    </Chip>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </Card>
    </>
  );
}

export default function RankingsPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Rankings />
    </Suspense>
  );
}
