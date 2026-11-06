import { boardPoint } from "./board";
import { FORMATIONS } from "./formations";

export type PitchPlayer = { number: number; name?: string; captain?: boolean };

/**
 * A vertical pitch with the XI in its formation (Figma: Tactics card / Tactics – formation & lineup). `players` is in
 * the formation's slot order. `onPick` makes the shirts buttons (the tactics editor swaps two picked players).
 */
export function MiniPitch({
  formation,
  players,
  names = false,
  picked,
  onPick,
  className = "",
}: {
  formation: string;
  players: (PitchPlayer | null)[];
  names?: boolean;
  picked?: number | null;
  onPick?: (slot: number) => void;
  className?: string;
}) {
  const slots = FORMATIONS[formation] ?? FORMATIONS["4-4-2"]!;
  return (
    <div
      className={`relative overflow-hidden rounded-[10px] bg-[var(--mor-pitch-green)] ${className}`}
      role="img"
      aria-label={`${formation} formation`}
    >
      <svg
        viewBox="0 0 68 105"
        preserveAspectRatio="none"
        className="absolute inset-0 size-full"
        aria-hidden
      >
        <g fill="none" stroke="var(--color-white)" strokeOpacity="0.55" strokeWidth="0.45">
          <rect x="2.5" y="2.5" width="63" height="100" />
          <line x1="2.5" y1="52.5" x2="65.5" y2="52.5" />
          <circle cx="34" cy="52.5" r="9.15" />
          <rect x="13.85" y="2.5" width="40.3" height="16.5" />
          <rect x="13.85" y="86" width="40.3" height="16.5" />
          <rect x="24.85" y="2.5" width="18.3" height="5.5" />
          <rect x="24.85" y="97" width="18.3" height="5.5" />
        </g>
      </svg>
      {slots.map((slot, i) => {
        const p = players[i];
        const { x, y } = boardPoint(slot);
        const shirt = (
          <span
            className={`flex size-7 items-center justify-center rounded-full text-[11px] font-bold text-accent-ink shadow transition-transform duration-200 ${
              i === 0 ? "bg-card" : "bg-white"
            } ${picked === i ? "scale-125 ring-2 ring-link" : ""}`}
          >
            {p?.number ?? slot.abbr}
          </span>
        );
        return (
          <div
            key={i}
            className="absolute flex -translate-x-1/2 -translate-y-1/2 flex-col items-center gap-0.5"
            style={{ left: `${x * 100}%`, top: `${y * 100}%` }}
          >
            {onPick ? (
              <button
                type="button"
                onClick={() => onPick(i)}
                aria-pressed={picked === i}
                aria-label={`${slot.abbr}: ${p?.name ?? "empty"}${picked === i ? " (picked)" : ""}`}
                className="rounded-full focus-visible:outline-2 focus-visible:outline-white"
              >
                {shirt}
              </button>
            ) : (
              shirt
            )}
            {names && p?.name && (
              <span className="rounded-md bg-bg/75 px-1.5 py-px text-[10px] font-semibold whitespace-nowrap text-text">
                {p.name}
                {p.captain ? " (C)" : ""}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
}
