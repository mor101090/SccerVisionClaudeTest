import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Suspense } from "react";

import { Bar, Card, Chip, Ovr, PageHeader } from "@/components/app-shell/ui";
import { Loading, Skeleton } from "@/components/ui/skeleton";
import { initials } from "@/features/home/rules";
import { DETAIL_GROUPS } from "@/features/team/detail-groups";
import { positionLabel } from "@/features/team/engine-team";
import { POSITION_BADGE } from "@/features/team/generator";
import { overall } from "@/features/team/names";
import { loadClub } from "@/server/team-flow";
import { getSquad } from "@/server/teams";

export const metadata: Metadata = { title: "Player" };

async function Player({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const { team } = await loadClub(`/squad/${id}`);
  const s = (await getSquad(team.id)).find((x) => x.id === id);
  if (!s) notFound();
  const p = s.p;
  const status = s.starterSlot !== null ? "Starter" : s.benchOrder !== null ? "Bench" : "Reserve";
  const info: [string, string][] = [
    ["Age", String(p.age)],
    ["Height", `${(p.height / 100).toFixed(2)} m`],
    [
      "Preferred foot",
      `${p.preferred_foot.dominant_foot} · weak foot ${"★".repeat(p.preferred_foot.weak_foot_stars ?? 1)}`,
    ],
    ["Stamina", String(p.stamina)],
    ["Fitness", p.fitness],
    ["Shirt", `#${p.shirt_number}`],
    ["Nationality", p.nationality],
  ];
  return (
    <>
      <PageHeader
        crumb={p.name}
        title={p.name}
        sub={`#${p.shirt_number} · ${POSITION_BADGE[p.position].label} · ${p.age} · ${p.nationality}`}
      />
      <div className="flex flex-col gap-5 lg:flex-row lg:items-start">
        <Card className="items-center lg:w-[320px]">
          <span className="flex size-[120px] items-center justify-center rounded-full border-2 border-link bg-panel text-4xl font-extrabold text-text">
            {initials(p.name)}
          </span>
          <h2 className="text-xl font-extrabold text-text">{p.name}</h2>
          <div className="flex flex-wrap justify-center gap-1.5">
            <Chip tone="info">{positionLabel(p)}</Chip>
            <Chip
              tone={p.level === "Excellent" ? "goal" : p.level === "Weaker" ? "chance" : "accent"}
            >
              {p.level}
            </Chip>
            <Chip tone="accent">{status}</Chip>
            {s.id === team.captainId && <Chip tone="card">Captain</Chip>}
          </div>
          <div className="flex items-center gap-2 text-sm text-muted">
            Overall <Ovr value={overall(p.stats)} />
          </div>
          <dl className="grid w-full grid-cols-[1fr_auto] gap-y-2 border-t border-divider pt-4 text-[13px]">
            {info.map(([k, v]) => (
              <div key={k} className="contents">
                <dt className="text-muted">{k}</dt>
                <dd className="text-right font-semibold text-text">{v}</dd>
              </div>
            ))}
          </dl>
        </Card>
        <div className="grid flex-1 gap-5 md:grid-cols-2">
          {DETAIL_GROUPS.filter((g) => p.stats[g.group]).map((g, i) => (
            <Card key={g.group} title={g.title.toLowerCase()} delay={i}>
              {g.keys.map(([k, label]) => {
                const v = p.stats[g.group]?.[k];
                if (v === undefined) return null;
                return (
                  <div key={k} className="flex items-center gap-2.5 text-[13px]">
                    <span className="w-[110px] text-muted">{label}</span>
                    <Bar
                      value={v / 100}
                      tone={v >= 80 ? "goal" : v >= 65 ? "accent" : v >= 50 ? "card" : "chance"}
                      className="flex-1"
                    />
                    <span className="w-7 text-right font-bold text-text">{v}</span>
                  </div>
                );
              })}
            </Card>
          ))}
        </div>
      </div>
    </>
  );
}

export default function PlayerPage(props: PageProps<"/squad/[id]">) {
  return (
    <Suspense
      fallback={
        <Loading className="flex flex-col gap-5 lg:flex-row">
          <Skeleton className="h-[520px] rounded-2xl lg:w-[320px]" />
          <Skeleton className="h-[520px] flex-1 rounded-2xl" />
        </Loading>
      }
    >
      <Player params={props.params} />
    </Suspense>
  );
}
