"use client";

import dynamic from "next/dynamic";

// The canvas renderer touches `document` at module load: browser only.
const MatchViewer = dynamic(
  () => import("@/features/matches/match-viewer").then((m) => m.MatchViewer),
  { ssr: false, loading: () => <p className="text-muted">Loading…</p> },
);

export function PlayClient({
  myTeam,
}: {
  myTeam?: { name: string; text: string; plan?: string | null } | null;
}) {
  return <MatchViewer myTeam={myTeam} />;
}
