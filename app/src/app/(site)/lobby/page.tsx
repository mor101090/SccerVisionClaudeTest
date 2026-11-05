import type { Metadata } from "next";

import { LobbyHome } from "@/features/lobby/lobby-home";

export const metadata: Metadata = { title: "Online" };

export default function LobbyPage() {
  return (
    <main className="mx-auto flex w-full max-w-xl flex-1 flex-col gap-6 p-4 text-zinc-50 sm:p-8">
      <h1 className="text-2xl font-semibold">Play a friend online</h1>
      <LobbyHome />
    </main>
  );
}
