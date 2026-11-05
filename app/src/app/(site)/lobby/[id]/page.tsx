import type { Metadata } from "next";
import { Suspense } from "react";

import { Loading, Skeleton } from "@/components/ui/skeleton";

import { OnlineClient } from "./online-client";

export const metadata: Metadata = { title: "Online match" };

async function Online({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <OnlineClient lobbyId={id} />;
}

export default function OnlinePage({ params }: PageProps<"/lobby/[id]">) {
  return (
    <main className="flex flex-1 flex-col items-center gap-4 p-4 text-zinc-50 sm:p-8">
      <Suspense
        fallback={
          <Loading className="flex w-full max-w-4xl flex-col items-center gap-4">
            <Skeleton className="h-8 w-72" />
            <Skeleton className="aspect-[105/68] w-full rounded-lg" />
          </Loading>
        }
      >
        <Online params={params} />
      </Suspense>
    </main>
  );
}
