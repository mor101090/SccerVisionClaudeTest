import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Bar, Card, OpensLater, PageHeader } from "@/components/app-shell/ui";
import { trainingOf } from "@/server/club-world";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "Training" };

const DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
/** The week a club trains to until its manager sets one (shown as the default, not as progress). */
const DEFAULT_WEEK: [string, number][] = [
  ["Recovery", 30],
  ["Pressing", 70],
  ["Set pieces", 50],
  ["Finishing", 80],
  ["Tactical shape", 60],
  ["Match day", 100],
  ["Rest", 0],
];

async function Training() {
  const { team } = await loadClub("/training");
  const rows = await trainingOf(team.id);
  const week = DAYS.map((_, i) => {
    const r = rows.find((x) => x.weekday === i);
    return r ? ([r.focus, r.intensity] as [string, number]) : DEFAULT_WEEK[i]!;
  });
  return (
    <>
      <PageHeader
        crumb="Training"
        title="Training"
        sub={rows.length ? "Your weekly plan" : "Default weekly plan"}
      />
      {!rows.length && (
        <OpensLater>
          Training is coming soon: it doesn&apos;t change your players yet. Below is the default
          week.
        </OpensLater>
      )}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-7">
        {week.map(([focus, intensity], i) => (
          <Card key={DAYS[i]} delay={i} className="gap-2.5! p-4!">
            <span className="text-xs font-bold tracking-[0.06em] text-muted uppercase">
              {DAYS[i]}
            </span>
            <span className="text-[15px] font-bold text-text">{focus}</span>
            <Bar
              value={intensity / 100}
              tone={intensity >= 90 ? "foul" : intensity >= 50 ? "accent" : "info"}
            />
            <span className="text-[11px] text-muted">
              {intensity ? `Intensity ${intensity}%` : "—"}
            </span>
          </Card>
        ))}
      </div>
    </>
  );
}

export default function TrainingPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Training />
    </Suspense>
  );
}
