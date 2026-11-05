import type { Metadata } from "next";
import { Suspense } from "react";

import { ManagerForm } from "@/features/team/ui/manager-form";
import { Stepper, TeamCard } from "@/features/team/ui/pieces";
import { TeamFormSkeleton } from "@/features/team/ui/team-skeleton";
import { countryFromRequest, getCountries } from "@/server/countries";
import { loadStep } from "@/server/team-flow";

export const metadata: Metadata = { title: "Tell us about you, coach" };

async function Step() {
  const [{ profile }, countries] = await Promise.all([loadStep("manager"), getCountries()]);
  // No nationality yet: start from the visitor's country (IP geolocation, no permission prompt).
  const nationality = profile.nationality ?? (await countryFromRequest(countries));
  return (
    <ManagerForm
      username={profile.username ?? ""}
      nationality={nationality}
      countries={countries}
    />
  );
}

export default function Page() {
  return (
    <>
      <Stepper current="manager" />
      <TeamCard
        width="max-w-[560px]"
        title="Tell us about you, coach"
        subtitle="It's time to start your journey as a manager and create your own team."
      >
        <Suspense fallback={<TeamFormSkeleton fields={4} />}>
          <Step />
        </Suspense>
      </TeamCard>
    </>
  );
}
