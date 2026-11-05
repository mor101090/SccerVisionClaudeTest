"use client";

import dynamic from "next/dynamic";

// Canvas renderer touches `document` at module load: browser only.
export const ReplayClient = dynamic(
  () => import("@/features/matches/replay-player").then((m) => m.ReplayPlayer),
  { ssr: false, loading: () => <p className="text-zinc-400">Loading replay…</p> },
);
