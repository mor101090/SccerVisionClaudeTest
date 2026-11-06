import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { DashboardSkeleton } from "@/components/app-shell/shell-skeleton";
import { Loading } from "@/components/ui/skeleton";
import { Dashboard } from "@/features/home/dashboard";
import { currentUser } from "@/server/auth";
import { getHomeData } from "@/server/home";

import { Landing } from "./landing";

export const metadata: Metadata = { title: "Home" };

/**
 * Where every sign-in lands (HOME_PATH). Visitors see the landing page; a manager without a finished club goes back to
 * Create your team (it resumes the step they reached); everyone else gets the dashboard.
 */
async function Home() {
  const user = await currentUser();
  if (!user) return <Landing />;
  const data = await getHomeData(user);
  if (!data || !data.club.ready) redirect("/team/new");
  return <Dashboard d={data} />;
}

export default function HomePage() {
  return (
    <Suspense
      fallback={
        <Loading label="Loading your club…">
          <DashboardSkeleton />
        </Loading>
      }
    >
      <Home />
    </Suspense>
  );
}
