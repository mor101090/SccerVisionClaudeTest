import { BrandLogo } from "@/components/brand";
import { FeatureCheckIcon } from "@/components/ui/icons";
import type { KitPick } from "@/server/db/schema";

import { POSITION_BADGE, type GeneratorPosition } from "../generator";
import { kitHex } from "../kits";

/** Figma "Kit": shirt body + trim (collar and cuffs), number, socks. 100×116. */
export function KitPreview({
  kit,
  number,
  size = 100,
}: {
  kit: KitPick;
  number: string;
  size?: number;
}) {
  return (
    <svg width={size} height={size * 1.16} viewBox="0 0 100 116" aria-hidden className="shrink-0">
      <path
        d="M30 6L42 2C47.3333 7.33333 52.6667 7.33333 58 2L70 6L96 22L86 40L76 34V88H24V34L14 40L4 22L30 6Z"
        fill={kitHex(kit.shirt)}
      />
      <path
        d="M42 2C47.3333 7.33333 52.6667 7.33333 58 2L61 4C53.6667 12 46.3333 12 39 4L42 2ZM4 22L14 40L19 37L9 19L4 22ZM96 22L86 40L81 37L91 19L96 22Z"
        fill={kitHex(kit.trim)}
      />
      <text
        x="50"
        y="62"
        textAnchor="middle"
        fontSize="30"
        fontWeight="800"
        fill={kitHex(kit.number)}
      >
        {number}
      </text>
      <rect x="29" y="94" width="14" height="22" rx="3" fill={kitHex(kit.socks)} />
      <rect x="57" y="94" width="14" height="22" rx="3" fill={kitHex(kit.socks)} />
    </svg>
  );
}

/** The simple shield crest in the home colours, with the club's initials (Figma: crest). */
export function Crest({
  kit,
  initials,
  size = 48,
}: {
  kit?: KitPick | null;
  initials?: string;
  size?: number;
}) {
  const fill = kitHex(kit?.shirt ?? "red");
  const stroke = kitHex(kit?.trim ?? "black");
  return (
    <svg
      width={size}
      height={(size * 56) / 48}
      viewBox="0 0 48 56"
      aria-hidden
      className="shrink-0"
    >
      <path
        d="M24.0001 2.28564L44.5716 9.14279V26.2856C44.5716 39.9999 35.143 48.5714 24.0001 53.7142C12.8573 48.5714 3.42871 39.9999 3.42871 26.2856V9.14279L24.0001 2.28564Z"
        fill={fill}
        stroke={stroke}
        strokeWidth="2.57"
      />
      {initials && (
        <text
          x="24"
          y="31"
          textAnchor="middle"
          fontSize="11"
          fontWeight="700"
          fill={kitHex(kit?.number ?? "white")}
        >
          {initials}
        </text>
      )}
    </svg>
  );
}

export function initialsOf(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  const two = words.length > 1 ? words[0]![0]! + words[1]![0]! : (words[0] ?? "").slice(0, 2);
  return two.toUpperCase();
}

const BADGE: Record<GeneratorPosition, string> = {
  goalkeeper: "bg-chance text-accent-ink",
  fullback: "bg-[var(--mor-sky-blue)] text-accent-ink",
  defender: "bg-info text-white",
  midfielder: "bg-accent text-white",
  winger: "bg-[var(--mor-pink)] text-white",
  striker: "bg-foul text-white",
};

/** Figma POS badge: GK FB DEF MID WNG ST. */
export function PositionBadge({ position }: { position: GeneratorPosition }) {
  return (
    <span
      className={`inline-flex h-[19px] w-10 items-center justify-center rounded text-[11px] leading-none font-bold ${BADGE[position]}`}
    >
      {POSITION_BADGE[position].code}
    </span>
  );
}

/** Figma "Scoreboard" preview: clock, home, score, away, and the competition strip. */
export function ScoreboardPreview({ home, homeColor }: { home: string; homeColor: string }) {
  return (
    <div className="flex w-[255px] flex-col rounded-[10px] bg-[var(--mor-pitch-green-dark)] p-3.5">
      <div className="flex h-7 items-stretch text-base leading-none font-medium text-white">
        <span className="flex items-center bg-panel px-1.5">00:00</span>
        <span
          className="flex min-w-[62px] items-center justify-center bg-panel-2 px-2"
          style={{ boxShadow: `inset 0 -3px 0 ${homeColor}` }}
        >
          {home || "—"}
        </span>
        <span className="flex items-center bg-muted px-1.5">0-0</span>
        <span className="flex min-w-[62px] items-center justify-center bg-panel-2 px-2 shadow-[inset_0_-3px_0_var(--mor-blue)]">
          BLU
        </span>
      </div>
      <div className="flex h-[21px] w-fit items-center gap-2 bg-muted pr-3 pl-2 text-[12.5px] font-medium text-white">
        <BrandLogo className="h-3.5" />
        League match
      </div>
    </div>
  );
}

type StepId = "manager" | "club" | "kits" | "squad";
const STEPS: { id: StepId; label: string }[] = [
  { id: "manager", label: "Manager" },
  { id: "club", label: "Club" },
  { id: "kits", label: "Kits" },
  { id: "squad", label: "Squad" },
];

/** Figma "stepper": done steps get a check, the current one a ringed number, later ones muted. */
export function Stepper({ current }: { current: StepId }) {
  const at = STEPS.findIndex((s) => s.id === current);
  return (
    <ol className="flex w-full max-w-[820px] items-center gap-3.5" aria-label="Create your team">
      {STEPS.map((s, i) => {
        const done = i < at;
        const now = i === at;
        return (
          <li
            key={s.id}
            className={`flex items-center gap-3.5 ${i < STEPS.length - 1 ? "flex-1" : ""}`}
            aria-current={now ? "step" : undefined}
          >
            <span className="flex items-center gap-2.5">
              <span
                className={`flex size-7 items-center justify-center rounded-full text-[13px] font-bold ${
                  done
                    ? "bg-accent text-on-accent"
                    : now
                      ? "border-2 border-accent bg-panel-2 text-accent"
                      : "border border-border bg-panel-2 text-muted"
                }`}
              >
                {done ? <FeatureCheckIcon className="text-on-accent" /> : i + 1}
              </span>
              <span
                className={`hidden text-sm sm:inline ${now ? "font-semibold text-text" : done ? "text-text" : "text-muted"}`}
              >
                {s.label}
              </span>
            </span>
            {i < STEPS.length - 1 && (
              <span className={`h-0.5 flex-1 ${done ? "bg-accent" : "bg-border"}`} />
            )}
          </li>
        );
      })}
    </ol>
  );
}

/** Wizard card (Figma: card): Background_mor panel, 14px radius, 32px padding. */
export function TeamCard({
  title,
  subtitle,
  width,
  children,
}: {
  title: string;
  subtitle?: React.ReactNode;
  width: string;
  children: React.ReactNode;
}) {
  return (
    <section
      className={`flex w-full flex-col gap-[22px] rounded-[14px] border border-border bg-panel-2 p-5 sm:p-8 ${width}`}
    >
      <div className="flex flex-col gap-1.5">
        <h1 className="text-[26px] leading-[1.2] font-extrabold text-text">{title}</h1>
        {subtitle && <p className="text-[15px] leading-[1.45] text-muted">{subtitle}</p>}
      </div>
      {children}
    </section>
  );
}
