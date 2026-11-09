import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { euros } from "@/features/home/rules";
import { financeOverview } from "@/server/club-world";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "Finances" };

const KIND: Record<string, string> = {
  tickets: "Ticket sales",
  sponsors: "Sponsorship",
  tv: "TV money",
  prize: "Prize money",
  player_wages: "Player wages",
  staff_wages: "Staff wages",
  transfer: "Transfers",
  construction: "Construction",
};

async function Finances() {
  const { team } = await loadClub("/finances");
  const f = await financeOverview(team.id);
  if (!f) {
    return (
      <>
        <PageHeader crumb="Finances" title="Finances" />
        <Card>
          <EmptyState icon="wallet" title="Finances are coming soon">
            Budgets, income and wages aren&apos;t part of the game yet. Friendlies cost nothing.
          </EmptyState>
        </Card>
      </>
    );
  }
  const income = f.entries.filter((e) => e.amount > 0).reduce((n, e) => n + e.amount, 0);
  const spend = f.entries.filter((e) => e.amount < 0).reduce((n, e) => n + e.amount, 0);
  const byKind = new Map<string, number>();
  for (const e of f.entries) byKind.set(e.kind, (byKind.get(e.kind) ?? 0) + e.amount);
  return (
    <>
      <PageHeader crumb="Finances" title="Finances" sub="Last 12 weeks" />
      <div className="grid gap-5 sm:grid-cols-3">
        <Card title="Balance">
          <span className="text-[28px] font-extrabold text-text">{euros(f.balance)}</span>
        </Card>
        <Card title="Income">
          <span className="text-[28px] font-extrabold text-goal">{euros(income)}</span>
        </Card>
        <Card title="Expenses">
          <span className="text-[28px] font-extrabold text-foul">{euros(spend)}</span>
        </Card>
      </div>
      <Card title="By category">
        {[...byKind].map(([k, v]) => (
          <div key={k} className="flex text-[13px]">
            <span className="flex-1 text-muted">{KIND[k] ?? k}</span>
            <span className={`font-bold ${v >= 0 ? "text-goal" : "text-foul"}`}>
              {v >= 0 ? "+" : ""}
              {euros(v)}
            </span>
          </div>
        ))}
      </Card>
    </>
  );
}

export default function FinancesPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Finances />
    </Suspense>
  );
}
