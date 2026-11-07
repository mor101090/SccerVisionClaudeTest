"use client";

import dynamic from "next/dynamic";

// Canvas renderer touches `document` at module load: browser only.
export const ReplayClient = dynamic(
  () => import("@/features/match-day/replay-view").then((m) => m.ReplayView),
  { ssr: false, loading: () => <p className="text-muted">Loading replay…</p> },
);
