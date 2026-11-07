import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { z } from "zod";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, FormDots, PageHeader } from "@/components/app-shell/ui";
import { FollowButton } from "@/features/profile/follow-button";
import { Row } from "@/features/profile/ui";
import { requireUser } from "@/server/auth";
import { leagueDetail } from "@/server/leagues";

export const metadata: Metadata = { title: "League" };

const day = (d: Date) =>
  d.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short" });

/** Figma "League – another league": table, next fixtures and results. */
async function League({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!z.uuid().safeParse(id).success) notFound();
  const user = await requireUser();
  const d = await leagueDetail(id, user.id);
  if (!d) notFound();
  return (
    <>
      <PageHeader
        crumb="Leagues"
        title={d.league.name}
        sub={`${d.table.length} ${d.table.length === 1 ? "club" : "clubs"} · Season ${d.league.season}`}
        actions={<FollowButton leagueId={d.league.id} following={d.following} />}
      />
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
        <Card className="overflow-x-auto p-2!">
          {d.table.length ? (
            <table className="w-full min-w-[620px] text-left text-[13px]">
              <thead>
                <tr className="border-b border-divider text-[11px] tracking-[0.06em] text-muted uppercase">
                  {["#", "Club", "P", "W", "D", "L", "GD", "Pts", "Form"].map((h) => (
                    <th key={h} className="px-3 py-2 font-bold">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {d.table.map((r) => (
                  <tr key={r.teamId} className="border-b border-divider last:border-0">
                    <td className="px-3 py-2.5 text-muted tabular-nums">{r.position}</td>
                    <td className="px-3 py-2.5">
                      <Link
                        href={`/clubs/${r.teamId}`}
                        className="font-semibold text-text hover:underline"
                      >
                        {r.name}
                      </Link>
                    </td>
                    {[r.played, r.won, r.drawn, r.lost].map((v, i) => (
                      <td key={i} className="px-3 py-2.5 tabular-nums">
                        {v}
                      </td>
                    ))}
                    <td className="px-3 py-2.5 tabular-nums">
                      {r.goalsFor - r.goalsAgainst > 0 ? "+" : ""}
                      {r.goalsFor - r.goalsAgainst}
                    </td>
                    <td className="px-3 py-2.5 font-bold tabular-nums">{r.points}</td>
                    <td className="px-3 py-2.5">
                      <FormDots form={r.form.split("")} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <EmptyState icon="trophy" title="The season hasn’t started">
              The table appears after the first matchday.
            </EmptyState>
          )}
        </Card>
        <div className="flex flex-col gap-4">
          <Card title="Next fixtures" icon="calendar">
            {d.upcoming.length ? (
              <div>
                {d.upcoming.map((f) => (
                  <Row
                    key={f.id}
                    title={`${f.homeName} v ${f.awayName}`}
                    sub={`${day(f.kickoffAt)}${f.round ? ` · Round ${f.round}` : ""}`}
                  />
                ))}
              </div>
            ) : (
              <p className="text-[13px] text-muted">No fixtures scheduled.</p>
            )}
          </Card>
          <Card title="Results" icon="ball">
            {d.results.length ? (
              <div>
                {d.results.map((f) => (
                  <Row
                    key={f.id}
                    title={`${f.homeName} ${f.homeScore ?? "–"}–${f.awayScore ?? "–"} ${f.awayName}`}
                    sub={day(f.kickoffAt)}
                  />
                ))}
              </div>
            ) : (
              <p className="text-[13px] text-muted">No results yet.</p>
            )}
          </Card>
        </div>
      </div>
    </>
  );
}

export default function LeaguePage({ params }: PageProps<"/leagues/[id]">) {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <League params={params} />
    </Suspense>
  );
}
