import Image from "next/image";
import Link from "next/link";

import { AppIcon, type IconName } from "@/components/app-shell/app-icon";
import { Chip } from "@/components/app-shell/ui";
import { flagUrl } from "@/features/team/countries";

import type { Achievement, Record_ } from "./achievements";

/** Pieces of the profile, club and league screens (Figma "Profile, clubs & leagues – screens"). Tokens only. */

export function initialsOf(name: string): string {
  const w = name.replace(/[_@]/g, " ").split(/\s+/).filter(Boolean);
  return ((w[0]?.[0] ?? "?") + (w[1]?.[0] ?? w[0]?.[1] ?? "")).toUpperCase();
}

export function Avatar({ name, size = 40 }: { name: string; size?: number }) {
  return (
    <span
      aria-hidden
      className="flex shrink-0 items-center justify-center rounded-full bg-selected font-bold text-white"
      style={{ width: size, height: size, fontSize: Math.max(11, size * 0.32) }}
    >
      {initialsOf(name)}
    </span>
  );
}

/** Club crest: the club's home shirt colour with its short name (data colours, not palette tokens). */
export function Crest({
  name,
  shortName,
  color,
  ink,
  size = 44,
  square = false,
}: {
  name: string;
  shortName?: string;
  color?: string | null;
  ink?: string | null;
  size?: number;
  square?: boolean;
}) {
  return (
    <span
      aria-hidden
      className={`flex shrink-0 items-center justify-center border border-white/20 font-extrabold ${square ? "rounded-2xl" : "rounded-full"} ${color ? "" : "bg-accent text-on-accent"}`}
      style={{
        width: size,
        height: size,
        fontSize: size * 0.3,
        ...(color ? { background: color, color: ink ?? "#fff" } : {}),
      }}
    >
      {(shortName ?? initialsOf(name)).slice(0, 3)}
    </span>
  );
}

export function Flag({ code }: { code: string | null }) {
  if (!code) return null;
  return (
    <Image
      src={flagUrl(code)}
      alt=""
      width={18}
      height={13}
      unoptimized
      className="inline-block h-[13px] w-[18px] rounded-[2px] object-cover"
    />
  );
}

export function StatTile({
  value,
  label,
  tone = "text-text",
}: {
  value: string | number;
  label: string;
  tone?: string;
}) {
  return (
    <div className="flex min-w-0 flex-1 basis-28 flex-col gap-0.5 rounded-xl border border-border/60 bg-panel-2 px-4 py-3.5">
      <span className={`text-[22px] leading-tight font-extrabold tabular-nums ${tone}`}>
        {value}
      </span>
      <span className="text-xs text-muted">{label}</span>
    </div>
  );
}

export function RecordTiles({ r }: { r: Record_ }) {
  return (
    <div className="flex flex-wrap gap-3">
      <StatTile value={r.played} label="Matches" />
      <StatTile value={r.won} label="Wins" tone="text-goal" />
      <StatTile value={r.drawn} label="Draws" />
      <StatTile value={r.lost} label="Defeats" tone="text-foul" />
      <StatTile value={r.played ? `${r.goalsFor}–${r.goalsAgainst}` : "—"} label="Goals" />
      <StatTile
        value={r.bestRating !== null ? r.bestRating.toFixed(1) : "—"}
        label="Best player rating"
      />
    </div>
  );
}

const RESULT = {
  W: "bg-goal text-accent-ink",
  D: "bg-border text-text",
  L: "bg-foul text-white",
} as const;
export function ResultDot({ r }: { r: "W" | "D" | "L" }) {
  return (
    <span
      className={`flex size-6 shrink-0 items-center justify-center rounded-md text-[11px] font-bold ${RESULT[r]}`}
    >
      {r}
    </span>
  );
}

export function BadgeTile({ a }: { a: Achievement }) {
  return (
    <div
      className={`flex flex-col gap-2 rounded-xl bg-panel-2 p-4 ${a.earned ? "" : "opacity-55"}`}
      aria-label={`${a.title}: ${a.earned ? "earned" : "locked"}`}
    >
      <span
        className={`flex size-10 items-center justify-center rounded-full ${a.earned ? "bg-accent text-on-accent" : "bg-border text-text"}`}
      >
        <AppIcon name={a.icon} size={20} />
      </span>
      <span className="text-sm font-bold text-text">{a.title}</span>
      <span className="text-xs text-muted">{a.how}</span>
      {a.earned ? (
        <span className="self-start">
          <Chip tone="goal">Earned</Chip>
        </span>
      ) : (
        <span className="text-xs font-semibold text-muted">{a.progress ?? "Locked"}</span>
      )}
    </div>
  );
}

/** A list row with a leading element, two lines of text and an optional trailing element. */
export function Row({
  lead,
  title,
  sub,
  trail,
  href,
}: {
  lead?: React.ReactNode;
  title: React.ReactNode;
  sub?: React.ReactNode;
  trail?: React.ReactNode;
  href?: string;
}) {
  const body = (
    <>
      {lead}
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-semibold text-text">{title}</span>
        {sub && <span className="block truncate text-xs text-muted">{sub}</span>}
      </span>
      {trail}
    </>
  );
  const cls = "flex items-center gap-3 border-b border-divider py-2.5 last:border-0";
  return href ? (
    <Link
      href={href}
      className={`${cls} -mx-2 rounded-lg px-2 hover:bg-panel focus-visible:outline-2 focus-visible:outline-link`}
    >
      {body}
    </Link>
  ) : (
    <div className={cls}>{body}</div>
  );
}

export function IconBubble({ icon, on = true }: { icon: IconName; on?: boolean }) {
  return (
    <span
      className={`flex size-8 shrink-0 items-center justify-center rounded-full ${on ? "bg-accent text-on-accent" : "bg-border text-text"}`}
    >
      <AppIcon name={icon} size={16} />
    </span>
  );
}

export function when(d: Date): string {
  return d.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
}

export function monthYear(d: Date): string {
  return d.toLocaleDateString("en-GB", { month: "short", year: "numeric" });
}
