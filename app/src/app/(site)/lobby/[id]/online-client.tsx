"use client";

import dynamic from "next/dynamic";

// Canvas renderer + engine worker: browser only.
export const OnlineClient = dynamic(
  () => import("@/features/lobby/online-match").then((m) => m.OnlineMatch),
  { ssr: false, loading: () => <p className="text-zinc-400">Loading…</p> },
);
