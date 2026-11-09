import type { Metadata } from "next";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Bar, Card, OpensLater, PageHeader } from "@/components/app-shell/ui";
import { euros, initials } from "@/features/home/rules";
import { staffOf } from "@/server/club-world";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "Coaches" };

const ROLES = [
  ["head_coach", "Head coach", "Tactics and motivation: sets how well the team follows your plan."],
  ["assistant", "Assistant coach", "Runs training sessions and reports on form."],
  ["goalkeeping", "Goalkeeping coach", "Improves your keepers' handling and reflexes."],
  ["fitness", "Fitness coach", "Stamina and recovery between matches; fewer injuries."],
  ["scout", "Chief scout", "Finds and judges players for the transfer market."],
  ["youth", "Youth coach", "Develops young players faster."],
] as const;

async function Coaches() {
  const { team } = await loadClub("/coaches");
  const rows = await staffOf(team.id);
  const wages = rows.reduce((n, s) => n + s.wage, 0);
  return (
    <>
      <PageHeader
        crumb="Coaches"
        title="Coaches"
        sub={`${rows.length} of ${ROLES.length} staff slots used${rows.length ? ` · staff wages ${euros(wages)} / week` : ""}`}
      />
      {!rows.length && (
        <OpensLater>
          Hiring staff is coming soon. These are the six roles you will be able to fill.
        </OpensLater>
      )}
      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
        {ROLES.map(([key, label, about], i) => {
          const s = rows.find((r) => r.role === key);
          if (!s) {
            return (
              <div
                key={key}
                className="flex min-h-[220px] animate-sv-enter flex-col items-center justify-center gap-2.5 rounded-2xl border-[1.5px] border-dashed border-border p-5 text-center"
                style={{ animationDelay: `${i * 40}ms` }}
              >
                <span className="flex size-11 items-center justify-center rounded-full bg-panel-2 text-muted">
                  <AppIcon name="coach" />
                </span>
                <span className="text-sm font-semibold text-text">Open slot: {label}</span>
                <span className="max-w-60 text-xs text-muted">{about}</span>
              </div>
            );
          }
          return (
            <Card key={key} delay={i}>
              <div className="flex items-center gap-3">
                <span className="flex size-12 items-center justify-center rounded-full bg-link text-[15px] font-bold text-white">
                  {initials(s.name)}
                </span>
                <span>
                  <span className="block text-[15px] font-bold text-text">{s.name}</span>
                  <span className="block text-xs text-muted">{label}</span>
                </span>
              </div>
              {Object.entries(s.ratings).map(([k, v]) => (
                <div key={k} className="flex items-center gap-2.5 text-xs">
                  <span className="w-24 text-muted capitalize">{k}</span>
                  <Bar value={v / 20} className="flex-1" />
                  <span className="w-5 text-right font-bold text-text">{v}</span>
                </div>
              ))}
              <p className="border-t border-divider pt-3 text-xs text-muted">
                {euros(s.wage)}/wk
                {s.contractUntil ? ` · until ${s.contractUntil.getFullYear()}` : ""}
              </p>
            </Card>
          );
        })}
      </div>
    </>
  );
}

export default function CoachesPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Coaches />
    </Suspense>
  );
}
