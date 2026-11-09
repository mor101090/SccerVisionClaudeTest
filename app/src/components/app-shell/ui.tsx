import Link from "next/link";

import { AppIcon, type IconName } from "./app-icon";

/**
 * Building blocks of the signed-in app (Figma "Homepage flow", node 594-15448): cards, page headers, chips, gauges.
 * Colors come from the semantic tokens only.
 */

type Tone = "accent" | "goal" | "foul" | "card" | "chance" | "info" | "muted";

const TONE_TEXT: Record<Tone, string> = {
  accent: "text-link",
  goal: "text-goal",
  foul: "text-foul",
  card: "text-card",
  chance: "text-chance",
  info: "text-info",
  muted: "text-muted",
};
const TONE_TINT: Record<Tone, string> = {
  accent: "bg-link/16",
  goal: "bg-goal/16",
  foul: "bg-foul/16",
  card: "bg-card/16",
  chance: "bg-chance/16",
  info: "bg-info/16",
  muted: "bg-muted/16",
};
const TONE_FILL: Record<Tone, string> = {
  accent: "bg-link",
  goal: "bg-goal",
  foul: "bg-foul",
  card: "bg-card",
  chance: "bg-chance",
  info: "bg-info",
  muted: "bg-border",
};
export type { Tone };

export function Card({
  title,
  icon,
  link,
  action,
  className = "",
  children,
  delay = 0,
  id,
}: {
  title?: string;
  icon?: IconName;
  link?: { href: string; label: string };
  action?: React.ReactNode;
  className?: string;
  children: React.ReactNode;
  /** Stagger index for the enter animation (40ms each). */
  delay?: number;
  id?: string;
}) {
  return (
    <section
      id={id}
      aria-label={title}
      style={{ animationDelay: `${delay * 40}ms` }}
      className={`flex animate-sv-enter flex-col gap-4 rounded-2xl border border-border bg-panel-2 p-5 ${className}`}
    >
      {(title || link || action) && (
        <header className="flex items-center gap-2">
          {icon && <AppIcon name={icon} size={18} className="text-link" />}
          {title && (
            <h2 className="text-[13px] font-bold tracking-[0.06em] text-muted uppercase">
              {title}
            </h2>
          )}
          <span className="flex-1" />
          {action}
          {link && (
            <Link
              href={link.href}
              className="inline-flex min-h-6 items-center gap-0.5 text-[13px] font-semibold text-link hover:underline"
            >
              {link.label}
              <AppIcon name="chevronRight" size={14} />
            </Link>
          )}
        </header>
      )}
      {children}
    </section>
  );
}

export function PageHeader({
  crumb,
  title,
  sub,
  actions,
}: {
  crumb?: string;
  title: string;
  sub?: string;
  actions?: React.ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <div className="flex min-w-0 flex-1 basis-72 flex-col gap-1">
        {crumb && (
          <nav
            aria-label="Breadcrumb"
            className="flex items-center gap-1 text-xs font-medium text-muted"
          >
            <Link href="/home" className="inline-flex min-h-6 items-center hover:text-text">
              Home
            </Link>
            <AppIcon name="chevronRight" size={12} />
            <span className="text-text">{crumb}</span>
          </nav>
        )}
        <h1 className="text-[26px] leading-tight font-extrabold text-text">{title}</h1>
        {sub && <p className="text-sm text-muted">{sub}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

export function Chip({
  tone = "muted",
  icon,
  children,
  solid = false,
}: {
  tone?: Tone;
  icon?: IconName;
  children: React.ReactNode;
  solid?: boolean;
}) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold whitespace-nowrap ${
        solid ? `${TONE_FILL[tone]} text-white` : `${TONE_TINT[tone]} ${TONE_TEXT[tone]}`
      }`}
    >
      {icon && <AppIcon name={icon} size={14} />}
      {children}
    </span>
  );
}

/** Small action link styled like the Figma secondary/primary buttons, auto width. */
export function ActionLink({
  href,
  children,
  variant = "primary",
  icon,
}: {
  href: string;
  children: React.ReactNode;
  variant?: "primary" | "secondary" | "ghost";
  icon?: IconName;
}) {
  const styles = {
    primary: "bg-button text-button-text hover:bg-border",
    secondary: "border border-border bg-panel-2 text-text hover:bg-panel",
    ghost: "text-link hover:underline",
  }[variant];
  return (
    <Link
      href={href}
      className={`inline-flex h-10 items-center justify-center gap-2 rounded-lg px-4 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-link ${styles}`}
    >
      {icon && <AppIcon name={icon} size={16} />}
      {children}
    </Link>
  );
}

/** Half-open ring (270°) that draws from 0 to `value` when it appears (Figma gauges). */
export function Gauge({
  value,
  label,
  caption,
  tone = "accent",
  size = 96,
}: {
  /** 0..1, null: nothing to show yet. */
  value: number | null;
  label: string;
  caption: string;
  tone?: Tone;
  size?: number;
}) {
  const r = 42;
  const len = 2 * Math.PI * r * 0.75;
  const shown = value === null ? 0 : Math.max(0.02, Math.min(1, value));
  return (
    <figure className="flex flex-col items-center gap-1.5">
      <div className="relative" style={{ width: size, height: size }}>
        <svg viewBox="0 0 100 100" className="size-full rotate-[135deg]" aria-hidden>
          <circle
            cx="50"
            cy="50"
            r={r}
            fill="none"
            strokeWidth="7"
            strokeLinecap="round"
            className="stroke-border"
            strokeDasharray={`${len} ${2 * Math.PI * r}`}
          />
          {value !== null && (
            <circle
              cx="50"
              cy="50"
              r={r}
              fill="none"
              strokeWidth="7"
              strokeLinecap="round"
              className={`animate-sv-gauge ${TONE_TEXT[tone]}`}
              stroke="currentColor"
              strokeDasharray={`${len * shown} ${2 * Math.PI * r}`}
              style={{ ["--sv-gauge-len" as string]: `${len * shown}` }}
            />
          )}
        </svg>
        <span className="absolute inset-0 flex items-center justify-center text-lg font-extrabold text-text">
          {label}
        </span>
      </div>
      <figcaption className="text-xs font-medium text-muted">{caption}</figcaption>
    </figure>
  );
}

export function EmptyState({
  icon,
  tone = "info",
  title,
  children,
  action,
}: {
  icon: IconName;
  tone?: Tone;
  title: string;
  children?: React.ReactNode;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-3 py-6 text-center">
      <span
        className={`flex size-14 items-center justify-center rounded-full ${TONE_TINT[tone]} ${TONE_TEXT[tone]}`}
      >
        <AppIcon name={icon} size={26} />
      </span>
      <h3 className="text-[17px] font-extrabold text-text">{title}</h3>
      {children && <div className="max-w-sm text-[13px] leading-5 text-muted">{children}</div>}
      {action}
    </div>
  );
}

/** Thin progress bar. */
export function Bar({
  value,
  tone = "accent",
  className = "",
}: {
  value: number;
  tone?: Tone;
  className?: string;
}) {
  return (
    <span className={`block h-1.5 overflow-hidden rounded-full bg-border ${className}`}>
      <span
        className={`block h-full rounded-full ${TONE_FILL[tone]} transition-[width] duration-700`}
        style={{ width: `${Math.max(0, Math.min(1, value)) * 100}%` }}
      />
    </span>
  );
}

/** W / D / L squares. */
export function FormDots({ form }: { form: string[] }) {
  return (
    <span className="inline-flex gap-1" aria-label={`Form: ${form.join(" ") || "no matches"}`}>
      {form.map((r, i) => (
        <span
          key={i}
          className={`flex size-[22px] items-center justify-center rounded-md text-[11px] font-bold text-white ${
            r === "W" ? "bg-goal" : r === "L" ? "bg-foul" : "bg-border"
          }`}
        >
          {r}
        </span>
      ))}
    </span>
  );
}

/** Overall rating badge, colored by band. */
export function Ovr({ value }: { value: number }) {
  const tone: Tone =
    value >= 75 ? "goal" : value >= 68 ? "accent" : value >= 62 ? "card" : "chance";
  return (
    <span
      className={`inline-flex h-6 w-9 items-center justify-center rounded-md text-xs font-bold ${TONE_TINT[tone]} ${TONE_TEXT[tone]}`}
    >
      {value}
    </span>
  );
}

/** A note that a section's data arrives with a later game system (no invented numbers). */
export function OpensLater({ children }: { children: React.ReactNode }) {
  return (
    <p className="flex items-start gap-2 rounded-lg bg-info/16 p-3 text-[13px] leading-5 text-text">
      <AppIcon name="clock" size={16} className="mt-0.5 text-info" />
      <span>{children}</span>
    </p>
  );
}
