"use client";

import { useEffect, useMemo, useRef, useState } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { WORLD_H, WORLD_W } from "@/engine/replay/drawPitch";
import type { Replay } from "@/engine/replay/replay";

import {
  keyMoments,
  rowAt,
  rulesOf,
  statsFromRow,
  substitutionsAt,
  teamOf,
  windowsUsed,
} from "./derive";
import { HalfTime } from "./half-time";
import { MatchPanel } from "./match-panel";
import { INSTRUCTIONS, MENTALITIES } from "./mentality";
import type { PlanChanges } from "./plan-changes";
import { weatherText } from "./pre-match";
import { ScoreBug, SpeedControl } from "./score-bar";
import { type PendingSub, SubsDrawer } from "./subs-drawer";
import { TacticsDrawer } from "./tactics-drawer";
import { btn, Dialog, MomentIcon, Segmented, Timeline, Toggle } from "./ui";
import type { MatchSession } from "./use-match";

/** A goal or red card from the last few seconds of replay, shown as a banner over the pitch. */
function bannerAt(replay: Replay, t: number) {
  for (let i = replay.events.length - 1; i >= 0; i--) {
    const e = replay.events[i]!;
    if (e.t > t) continue;
    if (t - e.t > 6) break;
    if (e.type === "Goal" || e.type === "RedCard") return e;
  }
  return null;
}

/** Locks the page behind a full-screen view. */
export function useScrollLock() {
  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = prev;
    };
  }, []);
}

export function PitchFrame({
  wrap,
  canvas,
  children,
  label,
}: {
  wrap: React.RefObject<HTMLDivElement | null>;
  canvas: React.RefObject<HTMLCanvasElement | null>;
  children?: React.ReactNode;
  label: string;
}) {
  return (
    <div
      ref={wrap}
      className="relative mx-auto w-full overflow-hidden rounded-2xl bg-[var(--mor-pitch-green-dark)] lg:max-w-[var(--pitch-max)]"
      style={{ ["--pitch-max" as string]: `calc((100dvh - 290px) * ${WORLD_W / WORLD_H})` }}
    >
      <canvas ref={canvas} className="block w-full" aria-label={label} />
      {children}
    </div>
  );
}

export function LiveScreen({
  session,
  plan,
  onLeave,
}: {
  session: MatchSession;
  plan: PlanChanges;
  onLeave: () => void;
}) {
  useScrollLock();
  const { replay, tick, phase } = session;
  const [drawer, setDrawer] = useState<"subs" | "tactics" | null>(null);
  const [leave, setLeave] = useState(false);
  const [pending, setPending] = useState<(PendingSub & { at: number })[]>([]);
  const [toast, setToast] = useState<string | null>(null);
  const seen = useRef(0);

  const t = tick.t;
  const rows = session.rows();
  const row = rowAt(rows, t);
  const stats = row ? statsFromRow(row) : null;

  // Pending changes leave the list when the engine makes them (Substitution) or refuses them (SubstitutionRequest).
  const events = replay?.events;
  useEffect(() => {
    if (!events || events.length === seen.current) return;
    const fresh = events.slice(seen.current);
    seen.current = events.length;
    for (const e of fresh) {
      if (e.team !== 0 || !e.s) continue;
      const s = e.s.split("|").map(Number);
      if (e.type === "Substitution") setPending((p) => p.filter((q) => q.in !== s[0]));
      if (e.type === "SubstitutionRequest" && (e.v ?? 0) > 0.5) {
        setPending((p) => p.filter((q) => !(q.out === s[0] && q.in === s[1])));
        setToast(e.text);
      }
    }
  }, [events, events?.length, t]);

  useEffect(() => {
    if (!toast) return;
    const id = setTimeout(() => setToast(null), 5000);
    return () => clearTimeout(id);
  }, [toast]);

  const second = Math.floor(t);
  const moments = useMemo(
    () => (replay ? keyMoments(replay.events, second) : []),
    // replay.events grows in place while the engine plays; the second re-reads it.

    [replay, second],
  );
  if (!replay) return null;

  const rules = rulesOf(replay);
  const made = substitutionsAt(replay.events, 0).length;
  const subsLeft = Math.max(0, rules.maxSubs - made - pending.length);
  const halftime = phase === "halftime";
  const windowsLeft = Math.max(
    0,
    rules.windows - windowsUsed(replay.events, 0) - (pending.length && !halftime ? 1 : 0),
  );
  // The whole match lasts two halves of halfRealSeconds (plus added time); the bar grows if it runs longer.
  const estimate = Math.max(tick.duration, replay.halfRealSeconds * 2 + 30, t);
  const htEvent = replay.events.find((e) => e.type === "HalfTime");
  const banner = bannerAt(replay, t);
  const home = teamOf(replay, 0);
  const changes = plan.changes;

  const confirmSubs = (pairs: PendingSub[]) => {
    for (const p of pairs) session.substitute(p.out, p.in);
    setPending((list) => [...list, ...pairs.map((p) => ({ ...p, at: t }))]);
  };

  return (
    <div className="fixed inset-0 z-40 flex flex-col bg-bg text-text">
      <header className="grid grid-cols-[auto_1fr_auto] items-center gap-2 border-b border-border bg-panel px-3 py-2 sm:px-5">
        <button
          type="button"
          onClick={() => setLeave(true)}
          className="flex items-center gap-1 rounded-md px-2 py-1.5 text-[13px] text-muted hover:text-text focus-visible:outline-2 focus-visible:outline-link"
        >
          <AppIcon name="chevronLeft" size={16} />
          <span className="hidden sm:inline">Leave match</span>
        </button>
        <div className="flex justify-center">
          <ScoreBug
            replay={replay}
            t={t}
            clock={tick.clock}
            home={tick.home}
            away={tick.away}
            state={halftime ? "ht" : session.paused ? "paused" : "live"}
          />
        </div>
        <div className="flex justify-end">
          <div className="hidden md:block">
            <SpeedControl
              speed={session.speed}
              onSpeed={session.setSpeed}
              paused={session.paused}
              onPause={() => session.setPaused(!session.paused)}
            />
          </div>
          <div className="md:hidden">
            <SpeedControl
              compact
              speed={session.speed}
              onSpeed={session.setSpeed}
              paused={session.paused}
              onPause={() => session.setPaused(!session.paused)}
            />
          </div>
        </div>
      </header>

      <main className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto p-3 sm:p-4 lg:flex-row lg:overflow-hidden">
        <div className="flex min-w-0 flex-1 flex-col gap-4">
          <PitchFrame wrap={session.wrap} canvas={session.canvas} label="Match pitch">
            {replay.conditions && (
              <span className="absolute top-3 left-3 flex items-center gap-1.5 rounded-md bg-bg/70 px-2 py-1 text-[11px] font-medium text-text">
                <AppIcon name="cloud" size={14} />
                {weatherText(replay)}
              </span>
            )}
            {banner && (
              <span
                role="status"
                className={`absolute bottom-4 left-1/2 flex -translate-x-1/2 animate-sv-enter items-center gap-2 rounded-full px-4 py-2 text-[13px] font-bold whitespace-nowrap shadow-lg ${banner.type === "Goal" ? "bg-goal text-accent-ink" : "bg-foul text-on-accent"}`}
              >
                <MomentIcon kind={banner.type === "Goal" ? "goal" : "red"} size={20} />
                {banner.type === "Goal"
                  ? `GOAL! ${banner.minute} · ${teamOf(replay, banner.team as 0 | 1).name} ${tick.home}–${tick.away}`
                  : `Red card ${banner.minute}`}
              </span>
            )}
          </PitchFrame>
          <section
            className="rounded-2xl border border-border bg-panel-2 px-4 pt-3 pb-2"
            aria-label="Match timeline"
          >
            <div className="flex justify-between text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
              <span>Match timeline</span>
              <span className="font-medium tracking-normal">0′ · HT · 90′</span>
            </div>
            <Timeline
              moments={moments}
              t={t}
              duration={estimate}
              halftime={htEvent?.t ?? replay.halfRealSeconds}
            />
          </section>
        </div>
        <MatchPanel
          className="h-[460px] shrink-0 lg:h-auto lg:w-[380px]"
          replay={replay}
          t={t}
          row={row}
          stats={stats}
          mode="live"
        />
      </main>

      <footer className="flex flex-wrap items-end gap-x-5 gap-y-3 border-t border-border bg-panel px-3 py-3 sm:px-5">
        {changes && (
          <>
            <div className="flex flex-col gap-1">
              <span className="text-[10px] font-bold tracking-[0.08em] text-muted uppercase">
                Mentality
              </span>
              <Segmented
                size="sm"
                label="Mentality"
                options={MENTALITIES}
                value={changes.mentality}
                onChange={(mentality) => plan.apply({ mentality })}
              />
            </div>
            <div className="flex max-w-full min-w-0 flex-col gap-1">
              <span className="text-[10px] font-bold tracking-[0.08em] text-muted uppercase">
                Instructions · applied at the next stoppage
              </span>
              <div className="flex gap-1.5 overflow-x-auto pb-0.5">
                {INSTRUCTIONS.map((i) => {
                  const on = changes.instructions.includes(i.id);
                  return (
                    <Toggle
                      key={i.id}
                      pressed={on}
                      title={i.hint}
                      onClick={() =>
                        plan.apply({
                          instructions: on
                            ? changes.instructions.filter((x) => x !== i.id)
                            : [...changes.instructions, i.id],
                        })
                      }
                    >
                      {i.label}
                    </Toggle>
                  );
                })}
              </div>
            </div>
          </>
        )}
        <div className="ml-auto flex gap-2">
          <button type="button" className={btn("secondary")} onClick={() => setDrawer("subs")}>
            <AppIcon name="swap" size={16} />
            <span className="sr-only sm:not-sr-only">Substitutions</span>
            <span className="rounded-full bg-selected/40 px-2 text-[11px]">{subsLeft} left</span>
          </button>
          <button type="button" className={btn("primary")} onClick={() => setDrawer("tactics")}>
            <AppIcon name="flag" size={16} />
            Tactics
          </button>
        </div>
      </footer>

      {toast && (
        <p
          role="status"
          className="fixed top-20 left-1/2 z-50 -translate-x-1/2 rounded-lg bg-panel px-4 py-2 text-[13px] text-text shadow-xl"
        >
          {toast}
        </p>
      )}

      <HalfTime
        open={halftime && drawer === null}
        replay={replay}
        home={tick.home}
        away={tick.away}
        stats={stats}
        plan={plan}
        subsLeft={subsLeft}
        onSubs={() => setDrawer("subs")}
        onTactics={() => setDrawer("tactics")}
        onResume={session.resumeSecondHalf}
      />
      <SubsDrawer
        open={drawer === "subs"}
        onClose={() => setDrawer(null)}
        replay={replay}
        t={t}
        row={row}
        left={subsLeft}
        windowsLeft={halftime ? Math.max(1, windowsLeft) : windowsLeft}
        pending={pending}
        onConfirm={confirmSubs}
      />
      <TacticsDrawer
        open={drawer === "tactics"}
        onClose={() => setDrawer(null)}
        replay={replay}
        t={t}
        plan={plan}
      />
      <Dialog
        open={leave}
        labelledBy="leave-title"
        className="max-w-md"
        onClose={() => setLeave(false)}
      >
        <h2 id="leave-title" className="text-lg font-extrabold text-text">
          Leave the match?
        </h2>
        <p className="mt-2 text-[13px] text-muted">
          {home.name} {tick.home}–{tick.away} {teamOf(replay, 1).name}, {tick.clock}. A match you
          leave isn&apos;t saved.
        </p>
        <div className="mt-5 flex justify-end gap-2">
          <button
            type="button"
            className={btn("secondary")}
            onClick={() => setLeave(false)}
            data-autofocus
          >
            Keep playing
          </button>
          <button type="button" className={btn("primary")} onClick={onLeave}>
            Leave match
          </button>
        </div>
      </Dialog>
    </div>
  );
}
