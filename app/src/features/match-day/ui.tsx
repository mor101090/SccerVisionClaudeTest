"use client";

import { useEffect, useId, useRef } from "react";

import { AppIcon, type IconName } from "@/components/app-shell/app-icon";

import { type Moment, type MomentKind, positionGroup, ratingTone } from "./derive";

/**
 * Small building blocks of the match-day screens (Figma "Design System" → Position Badge, Rating, Stat Compare,
 * Commentary Line, Timeline Marker, Momentum Bar, Segmented Control, Dialog). Colours come from the tokens only.
 */

const POS_CLASS = {
  GK: "bg-card text-accent-ink",
  DEF: "bg-accent text-on-accent",
  MID: "bg-goal text-accent-ink",
  WNG: "bg-[var(--mor-pink)] text-on-accent",
  ST: "bg-foul text-on-accent",
} as const;

export function PosBadge({ role }: { role: string }) {
  const g = positionGroup(role);
  return (
    <span
      className={`inline-flex h-5 w-fit min-w-9 items-center justify-center rounded px-1.5 text-[10px] font-bold ${POS_CLASS[g]}`}
      title={role}
    >
      {g}
    </span>
  );
}

const RATING_CLASS = {
  goal: "bg-goal text-accent-ink",
  accent: "bg-accent text-on-accent",
  card: "bg-card text-accent-ink",
  chance: "bg-chance text-accent-ink",
  foul: "bg-foul text-on-accent",
} as const;

export function RatingBadge({ value }: { value: number | null }) {
  if (value === null) return <span className="w-8 text-center text-xs text-muted">–</span>;
  return (
    <span
      className={`inline-flex h-5 w-8 items-center justify-center rounded text-[11px] font-bold tabular-nums ${RATING_CLASS[ratingTone(value)]}`}
    >
      {value.toFixed(1)}
    </span>
  );
}

/** Two-sided bar: home share on the left, away on the right. */
export function StatCompare({
  label,
  home,
  away,
  suffix = "",
}: {
  label: string;
  home: number;
  away: number;
  suffix?: string;
}) {
  const sum = home + away;
  const h = sum > 0 ? home / sum : 0.5;
  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-baseline justify-between text-[13px]">
        <span className="font-bold text-text tabular-nums">
          {home}
          {suffix}
        </span>
        <span className="text-xs text-muted">{label}</span>
        <span className="font-bold text-text tabular-nums">
          {away}
          {suffix}
        </span>
      </div>
      <div className="flex h-1.5 gap-1" aria-hidden>
        <span
          className="rounded-full bg-accent transition-[flex-grow] duration-500"
          style={{ flexGrow: h }}
        />
        <span
          className="rounded-full bg-muted/60 transition-[flex-grow] duration-500"
          style={{ flexGrow: 1 - h }}
        />
      </div>
    </div>
  );
}

const MOMENT_ICON: Record<MomentKind, { icon: IconName; cls: string }> = {
  goal: { icon: "target", cls: "bg-goal/20 text-goal" },
  card: { icon: "card", cls: "bg-card/20 text-card" },
  red: { icon: "card", cls: "bg-foul/20 text-foul" },
  chance: { icon: "target", cls: "bg-chance/20 text-chance" },
  sub: { icon: "swap", cls: "bg-accent/25 text-text" },
  whistle: { icon: "whistle", cls: "bg-border text-muted" },
  info: { icon: "whistle", cls: "bg-border text-muted" },
};

export function MomentIcon({ kind, size = 26 }: { kind: MomentKind; size?: number }) {
  const m = MOMENT_ICON[kind];
  return (
    <span
      className={`flex shrink-0 items-center justify-center rounded-full ${m.cls}`}
      style={{ width: size, height: size }}
      aria-hidden
    >
      <AppIcon name={m.icon} size={Math.round(size * 0.55)} />
    </span>
  );
}

export function CommentaryLine({ m, onClick }: { m: Moment; onClick?: () => void }) {
  const strong = m.kind === "goal";
  const body = (
    <>
      <span className="w-9 shrink-0 text-xs text-muted tabular-nums">{m.minute}</span>
      <MomentIcon kind={m.kind} />
      <span
        className={`text-[13px] leading-5 ${strong ? "font-bold text-text" : m.kind === "info" ? "text-muted" : "text-text"}`}
      >
        {m.text}
      </span>
    </>
  );
  const cls = `flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left ${strong ? "bg-goal/12" : ""}`;
  return (
    <li>
      {onClick ? (
        <button
          type="button"
          onClick={onClick}
          className={`${cls} hover:bg-panel focus-visible:outline-2 focus-visible:outline-link`}
        >
          {body}
        </button>
      ) : (
        <div className={cls}>{body}</div>
      )}
    </li>
  );
}

/** Match timeline: progress, the half-time tick and a marker per key moment (click: jump there, in replays). */
export function Timeline({
  moments,
  t,
  duration,
  halftime,
  onSeek,
  compact = false,
}: {
  moments: Moment[];
  t: number;
  /** Replay seconds of the whole match (or the best estimate while it is still being played). */
  duration: number;
  halftime: number | null;
  onSeek?: (t: number) => void;
  compact?: boolean;
}) {
  const at = (x: number) => `${Math.max(0, Math.min(1, duration > 0 ? x / duration : 0)) * 100}%`;
  return (
    <div className={`relative w-full ${compact ? "h-9" : "h-12"}`}>
      <div className="absolute inset-x-0 bottom-2 h-1 rounded-full bg-border" />
      <div
        className="absolute bottom-2 left-0 h-1 rounded-full bg-selected"
        style={{ width: at(t) }}
      />
      {halftime !== null && (
        <span
          className="absolute bottom-0.5 h-4 w-px bg-muted"
          style={{ left: at(halftime) }}
          aria-hidden
        />
      )}
      {onSeek ? (
        <input
          type="range"
          min={0}
          max={Math.max(1, duration)}
          step={0.1}
          value={Math.min(t, duration)}
          onChange={(e) => onSeek(Number(e.target.value))}
          aria-label="Match time"
          className="absolute inset-x-0 bottom-0 h-5 w-full cursor-pointer opacity-0"
        />
      ) : null}
      <span
        className="pointer-events-none absolute bottom-[3px] size-3 -translate-x-1/2 rounded-full bg-text shadow"
        style={{ left: at(t) }}
        aria-hidden
      />
      {moments
        .filter((m) => m.kind !== "info" && m.kind !== "whistle" && m.kind !== "chance")
        .map((m, i) => {
          const marker = (
            <>
              <MomentIcon kind={m.kind} size={compact ? 16 : 20} />
              <span className="h-2 w-px bg-muted/60" />
            </>
          );
          const style = { left: at(m.t) };
          const cls = "absolute bottom-3 flex -translate-x-1/2 flex-col items-center";
          return onSeek ? (
            <button
              key={`${m.t}-${i}`}
              type="button"
              className={`${cls} rounded-full focus-visible:outline-2 focus-visible:outline-link`}
              style={style}
              onClick={() => onSeek(Math.max(0, m.t - 4))}
              aria-label={`${m.minute} ${m.text}`}
              title={`${m.minute} ${m.text}`}
            >
              {marker}
            </button>
          ) : (
            <span key={`${m.t}-${i}`} className={cls} style={style} title={`${m.minute} ${m.text}`}>
              {marker}
            </span>
          );
        })}
    </div>
  );
}

/** Momentum bars: height = how strongly a side pressed in that slice; home blue, away muted. */
export function Momentum({ values }: { values: number[] }) {
  return (
    <div className="flex h-10 items-center gap-1" role="img" aria-label="Momentum, last minutes">
      {values.map((v, i) => (
        <span
          key={i}
          className={`flex-1 rounded-sm transition-[height] duration-500 ${v >= 0 ? "bg-accent" : "bg-muted/70"}`}
          style={{ height: `${20 + Math.abs(v) * 80}%` }}
        />
      ))}
    </div>
  );
}

export function Segmented<T extends string>({
  options,
  value,
  onChange,
  label,
  size = "md",
}: {
  options: readonly T[];
  value: T;
  onChange: (v: T) => void;
  label: string;
  size?: "sm" | "md";
}) {
  return (
    <div
      role="radiogroup"
      aria-label={label}
      className="inline-flex rounded-lg border border-border bg-panel-2 p-1"
    >
      {options.map((o) => (
        <button
          key={o}
          type="button"
          role="radio"
          aria-checked={value === o}
          onClick={() => onChange(o)}
          className={`rounded-md font-medium transition-colors focus-visible:outline-2 focus-visible:outline-link ${
            size === "sm" ? "px-2.5 py-1 text-xs" : "px-3.5 py-1.5 text-[13px]"
          } ${value === o ? "bg-selected text-white" : "text-muted hover:text-text"}`}
        >
          {o}
        </button>
      ))}
    </div>
  );
}

export function Toggle({
  pressed,
  onClick,
  children,
  title,
}: {
  pressed: boolean;
  onClick: () => void;
  children: React.ReactNode;
  title?: string;
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onClick}
      title={title}
      className={`shrink-0 rounded-lg border px-3 py-1.5 text-[13px] font-medium whitespace-nowrap transition-colors focus-visible:outline-2 focus-visible:outline-link ${
        pressed
          ? "border-selected bg-selected text-white"
          : "border-border bg-panel-2 text-text hover:bg-panel"
      }`}
    >
      {children}
    </button>
  );
}

/** Focus trap + Escape for drawers and dialogs (the match keeps running behind them). */
function useModal(open: boolean, onClose?: () => void) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const prev = document.activeElement as HTMLElement | null;
    const el = ref.current;
    el?.querySelector<HTMLElement>("[data-autofocus], button, [href], input, select")?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && onClose) onClose();
      if (e.key !== "Tab" || !el) return;
      const f = [
        ...el.querySelectorAll<HTMLElement>("button:not([disabled]), [href], input, select"),
      ];
      if (!f.length) return;
      const first = f[0]!;
      const last = f[f.length - 1]!;
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      prev?.focus?.();
    };
  }, [open, onClose]);
  return ref;
}

export function Drawer({
  open,
  title,
  sub,
  onClose,
  footer,
  children,
}: {
  open: boolean;
  title: string;
  sub?: string;
  onClose: () => void;
  footer?: React.ReactNode;
  children: React.ReactNode;
}) {
  const ref = useModal(open, onClose);
  const id = useId();
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <button
        type="button"
        aria-label="Close"
        tabIndex={-1}
        className="absolute inset-0 bg-overlay/60"
        onClick={onClose}
      />
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-labelledby={id}
        className="relative flex h-full w-full max-w-[540px] animate-sv-enter flex-col bg-panel shadow-2xl"
      >
        <header className="flex items-start gap-3 px-6 pt-6 pb-4">
          <div className="flex-1">
            <h2 id={id} className="text-xl font-extrabold text-text">
              {title}
            </h2>
            {sub && <p className="mt-1 text-xs text-muted">{sub}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-md p-1 text-text hover:bg-panel-2 focus-visible:outline-2 focus-visible:outline-link"
          >
            <AppIcon name="close" size={20} />
          </button>
        </header>
        <div className="flex-1 overflow-y-auto px-6 pb-4">{children}</div>
        {footer && (
          <footer className="flex justify-end gap-2 border-t border-border px-6 py-4">
            {footer}
          </footer>
        )}
      </div>
    </div>
  );
}

export function Dialog({
  open,
  labelledBy,
  children,
  className = "",
  onClose,
}: {
  open: boolean;
  labelledBy: string;
  children: React.ReactNode;
  className?: string;
  onClose?: () => void;
}) {
  const ref = useModal(open, onClose);
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-overlay/55" aria-hidden />
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
        className={`relative max-h-full w-full animate-sv-enter overflow-y-auto rounded-2xl border border-border bg-panel p-6 shadow-2xl ${className}`}
      >
        {children}
      </div>
    </div>
  );
}

/** Button styles of the match screens (Figma Button: primary blue, secondary panel, sizes md / sm). */
export function btn(
  variant: "primary" | "secondary" | "ghost" = "primary",
  size: "md" | "sm" = "md",
) {
  const base =
    "inline-flex items-center justify-center gap-2 rounded-lg font-semibold whitespace-nowrap transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link disabled:cursor-not-allowed disabled:opacity-45";
  const sz = size === "md" ? "h-11 px-4 text-[14px]" : "h-9 px-3 text-[13px]";
  const v = {
    primary: "bg-accent text-on-accent enabled:hover:brightness-110 [&:is(a)]:hover:brightness-110",
    secondary:
      "border border-border bg-panel-2 text-text enabled:hover:bg-panel [&:is(a)]:hover:bg-panel",
    ghost: "text-text enabled:hover:bg-panel-2 [&:is(a)]:hover:bg-panel-2",
  }[variant];
  return `${base} ${sz} ${v}`;
}
