import type { ReplayEvent } from "@/engine/replay/replay";

export const SPEEDS = [1, 4, 16, 64] as const;
export type Speed = (typeof SPEEDS)[number];

export interface HudState {
  clock: string;
  home: number;
  away: number;
  homeName: string;
  awayName: string;
}

export function SpeedPicker({ speed, onChange }: { speed: Speed; onChange: (s: Speed) => void }) {
  return (
    <div className="flex items-center gap-1" role="group" aria-label="Playback speed">
      {SPEEDS.map((s) => (
        <button
          key={s}
          type="button"
          aria-pressed={speed === s}
          className={`rounded px-3 py-1 text-sm ${speed === s ? "bg-zinc-100 text-zinc-900" : "bg-zinc-800 text-zinc-200"}`}
          onClick={() => onChange(s)}
        >
          {s}×
        </button>
      ))}
    </div>
  );
}

export function Scoreboard({ hud }: { hud: HudState }) {
  return (
    <div
      className="flex items-center justify-between rounded bg-zinc-900 px-4 py-2 font-mono text-lg text-zinc-50"
      data-testid="scoreboard"
    >
      <span>{hud.homeName || "Home"}</span>
      <span data-testid="score">
        {hud.home} – {hud.away}
      </span>
      <span>{hud.awayName || "Away"}</span>
      <span className="text-zinc-400" data-testid="clock">
        {hud.clock}
      </span>
    </div>
  );
}

export function EventFeed({ events }: { events: ReplayEvent[] }) {
  return (
    <ul className="mt-2 space-y-1 text-sm text-zinc-400" aria-live="polite">
      {events.map((ev, i) => (
        <li key={`${ev.t}-${i}`}>
          <span className="text-zinc-500">{ev.minute}</span> {ev.text}
        </li>
      ))}
    </ul>
  );
}

/** Latest notable events up to time t. */
export function recentEvents(events: ReplayEvent[], t: number, n = 6): ReplayEvent[] {
  return events.filter((ev) => ev.t <= t && ev.tone !== "Normal").slice(-n);
}

export function isSecondHalf(events: ReplayEvent[], t: number): boolean {
  return events.some((ev) => ev.type === "SecondHalfStart" && ev.t <= t);
}
