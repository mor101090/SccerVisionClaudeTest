import type { Metadata } from "next";
import { Suspense } from "react";

import { ClubForm } from "@/features/team/ui/club-form";
import { Stepper, TeamCard } from "@/features/team/ui/pieces";
import { TeamFormSkeleton } from "@/features/team/ui/team-skeleton";
import { loadStep } from "@/server/team-flow";

export const metadata: Metadata = { title: "Name your club" };

async function Step() {
  const { team } = await loadStep("club");
  return (
    <ClubForm
      kits={team?.kits ?? null}
      club={
        team
          ? {
              name: team.name,
              shortName: team.shortName,
              namePools: team.namePools ?? [],
              crestPrompt: team.crest?.prompt ?? "",
            }
          : null
      }
    />
  );
}

export default function Page() {
  return (
    <>
      <Stepper current="club" />
      <TeamCard
        width="max-w-[960px]"
        title="Name your club"
        subtitle="This is what fans, opponents and the scoreboard will see."
      >
        <Suspense fallback={<TeamFormSkeleton fields={4} />}>
          <Step />
        </Suspense>
      </TeamCard>
    </>
  );
}
