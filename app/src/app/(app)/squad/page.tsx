import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { FilterTabs } from "@/components/app-shell/filter-tabs";
import { ActionLink, Bar, Card, Chip, Ovr, PageHeader } from "@/components/app-shell/ui";
import { Loading, Skeleton } from "@/components/ui/skeleton";
import { POSITION_BADGE, POSITION_ORDER } from "@/features/team/generator";
import { overall } from "@/features/team/names";
import { PositionBadge } from "@/features/team/ui/pieces";
import { loadClub } from "@/server/team-flow";
import { getSquad } from "@/server/teams";

export const metadata: Metadata = { title: "Squad" };

const LEVEL = {
  Excellent: ["Top", "goal"],
  Normal: ["Normal", "accent"],
  Weaker: ["Weaker", "chance"],
} as const;

async function Squad() {
  const { team } = await loadClub("/squad");
  const squad = await getSquad(team.id);
  const rows = squad.sort(
    (a, b) =>
      (a.starterSlot ?? 99) - (b.starterSlot ?? 99) || (a.benchOrder ?? 99) - (b.benchOrder ?? 99),
  );
  const avgAge = squad.length
    ? (squad.reduce((n, s) => n + s.p.age, 0) / squad.length).toFixed(1)
    : "—";
  const starters = squad.filter((s) => s.starterSlot !== null).length;
  const bench = squad.filter((s) => s.benchOrder !== null).length;
  const counts = Object.fromEntries(POSITION_ORDER.map((p) => [p, 0])) as Record<string, number>;
  for (const s of squad) counts[s.p.position] = (counts[s.p.position] ?? 0) + 1;
  return (
    <>
      <PageHeader
        crumb="Squad"
        title="Squad"
        sub={`${squad.length} players · avg age ${avgAge} · ${starters} starters, ${bench} on the bench, ${squad.length - starters - bench} reserves`}
        actions={
          <ActionLink href="/team/new/squad" variant="secondary">
            Edit lineup
          </ActionLink>
        }
      />
      <FilterTabs
        label="Positions"
        counts={counts}
        tabs={[
          { id: "", label: `All (${squad.length})` },
          ...POSITION_ORDER.map((p) => ({
            id: p,
            label: `${POSITION_BADGE[p].plural} (${counts[p] ?? 0})`,
          })),
        ]}
      >
        <Card className="overflow-x-auto p-2!">
          <table className="w-full min-w-[760px] text-[13px]">
            <thead className="border-b border-divider text-[11px] font-bold tracking-wide text-muted uppercase">
              <tr>
                <th className="w-10 px-3 py-2.5 text-left">#</th>
                <th className="text-left">Player</th>
                <th className="w-14 text-left">Pos</th>
                <th className="w-12 text-left">Age</th>
                <th className="w-24 text-left">Level</th>
                <th className="w-28 text-left">Stamina</th>
                <th className="w-24 text-left">Fitness</th>
                <th className="w-24 text-left">Status</th>
                <th className="w-12 text-left">OVR</th>
                <th className="w-6" />
              </tr>
            </thead>
            <tbody>
              {rows.map((s, i) => {
                const status =
                  s.starterSlot !== null
                    ? (["Starter", "accent"] as const)
                    : s.benchOrder !== null
                      ? (["Bench", "info"] as const)
                      : (["Reserve", "muted"] as const);
                const [lvl, tone] = LEVEL[s.p.level];
                return (
                  <tr
                    key={s.id}
                    data-filter={s.p.position}
                    className={`${i % 2 === 0 ? "bg-panel/60" : ""} hover:bg-panel`}
                  >
                    <td className="rounded-l-lg px-3 py-2.5 text-muted">{s.p.shirt_number}</td>
                    <td>
                      <Link
                        href={`/squad/${s.id}`}
                        className="font-semibold text-text hover:underline"
                      >
                        {s.p.name}
                      </Link>
                      {s.id === team.captainId && (
                        <span className="ml-1.5 text-xs font-bold text-card">(C)</span>
                      )}
                    </td>
                    <td>
                      <PositionBadge position={s.p.position} />
                    </td>
                    <td className="text-muted">{s.p.age}</td>
                    <td>
                      <Chip tone={tone}>{lvl}</Chip>
                    </td>
                    <td className="pr-4">
                      <Bar
                        value={s.p.stamina / 100}
                        tone={s.p.stamina < 50 ? "chance" : "accent"}
                      />
                    </td>
                    <td className="text-muted">{s.p.fitness}</td>
                    <td>
                      <Chip tone={status[1]}>{status[0]}</Chip>
                    </td>
                    <td>
                      <Ovr value={overall(s.p.stats)} />
                    </td>
                    <td className="rounded-r-lg">
                      <Link
                        href={`/squad/${s.id}`}
                        aria-label={`Open ${s.p.name}`}
                        className="text-muted hover:text-text"
                      >
                        <AppIcon name="chevronRight" size={16} />
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </Card>
      </FilterTabs>
    </>
  );
}

export default function SquadPage() {
  return (
    <Suspense
      fallback={
        <Loading className="flex flex-col gap-5">
          <Skeleton className="h-16 w-96" />
          <Skeleton className="h-[560px] rounded-2xl" />
        </Loading>
      }
    >
      <Squad />
    </Suspense>
  );
}
