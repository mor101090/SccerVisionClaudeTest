import type { Metadata } from "next";
import { Suspense } from "react";

import { Loading, Skeleton } from "@/components/ui/skeleton";
import { parsePlan } from "@/features/tactics/plan";
import { type EditorPlayer, TacticsEditor } from "@/features/tactics/tactics-editor";
import { overall, shortName } from "@/features/team/names";
import { loadClub } from "@/server/team-flow";
import { getSquad, type SquadPlayer } from "@/server/teams";

export const metadata: Metadata = { title: "Tactics" };

const toEditor = (s: SquadPlayer): EditorPlayer => ({
  number: s.p.shirt_number,
  name: s.p.name,
  short: shortName(s.p.name),
  stamina: s.p.stamina,
  fitness: s.p.fitness,
  ovr: overall(s.p.stats),
});

async function Tactics() {
  const { team } = await loadClub("/tactics");
  const squad = await getSquad(team.id);
  const starters = squad
    .filter((s) => s.starterSlot !== null)
    .sort((a, b) => a.starterSlot! - b.starterSlot!)
    .map(toEditor);
  const bench = squad
    .filter((s) => s.benchOrder !== null)
    .sort((a, b) => a.benchOrder! - b.benchOrder!)
    .map(toEditor);
  return (
    <TacticsEditor
      initial={parsePlan(team.plan ?? "", team.formation)}
      starters={starters}
      bench={bench}
    />
  );
}

export default function TacticsPage() {
  return (
    <Suspense
      fallback={
        <Loading className="flex flex-col gap-5">
          <Skeleton className="h-14 w-72" />
          <Skeleton className="h-10 w-full" />
          <div className="flex gap-5">
            <Skeleton className="h-[560px] w-[430px] rounded-2xl" />
            <Skeleton className="h-[560px] flex-1 rounded-2xl" />
          </div>
        </Loading>
      }
    >
      <Tactics />
    </Suspense>
  );
}
