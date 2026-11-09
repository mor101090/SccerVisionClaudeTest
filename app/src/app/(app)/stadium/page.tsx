import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { euros } from "@/features/home/rules";
import { stadiumOf } from "@/server/club-world";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "Stadium" };

async function Stadium() {
  const { team } = await loadClub("/stadium");
  const s = await stadiumOf(team.id);
  if (!s) {
    return (
      <>
        <PageHeader crumb="Stadium" title="Home ground" />
        <Card>
          <EmptyState icon="stadium" title="Stadium management is coming soon">
            Capacity, roof, season tickets and ticket prices for {team.name}&apos;s home ground will
            be managed here.
          </EmptyState>
        </Card>
      </>
    );
  }
  const facts: [string, string][] = [
    ["Capacity", s.capacity.toLocaleString("en-US")],
    ["Roof", s.roof ? "Yes" : "None"],
    ["Season tickets", s.seasonTickets.toLocaleString("en-US")],
    ["Ticket price", euros(s.ticketPrice)],
  ];
  return (
    <>
      <PageHeader
        crumb="Stadium"
        title={s.name}
        sub={`Capacity ${s.capacity.toLocaleString("en-US")} · ticket ${euros(s.ticketPrice)}`}
      />
      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
        {facts.map(([k, v], i) => (
          <Card key={k} title={k} delay={i}>
            <span className="text-2xl font-extrabold text-text">{v}</span>
          </Card>
        ))}
      </div>
    </>
  );
}

export default function StadiumPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Stadium />
    </Suspense>
  );
}
