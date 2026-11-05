"use client";

import { useState } from "react";

import { Alert } from "@/components/ui/alert";
import { Button, ButtonLink } from "@/components/ui/button";
import { useAuthForm } from "@/features/auth/use-auth-form";
import type { TeamKits } from "@/server/db/schema";

import { saveKitsAction } from "../actions";
import { isBlocked, KIT_COLORS, repairKits, type KitName, type KitPart } from "../kits";
import { KitPreview } from "./pieces";

const KITS: { id: KitName; label: string; number: string }[] = [
  { id: "home", label: "Home kit", number: "10" },
  { id: "away", label: "Away kit", number: "10" },
  { id: "keeper", label: "Goalkeeper", number: "1" },
];
const PARTS: { id: KitPart; label: string }[] = [
  { id: "shirt", label: "Shirt" },
  { id: "trim", label: "Trim" },
  { id: "socks", label: "Socks" },
  { id: "number", label: "Number" },
];

/** Figma "palette": 32px swatches; picked = white ring + glow, blocked = faded with a slash. */
function Palette({
  kits,
  kit,
  part,
  onPick,
}: {
  kits: TeamKits;
  kit: KitName;
  part: KitPart;
  onPick: (color: string) => void;
}) {
  const name = `${kit}.${part}`;
  return (
    <fieldset className="flex flex-col gap-1.5">
      <legend className="mb-1.5 text-xs font-semibold tracking-[0.4px] text-muted">
        {PARTS.find((p) => p.id === part)!.label}
      </legend>
      <div className="flex flex-wrap gap-1.5">
        {KIT_COLORS.map((c) => {
          const blocked = isBlocked(kits, kit, part, c.id);
          const picked = kits[kit][part] === c.id;
          return (
            <label
              key={c.id}
              title={blocked ? `${c.label}: too close to another kit` : c.label}
              className={`relative size-8 rounded-md has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-link ${
                blocked ? "cursor-not-allowed opacity-30" : "cursor-pointer"
              } ${picked ? "border-2 border-white shadow-[0_0_0_2px_var(--color-accent)]" : "border border-white/80"}`}
              style={{ background: c.hex }}
            >
              <input
                type="radio"
                name={name}
                value={c.id}
                checked={picked}
                disabled={blocked}
                onChange={() => onPick(c.id)}
                aria-label={`${PARTS.find((p) => p.id === part)!.label}: ${c.label}`}
                className="sr-only"
              />
              {blocked && (
                <svg viewBox="0 0 32 32" className="absolute inset-0" aria-hidden>
                  <path d="M6 26L26 6" stroke="white" strokeWidth="2" />
                </svg>
              )}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

/** Figma: Create team – 3 Kits. */
export function KitsForm({ initial }: { initial: TeamKits }) {
  const { state, pending, formProps } = useAuthForm(saveKitsAction);
  const [kits, setKits] = useState<TeamKits>(initial);

  return (
    <form {...formProps} className="flex flex-col gap-[22px]">
      {state?.error && <Alert tone="error">{state.error}</Alert>}
      <div className="grid gap-5 lg:grid-cols-3">
        {KITS.map((k) => (
          <div
            key={k.id}
            className="flex flex-col gap-3.5 rounded-xl border border-border bg-panel p-[18px]"
          >
            <div className="flex items-center gap-4">
              <KitPreview kit={kits[k.id]} number={k.number} />
              <h2 className="text-[17px] font-bold text-text">{k.label}</h2>
            </div>
            {PARTS.map((p) => (
              <Palette
                key={p.id}
                kits={kits}
                kit={k.id}
                part={p.id}
                onPick={(color) =>
                  setKits((all) => repairKits({ ...all, [k.id]: { ...all[k.id], [p.id]: color } }))
                }
              />
            ))}
          </div>
        ))}
      </div>
      <p className="text-[13px] text-muted">
        The other team&apos;s keeper and the referees are dressed in colours that clash with none of
        yours.
      </p>
      <div className="grid gap-3 sm:grid-cols-2">
        <ButtonLink href="/team/new/club" variant="secondary">
          ← Back
        </ButtonLink>
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : "Generate my squad"}
        </Button>
      </div>
    </form>
  );
}
