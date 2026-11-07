import type { Metadata } from "next";
import { Suspense } from "react";

import { MatchSkeleton } from "@/components/ui/skeleton";

import { OnlineClient } from "./online-client";

export const metadata: Metadata = { title: "Online match" };

async function Online({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <OnlineClient lobbyId={id} />;
}

export default function OnlinePage({ params }: PageProps<"/lobby/[id]">) {
  return (
    <div className="flex flex-1 flex-col items-center gap-4 text-zinc-50">
      <Suspense fallback={<MatchSkeleton />}>
        <Online params={params} />
      </Suspense>
    </div>
  );
}
