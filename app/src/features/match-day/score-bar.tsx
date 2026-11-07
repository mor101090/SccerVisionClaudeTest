"use client";

import { AppIcon } from "@/components/app-shell/app-icon";
import type { Replay } from "@/engine/replay/replay";

import { scorerLine, scorersAt, shortCode, teamOf } from "./derive";
import { SPEEDS, type Speed } from "./use-match";

export type BarState = "live" | "paused" | "ht" | "ft" | "replay";

/** Figma "Scoreboard": clock, both teams with their kit colour, the score, a state chip; the scorers underneath. */
export function ScoreBug({
  replay,
  t,
  clock,
  home,
  away,
  state,
}: {
  replay: Replay;
  t: number;
  clock: string;
  home: number;
  away: number;
  state: BarState;
}) {
  const h = teamOf(replay, 0);
  const a = teamOf(replay, 1);
  const hs = scorerLine(scorersAt(replay, 0, t));
  const as = scorerLine(scorersAt(replay, 1, t));
  const lead = state === "ft" ? "FT" : state === "ht" ? "HT" : clock;
  return (
    <div className="flex min-w-0 flex-col items-center gap-1">
      <div
        className="flex items-stretch overflow-hidden rounded-lg bg-panel-2 text-[13px] font-bold whitespace-nowrap"
        data-testid="scoreboard"
      >
        <span
          className={`flex min-w-11 items-center justify-center px-2 tabular-nums sm:min-w-14 sm:px-2.5 ${state === "live" ? "text-goal" : "text-text"}`}
          data-testid="clock"
        >
          {lead}
        </span>
        <span className="flex items-center gap-2 px-2.5 text-text">
          <span
            className="h-4 w-1 rounded-full"
            style={{ background: h.kit.primary }}
            aria-hidden
          />
          <span title={h.name}>{shortCode(h)}</span>
        </span>
        <span
          className="flex items-center bg-selected px-3 text-[15px] text-white tabular-nums"
          data-testid="score"
        >
          {home} – {away}
        </span>
        <span className="flex items-center gap-2 px-2.5 text-text">
          <span title={a.name}>{shortCode(a)}</span>
          <span
            className="h-4 w-1 rounded-full"
            style={{ background: a.kit.primary }}
            aria-hidden
          />
        </span>
        {(state === "live" || state === "paused") && (
          <span className="hidden items-center px-2 sm:flex">
            <span
              className={`flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] font-bold text-white ${state === "live" ? "bg-foul" : "bg-border"}`}
            >
              <span className="size-1.5 rounded-full bg-white" aria-hidden />
              {state === "live" ? "LIVE" : "PAUSED"}
            </span>
          </span>
        )}
      </div>
      {(hs || as) && (
        <p className="max-w-[90vw] truncate text-[11px] text-muted">
          {hs && <span>{hs}</span>}
          {hs && as && <span className="mx-2">·</span>}
          {as && <span>{as}</span>}
        </p>
      )}
    </div>
  );
}

/** Pause + speed (Figma "Speed Control"). */
export function SpeedControl({
  speed,
  onSpeed,
  paused,
  onPause,
  compact = false,
}: {
  speed: Speed;
  onSpeed: (s: Speed) => void;
  paused: boolean;
  onPause: () => void;
  compact?: boolean;
}) {
  return (
    <div
      className="flex items-center gap-1 rounded-lg border border-border bg-panel-2 p-1"
      role="group"
      aria-label="Playback"
    >
      <button
        type="button"
        onClick={onPause}
        aria-label={paused ? "Play" : "Pause"}
        className="flex size-8 items-center justify-center rounded-md text-text hover:bg-panel focus-visible:outline-2 focus-visible:outline-link"
      >
        <AppIcon name={paused ? "play" : "pause"} size={16} />
      </button>
      {compact ? (
        <select
          aria-label="Speed"
          value={speed}
          onChange={(e) => onSpeed(Number(e.target.value) as Speed)}
          className="h-8 rounded-md bg-panel-2 px-1 text-xs font-semibold text-text"
        >
          {SPEEDS.map((s) => (
            <option key={s} value={s}>
              {s}×
            </option>
          ))}
        </select>
      ) : (
        SPEEDS.map((s) => (
          <button
            key={s}
            type="button"
            aria-pressed={speed === s}
            onClick={() => onSpeed(s)}
            className={`h-8 rounded-md px-2.5 text-xs font-semibold focus-visible:outline-2 focus-visible:outline-link ${speed === s ? "bg-selected text-white" : "text-muted hover:text-text"}`}
          >
            {s}×
          </button>
        ))
      )}
    </div>
  );
}
