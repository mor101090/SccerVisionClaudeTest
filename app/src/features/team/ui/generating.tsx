"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { FeatureCheckIcon } from "@/components/ui/icons";

import { generateSquadAction } from "../actions";
import { POSITION_BADGE, POSITION_ORDER, SQUAD_SHAPE } from "../generator";
import { PositionBadge } from "./pieces";
import { CompleteBadge, CompleteStars, CompleteTitle } from "./squad-complete";

const TOTAL = Object.values(SQUAD_SHAPE).reduce((a, b) => a + b, 0);
/** Last row's check stays on screen this long before the celebration starts. */
export const HOLD_MS = 300;
/** Celebration length before moving on to the squad (reduced motion: the end frame, shorter). */
export const CELEBRATE_MS = 2200;
export const CELEBRATE_REDUCED_MS = 1200;

/**
 * Figma: Create team – 4 Generating squad. The generator answers in one go; the count fills while it works and
 * completes when the squad is stored (never ahead of the real answer: it waits at 21 until then).
 * Only at 22/22 does it celebrate ("Squad complete") and then refresh into the squad page; the action itself does not
 * revalidate, so nothing can swap the page out early.
 */
export function Generating({ pools }: { pools: string }) {
  const router = useRouter();
  const [made, setMade] = useState(0);
  const [done, setDone] = useState(false);
  const [complete, setComplete] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const started = useRef(false);

  async function run() {
    setError(null);
    setDone(false);
    setMade(0);
    const r = await generateSquadAction();
    if (r?.error) setError(r.error);
    else setDone(true);
  }

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    void run();
  }, []);

  useEffect(() => {
    if (error || complete) return;
    if (made >= TOTAL) {
      const t = setTimeout(() => setComplete(true), HOLD_MS);
      return () => clearTimeout(t);
    }
    if (made >= TOTAL - 1 && !done) return;
    const t = setTimeout(() => setMade((m) => m + 1), done ? 40 : 110);
    return () => clearTimeout(t);
  }, [made, done, error, complete]);

  useEffect(() => {
    if (!complete) return;
    const reduced = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
    const t = setTimeout(() => router.refresh(), reduced ? CELEBRATE_REDUCED_MS : CELEBRATE_MS);
    return () => clearTimeout(t);
  }, [complete, router]);

  const pct = Math.round((made / TOTAL) * 100);
  // players made before each position's row (positions fill in squad order)
  const offsets = POSITION_ORDER.map((_, i) =>
    POSITION_ORDER.slice(0, i).reduce((a, p) => a + SQUAD_SHAPE[p], 0),
  );

  return (
    <section
      className={`relative flex w-full max-w-[600px] flex-col items-center gap-6 rounded-[14px] border bg-panel-2 p-8 text-center transition-colors duration-500 ${complete ? "border-card motion-safe:animate-sv-shake" : "border-border"}`}
      aria-live="polite"
    >
      {complete && (
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 rounded-[14px] bg-white opacity-0 motion-safe:animate-sv-flash"
        />
      )}
      {complete ? (
        <CompleteBadge />
      ) : (
        <svg
          width="64"
          height="64"
          viewBox="0 0 64 64"
          className="animate-spin motion-reduce:animate-none"
          aria-hidden
        >
          <circle cx="32" cy="32" r="26" fill="none" strokeWidth="4" className="stroke-border" />
          <path
            d="M32 6a26 26 0 0 1 26 26"
            fill="none"
            strokeWidth="4"
            strokeLinecap="round"
            className="stroke-accent"
          />
        </svg>
      )}
      {complete ? (
        <CompleteTitle>Squad complete!</CompleteTitle>
      ) : (
        <h1 className="text-[26px] leading-[1.2] font-extrabold text-text">
          Generating your squad...
        </h1>
      )}
      {complete ? (
        <p
          className="text-[15px] text-text motion-safe:animate-sv-rise"
          style={{ animationDelay: "450ms" }}
        >
          {TOTAL}/{TOTAL} players signed. Your club is ready.
        </p>
      ) : (
        <p className="text-[15px] text-muted">
          22 players with real attributes, ages, heights and feet, named from {pools} names.
        </p>
      )}
      {complete ? (
        <CompleteStars />
      ) : error ? (
        <div className="flex w-full flex-col gap-3">
          <Alert tone="error">{error}</Alert>
          <Button onClick={() => void run()}>Try again</Button>
        </div>
      ) : (
        <div className="flex w-full flex-col gap-2">
          <div
            className="h-2 w-full overflow-hidden rounded-full bg-panel"
            role="progressbar"
            aria-valuenow={made}
            aria-valuemin={0}
            aria-valuemax={TOTAL}
          >
            <div
              className="h-full rounded-full bg-accent transition-[width] duration-200"
              style={{ width: `${pct}%` }}
            />
          </div>
          <div className="flex justify-between text-[13px]">
            <span className="text-muted">
              {made} of {TOTAL} players
            </span>
            <span className="font-semibold text-text">{pct}%</span>
          </div>
        </div>
      )}
      <ul className="flex w-full flex-col gap-4">
        {POSITION_ORDER.map((pos, i) => {
          const n = SQUAD_SHAPE[pos];
          const have = Math.max(0, Math.min(n, made - offsets[i]!));
          const full = have === n;
          return (
            <li
              key={pos}
              className={`relative flex items-center gap-3 text-[15px] ${complete ? "motion-safe:animate-sv-stamp" : ""}`}
              style={complete ? { animationDelay: `${300 + i * 70}ms` } : undefined}
            >
              {complete && (
                <span
                  aria-hidden
                  className="pointer-events-none absolute -inset-x-2 -inset-y-1 rounded-md bg-goal/25 opacity-0 motion-safe:animate-sv-flash"
                  style={{ animationDelay: `${300 + i * 70}ms` }}
                />
              )}
              <PositionBadge position={pos} />
              <span className={have ? "text-text" : "text-muted"}>
                {POSITION_BADGE[pos].plural}
              </span>
              <span
                className={`ml-auto flex items-center gap-1.5 font-medium ${full ? "text-goal" : "text-muted"}`}
              >
                {full && <FeatureCheckIcon className="text-goal" />}
                {have}/{n}
              </span>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
