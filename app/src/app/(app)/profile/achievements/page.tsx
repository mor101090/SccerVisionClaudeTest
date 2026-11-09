import type { Metadata } from "next";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { Card, PageHeader } from "@/components/app-shell/ui";
import { BadgeTile } from "@/features/profile/ui";
import { myProfileData } from "@/server/profile-page";

export const metadata: Metadata = { title: "Achievements" };

async function Achievements() {
  const d = await myProfileData("/profile/achievements");
  const earned = d.achievements.filter((a) => a.earned).length;
  const groups = ["Matches", "Players", "Community"] as const;
  return (
    <>
      <PageHeader
        crumb="Profile"
        title="Achievements"
        sub={`${earned} of ${d.achievements.length} earned. Achievements are badges: they never change how a match plays.`}
      />
      {groups.map((g) => (
        <Card key={g} title={g} icon="trophy">
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            {d.achievements
              .filter((a) => a.group === g)
              .map((a) => (
                <BadgeTile key={a.id} a={a} />
              ))}
          </div>
        </Card>
      ))}
    </>
  );
}

export default function AchievementsPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Achievements />
    </Suspense>
  );
}
