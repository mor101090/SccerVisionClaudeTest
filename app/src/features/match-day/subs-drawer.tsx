"use client";

import { useState } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import type { Replay } from "@/engine/replay/replay";

import { benchAt, elevenAt, energyAt, ratingAt } from "./derive";
import { btn, Drawer, PosBadge, RatingBadge } from "./ui";

export type PendingSub = { out: number; in: number; outName: string; inName: string };

/**
 * Figma "Live – substitutions": pick a player on the pitch and one from the bench; pairs wait for the next stoppage
 * and are made there together (one window).
 */
export function SubsDrawer({
  open,
  onClose,
  replay,
  t,
  row,
  left,
  windowsLeft,
  pending,
  onConfirm,
}: {
  open: boolean;
  onClose: () => void;
  replay: Replay;
  t: number;
  row: number[] | null;
  left: number;
  windowsLeft: number;
  pending: PendingSub[];
  onConfirm: (pairs: PendingSub[]) => void;
}) {
  const [planned, setPlanned] = useState<PendingSub[]>([]);
  const [out, setOut] = useState<number | null>(null);
  const [inn, setIn] = useState<number | null>(null);

  const eleven = elevenAt(replay, 0, t);
  const bench = benchAt(replay, 0, t);
  const busyOut = new Set([...pending, ...planned].map((p) => p.out));
  const busyIn = new Set([...pending, ...planned].map((p) => p.in));
  const room = left - planned.length;
  const blocked =
    left <= 0
      ? "No substitutions left."
      : windowsLeft <= 0 && !pending.length
        ? "No substitution windows left."
        : null;

  const pair = (o: number | null, i: number | null) => {
    if (o === null || i === null || room <= 0) return;
    const op = eleven.find((p) => p.number === o);
    const ip = bench.find((b) => b.number === i);
    if (!op || !ip) return;
    setPlanned((list) => [...list, { out: o, in: i, outName: op.name, inName: ip.shortName }]);
    setOut(null);
    setIn(null);
  };

  const close = () => {
    setPlanned([]);
    setOut(null);
    setIn(null);
    onClose();
  };

  return (
    <Drawer
      open={open}
      onClose={close}
      title="Substitutions"
      sub={`${left} left · ${windowsLeft} ${windowsLeft === 1 ? "window" : "windows"} left · made together at the next stoppage`}
      footer={
        <>
          <button
            type="button"
            className={btn("secondary")}
            onClick={() => setPlanned([])}
            disabled={!planned.length}
          >
            Clear
          </button>
          <button
            type="button"
            className={btn("primary")}
            disabled={!planned.length || !!blocked}
            onClick={() => {
              onConfirm(planned);
              close();
            }}
          >
            Confirm {planned.length || ""} {planned.length === 1 ? "change" : "changes"}
          </button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {blocked ? (
          <p role="status" className="rounded-lg bg-foul/15 p-3 text-[13px] text-text">
            {blocked}
          </p>
        ) : (
          <p className="flex items-start gap-2 rounded-lg border border-accent/60 bg-accent/12 p-3 text-[13px] leading-5 text-text">
            <AppIcon name="info" size={16} className="mt-0.5 text-link" />
            {planned.length
              ? `Planned: ${planned.map((p) => `${p.inName} on for ${p.outName}`).join("; ")}. Pick another pair or confirm.`
              : "Pick a player on the pitch, then who comes on."}
          </p>
        )}
        {pending.length > 0 && (
          <p className="text-xs text-muted">
            Waiting for a stoppage: {pending.map((p) => `${p.inName} for ${p.outName}`).join(", ")}
          </p>
        )}
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <h3 className="text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
              On the pitch
            </h3>
            {eleven.map((p) => {
              const e = energyAt(row, p.index);
              const busy = busyOut.has(p.number);
              return (
                <button
                  key={p.index}
                  type="button"
                  disabled={busy || !!blocked}
                  aria-pressed={out === p.number}
                  onClick={() => {
                    const o = out === p.number ? null : p.number;
                    setOut(o);
                    pair(o, inn);
                  }}
                  className={`flex items-center gap-2 rounded-lg border p-2 text-left focus-visible:outline-2 focus-visible:outline-link disabled:opacity-40 ${out === p.number ? "border-selected bg-selected/20" : "border-transparent hover:bg-panel-2"}`}
                >
                  <span className="w-5 text-xs text-muted tabular-nums">{p.number}</span>
                  <span className="flex min-w-0 flex-1 flex-col gap-1">
                    <span className="truncate text-[13px] font-semibold text-text">{p.name}</span>
                    <span className="flex items-center gap-2">
                      <PosBadge role={p.role} />
                      {e !== null && (
                        <span
                          className="h-1 w-12 overflow-hidden rounded-full bg-border"
                          title={`Energy ${e}%`}
                        >
                          <span
                            className={`block h-full ${e >= 70 ? "bg-goal" : e >= 50 ? "bg-card" : "bg-foul"}`}
                            style={{ width: `${e}%` }}
                          />
                        </span>
                      )}
                    </span>
                  </span>
                  <RatingBadge value={ratingAt(row, p.index)} />
                </button>
              );
            })}
          </div>
          <div className="flex flex-col gap-1.5">
            <h3 className="text-[11px] font-bold tracking-[0.06em] text-muted uppercase">Bench</h3>
            {bench.length ? (
              bench.map((b) => {
                const busy = b.used || busyIn.has(b.number);
                return (
                  <button
                    key={b.number}
                    type="button"
                    disabled={busy || !!blocked}
                    aria-pressed={inn === b.number}
                    title={b.used ? "Already played" : undefined}
                    onClick={() => {
                      const i = inn === b.number ? null : b.number;
                      setIn(i);
                      pair(out, i);
                    }}
                    className={`flex items-center gap-2 rounded-lg border p-2 text-left focus-visible:outline-2 focus-visible:outline-link disabled:opacity-40 ${inn === b.number ? "border-selected bg-selected/20" : "border-transparent hover:bg-panel-2"}`}
                  >
                    <span className="w-5 text-xs text-muted tabular-nums">{b.number}</span>
                    <span className="flex min-w-0 flex-1 flex-col gap-1">
                      <span className="truncate text-[13px] font-semibold text-text">
                        {b.shortName}
                      </span>
                      <PosBadge role={b.role} />
                    </span>
                  </button>
                );
              })
            ) : (
              <p className="text-[13px] text-muted">No substitutes named.</p>
            )}
          </div>
        </div>
      </div>
    </Drawer>
  );
}
