import type { Metadata } from "next";
import { Suspense } from "react";

import { DEFAULT_KITS } from "@/features/team/kits";
import { KitsForm } from "@/features/team/ui/kits-form";
import { Stepper, TeamCard } from "@/features/team/ui/pieces";
import { TeamFormSkeleton } from "@/features/team/ui/team-skeleton";
import { loadStep } from "@/server/team-flow";

export const metadata: Metadata = { title: "Your kits" };

async function Step() {
  const { team } = await loadStep("kits");
  return <KitsForm initial={team?.kits ?? DEFAULT_KITS} />;
}

export default function Page() {
  return (
    <>
      <Stepper current="kits" />
      <TeamCard
        width="max-w-[1140px]"
        title="Your kits"
        subtitle="Pick the shirt, trim, socks and number for each kit. Colours too close to another kit are greyed out."
      >
        <Suspense fallback={<TeamFormSkeleton fields={2} />}>
          <Step />
        </Suspense>
      </TeamCard>
    </>
  );
}
