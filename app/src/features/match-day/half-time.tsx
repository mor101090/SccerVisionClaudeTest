"use client";

import { AppIcon } from "@/components/app-shell/app-icon";
import type { Replay } from "@/engine/replay/replay";

import { teamOf, type TeamStats } from "./derive";
import { StatsList } from "./match-panel";
import { MENTALITIES } from "./mentality";
import type { PlanChanges } from "./plan-changes";
import { btn, Dialog, Segmented } from "./ui";

/** A hint from the first-half numbers, pointing at a control the manager has (no invented scouting). */
export function halfTimeHint(stats: TeamStats | null, home: number, away: number): string | null {
  if (!stats) return null;
  const [hs, as] = stats.shots;
  if (as >= hs + 3)
    return `They had ${as - hs} more shots. Defensive or Drop deeper can tighten things up.`;
  if (hs >= as + 3 && home <= away)
    return `You're creating more (${hs} shots to ${as}). Attacking or Push forward to turn it into goals.`;
  if (stats.possession[0] <= 40)
    return `They had the ball ${stats.possession[1]}% of the time. Play wider to stretch them when you win it back.`;
  return null;
}

export function HalfTime({
  open,
  replay,
  home,
  away,
  stats,
  plan,
  subsLeft,
  onSubs,
  onTactics,
  onResume,
}: {
  open: boolean;
  replay: Replay;
  home: number;
  away: number;
  stats: TeamStats | null;
  plan: PlanChanges;
  subsLeft: number;
  onSubs: () => void;
  onTactics: () => void;
  onResume: () => void;
}) {
  const h = teamOf(replay, 0);
  const a = teamOf(replay, 1);
  const hint = halfTimeHint(stats, home, away);
  return (
    <Dialog open={open} labelledBy="ht-title" className="max-w-[720px]" onClose={onResume}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-[11px] font-bold tracking-[0.08em] text-link uppercase">Half-time</p>
          <h2 id="ht-title" className="text-2xl font-extrabold text-text">
            {h.name} {home} – {away} {a.name}
          </h2>
        </div>
        <span className="rounded-full bg-accent/20 px-3 py-1 text-[11px] font-semibold text-text">
          Changes now are free: no window used
        </span>
      </div>
      <div className="mt-5 grid gap-6 md:grid-cols-2">
        <div className="flex flex-col gap-3">
          <h3 className="text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
            First half
          </h3>
          {stats ? (
            <StatsList stats={stats} />
          ) : (
            <p className="text-[13px] text-muted">No stats yet.</p>
          )}
          {hint && (
            <p className="flex items-start gap-2 rounded-lg border border-card/60 bg-card/10 p-3 text-[13px] leading-5 text-text">
              <AppIcon name="alert" size={16} className="mt-0.5 text-card" />
              {hint}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-3">
          <h3 className="text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
            Your changes
          </h3>
          {plan.changes && (
            <div className="flex flex-col gap-2">
              <span className="text-[13px] text-muted">Mentality</span>
              <Segmented
                label="Mentality"
                options={MENTALITIES}
                value={plan.changes.mentality}
                onChange={(mentality) => plan.apply({ mentality })}
              />
            </div>
          )}
          <button
            type="button"
            className={`${btn("secondary")} self-start`}
            onClick={onSubs}
            disabled={subsLeft <= 0}
          >
            <AppIcon name="swap" size={16} />
            Make substitutions
            <span className="rounded-full bg-selected/30 px-2 text-[11px]">{subsLeft} left</span>
          </button>
          <div className="mt-auto flex flex-wrap justify-end gap-2 pt-4">
            <button type="button" className={btn("secondary")} onClick={onTactics}>
              Open tactics
            </button>
            <button type="button" className={btn("primary")} onClick={onResume} data-autofocus>
              Start second half
            </button>
          </div>
        </div>
      </div>
    </Dialog>
  );
}
