"use client";

import { useState } from "react";

import type { Replay } from "@/engine/replay/replay";
import { FORMATION_NAMES, FORMATIONS } from "@/features/tactics/formations";
import { MiniPitch } from "@/features/tactics/mini-pitch";
import { lineLabel, tacticLocks, widthLabel } from "@/features/tactics/plan";

import { elevenAt } from "./derive";
import type { Changes, PlanChanges } from "./plan-changes";
import { btn, Drawer } from "./ui";

/** Each slot of `to` takes the nearest remaining player of `from` (the way the engine reshapes a team). */
export function reshape<T>(from: string, to: string, players: T[]): T[] {
  const a = FORMATIONS[from] ?? FORMATIONS["4-4-2"]!;
  const b = FORMATIONS[to] ?? a;
  const free = new Set(a.map((_, i) => i).filter((i) => i > 0));
  return b.map((slot, i) => {
    if (i === 0) return players[0]!;
    let best = -1;
    let bestD = Infinity;
    for (const j of free) {
      const d = (a[j]!.forward - slot.forward) ** 2 + (a[j]!.lateral - slot.lateral) ** 2;
      if (d < bestD) {
        bestD = d;
        best = j;
      }
    }
    free.delete(best);
    return players[best]!;
  });
}

function Slider({
  label,
  value,
  text,
  range,
  why,
  onChange,
}: {
  label: string;
  value: number;
  text: string;
  range: [number, number];
  why: string;
  onChange: (v: number) => void;
}) {
  const locked = range[0] > 0 || range[1] < 100;
  return (
    <label className="flex flex-col gap-1.5">
      <span className="flex justify-between text-[13px]">
        <span className="text-text">{label}</span>
        <span className="font-semibold text-link">{text}</span>
      </span>
      <input
        type="range"
        min={0}
        max={100}
        step={5}
        value={value}
        onChange={(e) => onChange(Math.max(range[0], Math.min(range[1], Number(e.target.value))))}
        className="accent-[var(--color-selected)]"
      />
      {locked && why && <span className="text-xs text-muted">{why}</span>}
    </label>
  );
}

/** Figma "Live – tactics": shape and line for the rest of this match; the saved plan is not changed. */
export function TacticsDrawer({
  open,
  onClose,
  replay,
  t,
  plan,
}: {
  open: boolean;
  onClose: () => void;
  replay: Replay;
  t: number;
  plan: PlanChanges;
}) {
  const [draft, setDraft] = useState<Changes | null>(null);
  const current = plan.changes;
  if (!current || !plan.base) return null;
  const d = draft ?? current;
  const locks = tacticLocks({ ...plan.base.team, line: d.line, width: d.width });

  // The eleven in the shape they play now (header formation, with substitutions), moved into the draft shape.
  const header = replay.home as { formation?: string };
  const startShape = header.formation ?? "4-4-2";
  const now = elevenAt(replay, 0, t);
  const bySlot: { number: number; name: string }[] = [];
  replay.home.players.slice(0, 11).forEach((p, i) => {
    const slot = (p as { slot?: number }).slot ?? i;
    bySlot[slot] = { number: now[i]!.number, name: now[i]!.name };
  });
  const playing = reshape(startShape, current.formation, bySlot);
  const shown = reshape(current.formation, d.formation, playing);

  const close = () => {
    setDraft(null);
    onClose();
  };

  return (
    <Drawer
      open={open}
      onClose={close}
      title="Tactics"
      sub="Changes reach the players at the next stoppage. Your saved tactics don't change."
      footer={
        <>
          <button
            type="button"
            className={btn("secondary")}
            onClick={() => {
              plan.resetToSaved();
              close();
            }}
          >
            Reset to saved plan
          </button>
          <button
            type="button"
            className={btn("primary")}
            disabled={!draft}
            onClick={() => {
              if (draft) plan.apply(draft);
              close();
            }}
          >
            Apply at next stoppage
          </button>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        <label className="flex items-center gap-3 text-[13px] text-muted">
          Formation
          <select
            value={d.formation}
            onChange={(e) => setDraft({ ...d, formation: e.target.value })}
            className="h-10 min-w-40 rounded-lg border border-border bg-panel-2 px-3 text-sm font-semibold text-text"
          >
            {FORMATION_NAMES.map((f) => (
              <option key={f} value={f}>
                {f}
              </option>
            ))}
          </select>
        </label>
        <MiniPitch
          formation={d.formation}
          players={shown}
          names
          className="mx-auto aspect-[68/105] w-full max-w-[360px]"
        />
        <Slider
          label="Defensive line"
          value={d.line}
          text={lineLabel(d.line)}
          range={locks.range.line}
          why={locks.why.line}
          onChange={(line) => setDraft({ ...d, line })}
        />
        <Slider
          label="Width"
          value={d.width}
          text={widthLabel(d.width)}
          range={locks.range.width}
          why={locks.why.width}
          onChange={(width) => setDraft({ ...d, width })}
        />
        <p className="text-xs text-muted">
          Mentality and touchline instructions (in the bar under the pitch) apply on top of these.
        </p>
      </div>
    </Drawer>
  );
}
