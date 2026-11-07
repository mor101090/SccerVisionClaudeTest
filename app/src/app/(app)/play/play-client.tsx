"use client";

import dynamic from "next/dynamic";

import type { HubData } from "@/features/match-day/play-hub";

// The canvas renderer touches `document` at module load: browser only.
const MatchDay = dynamic(() => import("@/features/match-day/match-day").then((m) => m.MatchDay), {
  ssr: false,
  loading: () => <p className="text-muted">Loading…</p>,
});

export function PlayClient({ data }: { data: HubData }) {
  return <MatchDay data={data} />;
}
