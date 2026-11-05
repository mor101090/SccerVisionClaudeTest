"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

const input =
  "w-40 rounded border border-zinc-700 bg-zinc-900 px-3 py-2 text-center font-mono text-lg tracking-widest text-zinc-50 outline-none focus:border-emerald-500";

export function LobbyHome() {
  const router = useRouter();
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function call(url: string, body?: unknown) {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: body ? JSON.stringify(body) : undefined,
      });
      const data = (await res.json()) as { id?: string; error?: string };
      if (!res.ok || !data.id) throw new Error(data.error ?? res.statusText);
      router.push(`/lobby/${data.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-medium">Host a game</h2>
        <p className="text-sm text-zinc-400">You get a six-digit code to send to your friend.</p>
        <button
          type="button"
          disabled={busy}
          onClick={() => void call("/api/lobbies")}
          className="w-fit rounded bg-emerald-600 px-4 py-2 font-medium text-white disabled:opacity-50"
        >
          Host a game
        </button>
      </section>
      <form
        className="flex flex-col gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          void call("/api/lobbies/join", { code });
        }}
      >
        <h2 className="text-lg font-medium">Join a game</h2>
        <label className="flex items-center gap-3 text-sm">
          Game code
          <input
            className={input}
            inputMode="numeric"
            pattern="\d{6}"
            maxLength={6}
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
            aria-label="Game code"
          />
        </label>
        <button
          type="submit"
          disabled={busy || code.length !== 6}
          className="w-fit rounded bg-zinc-700 px-4 py-2 font-medium text-white disabled:opacity-50"
        >
          Join
        </button>
      </form>
      {error && (
        <p role="alert" className="text-red-400">
          {error}
        </p>
      )}
    </div>
  );
}
