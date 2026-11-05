import type { Metadata } from "next";
import { Suspense } from "react";

import { NAME_POOLS } from "@/features/team/generator";
import { Generating } from "@/features/team/ui/generating";
import { Stepper } from "@/features/team/ui/pieces";
import { SquadView } from "@/features/team/ui/squad-view";
import { SquadSkeleton } from "@/features/team/ui/team-skeleton";
import { loadStep } from "@/server/team-flow";
import { getSquad } from "@/server/teams";

export const metadata: Metadata = { title: "Your squad" };

function listPools(ids: string[]): string {
  const labels = NAME_POOLS.filter((p) => ids.includes(p.id)).map((p) => p.label);
  return labels.length > 1
    ? `${labels.slice(0, -1).join(", ")} and ${labels.at(-1)}`
    : (labels[0] ?? "");
}

async function Step() {
  const { team, profile } = await loadStep("squad");
  const squad = team ? await getSquad(team.id) : [];
  if (!team?.kits || squad.length === 0)
    return <Generating pools={listPools(team?.namePools ?? [])} />;
  return (
    <SquadView
      manager={profile.username ?? ""}
      squad={squad}
      team={{
        name: team.name,
        shortName: team.shortName,
        kits: team.kits,
        regenerationsLeft: team.regenerationsLeft,
        captainId: team.captainId,
      }}
    />
  );
}

export default function Page() {
  return (
    <>
      <Stepper current="squad" />
      <Suspense fallback={<SquadSkeleton />}>
        <Step />
      </Suspense>
    </>
  );
}
