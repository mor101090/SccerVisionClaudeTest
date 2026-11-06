"use client";

import { useEffect, useState, useSyncExternalStore, useTransition } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";

import { markOnboardingSeenAction } from "./actions";
import { countdown, greeting } from "./rules";

const noop = () => () => {};

/** "Good evening, redcoach" by the manager's own clock (the server doesn't know the time zone). */
export function Greeting({ name }: { name: string }) {
  const hello = useSyncExternalStore(
    noop,
    () => greeting(new Date().getHours()),
    () => "Welcome back",
  );
  return (
    <h1 className="text-[26px] leading-tight font-extrabold text-text">
      {hello}, {name}
    </h1>
  );
}

function useNow(stepMs: number) {
  const [now, setNow] = useState<Date | null>(null);
  useEffect(() => {
    const tick = () => setNow(new Date());
    tick();
    const id = setInterval(tick, stepMs);
    return () => clearInterval(id);
  }, [stepMs]);
  return now;
}

/** dd : hh : mm to kick-off, updating every minute; orange under an hour (Figma: Countdown). */
export function Countdown({ kickoff }: { kickoff: string }) {
  const now = useNow(30_000);
  const left = now ? countdown(new Date(kickoff), now) : null;
  const parts: [string, string][] = left
    ? [
        [String(left.days).padStart(2, "0"), "days"],
        [String(left.hours).padStart(2, "0"), "hrs"],
        [String(left.minutes).padStart(2, "0"), "min"],
      ]
    : [
        ["--", "days"],
        ["--", "hrs"],
        ["--", "min"],
      ];
  const urgent = left !== null && left.days === 0 && left.hours === 0;
  return (
    <div
      className={`flex items-center gap-2.5 ${urgent ? "text-chance" : "text-text"}`}
      aria-live={urgent ? "polite" : "off"}
    >
      {parts.map(([v, unit], i) => (
        <div key={unit} className="flex items-center gap-2.5">
          {i > 0 && <span className="text-[28px] font-extrabold text-muted">:</span>}
          <span className="flex flex-col items-center">
            <span className="text-[34px] leading-none font-extrabold tabular-nums">{v}</span>
            <span className="text-[11px] font-medium text-muted">{unit}</span>
          </span>
        </div>
      ))}
    </div>
  );
}

const STEPS = [
  {
    target: "tour-next-match",
    title: "Your next match lives here",
    body: "See who you play and when. With no league fixture yet, start a friendly from here.",
  },
  {
    target: "tour-stats",
    title: "Form at a glance",
    body: "Results, goals and possession from your last five matches and the whole season.",
  },
  {
    target: "tour-tactics",
    title: "Your tactics",
    body: "The shape and orders your team plays with. Edit them before every match.",
  },
  {
    target: "tour-league",
    title: "Coming soon",
    body: "League table, budget and stadium are on their way. These cards fill in when they launch.",
  },
];

/** First-visit coach marks (Figma: Home – first visit tour). Shown once; finishing or skipping saves it. */
export function Tour() {
  const [step, setStep] = useState(0);
  const [open, setOpen] = useState(true);
  const [rect, setRect] = useState<DOMRect | null>(null);
  const [, start] = useTransition();
  useEffect(() => {
    if (!open) return;
    const el = document.getElementById(STEPS[step]!.target);
    if (!el) return;
    el.scrollIntoView({ block: "center", behavior: "smooth" });
    const measure = () => setRect(el.getBoundingClientRect());
    const t = setTimeout(measure, 320);
    window.addEventListener("resize", measure);
    window.addEventListener("scroll", measure, true);
    return () => {
      clearTimeout(t);
      window.removeEventListener("resize", measure);
      window.removeEventListener("scroll", measure, true);
    };
  }, [step, open]);
  if (!open) return null;
  const finish = () => {
    setOpen(false);
    start(() => markOnboardingSeenAction());
  };
  const s = STEPS[step]!;
  const last = step === STEPS.length - 1;
  const below = rect ? rect.bottom + 230 < window.innerHeight : true;
  return (
    <div
      className="fixed inset-0 z-40"
      role="dialog"
      aria-modal="true"
      aria-labelledby="tour-title"
    >
      <div
        className={`absolute inset-0 animate-sv-scrim ${rect ? "" : "bg-bg/70"}`}
        onClick={finish}
      />
      {rect && (
        <div
          className="pointer-events-none absolute rounded-[20px] border-[3px] border-link shadow-[0_0_0_9999px_rgb(23_6_46/0.55),0_0_24px_rgb(98_110_212/0.5)] transition-all duration-[320ms] ease-[cubic-bezier(.3,0,0,1)]"
          style={{
            left: rect.left - 6,
            top: rect.top - 6,
            width: rect.width + 12,
            height: rect.height + 12,
          }}
        />
      )}
      <div
        className="absolute flex w-[360px] max-w-[calc(100vw-32px)] animate-sv-pop flex-col gap-3 rounded-[14px] border border-link bg-panel-2 p-5 shadow-[0_12px_32px_rgb(0_0_0/0.45)] transition-all duration-[320ms]"
        style={
          rect
            ? {
                left: Math.min(Math.max(16, rect.left + 40), window.innerWidth - 376),
                top: below ? rect.bottom + 18 : Math.max(16, rect.top - 218),
              }
            : { left: 16, top: 16 }
        }
      >
        <p className="text-[11px] font-bold tracking-[0.08em] text-link">
          STEP {step + 1} OF {STEPS.length}
        </p>
        <h2 id="tour-title" className="text-lg font-extrabold text-text">
          {s.title}
        </h2>
        <p className="text-[13px] leading-5 text-muted">{s.body}</p>
        <div className="flex items-center gap-1.5">
          {STEPS.map((_, i) => (
            <span
              key={i}
              className={`h-2 rounded-full transition-all ${i === step ? "w-5 bg-link" : "w-2 bg-border"}`}
            />
          ))}
          <span className="flex-1" />
          <button
            type="button"
            onClick={finish}
            className="px-2 text-[13px] font-semibold text-muted hover:text-text"
          >
            Skip tour
          </button>
          <button
            type="button"
            autoFocus
            onClick={() => (last ? finish() : setStep(step + 1))}
            className="inline-flex items-center gap-1 rounded-lg bg-link px-3.5 py-2 text-[13px] font-bold text-white"
          >
            {last ? "Done" : "Next"}
            {!last && <AppIcon name="chevronRight" size={14} />}
          </button>
        </div>
      </div>
    </div>
  );
}
