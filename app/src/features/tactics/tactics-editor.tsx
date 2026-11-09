"use client";

import Link from "next/link";
import { useEffect, useState, useTransition } from "react";

import { AppIcon } from "@/components/app-shell/app-icon";
import { Bar, Card, Ovr } from "@/components/app-shell/ui";

import { saveTacticsAction } from "./actions";
import { assignToFormation } from "./board";
import { FORMATION_NAMES, FORMATIONS } from "./formations";
import { MiniPitch } from "./mini-pitch";
import {
  AGGRESSION,
  AMOUNTS,
  type Amount,
  applyLocks,
  ATTACK,
  DEFAULT_ORDERS,
  DEFAULT_TEAM,
  DEFENCE,
  fitOrders,
  KIND_ROLES,
  lineLabel,
  PASSING,
  planText,
  ROLES,
  type SetPieces,
  STYLE,
  summaryLine,
  tacticLocks,
  type TeamInstructions,
  type TeamPlanView,
  TEMPO,
  widthLabel,
} from "./plan";

export type EditorPlayer = {
  number: number;
  name: string;
  short: string;
  stamina: number;
  fitness: string;
  ovr: number;
};

const TABS = [
  "Formation & lineup",
  "Team instructions",
  "Player instructions",
  "Set pieces",
] as const;

function Segmented<T extends number>({
  label,
  options,
  value,
  onChange,
  off,
}: {
  label: string;
  options: [T, string][];
  value: T;
  onChange: (v: T) => void;
  off?: Map<number, string>;
}) {
  return (
    <div
      role="radiogroup"
      aria-label={label}
      className="flex flex-wrap gap-[3px] rounded-[10px] bg-panel p-[3px]"
    >
      {options.map(([v, text]) => {
        const reason = off?.get(v);
        const on = v === value;
        return (
          <button
            key={v}
            type="button"
            role="radio"
            aria-checked={on}
            disabled={!!reason && !on}
            title={reason}
            onClick={() => onChange(v)}
            className={`flex-1 rounded-lg px-3 py-[7px] text-xs whitespace-nowrap transition-colors duration-200 disabled:cursor-not-allowed disabled:opacity-40 ${
              on ? "bg-link font-bold text-white" : "font-medium text-muted enabled:hover:text-text"
            }`}
          >
            {text}
          </button>
        );
      })}
    </div>
  );
}

function Slider({
  label,
  value,
  shown,
  ends,
  range,
  why,
  onChange,
}: {
  label: string;
  value: number;
  shown: string;
  ends: [string, string];
  range: [number, number];
  why: string;
  onChange: (v: number) => void;
}) {
  return (
    <label className="flex flex-col gap-2">
      <span className="flex items-center text-sm font-semibold text-text">
        <span className="flex-1">{label}</span>
        <span className="text-[13px] font-bold text-link">{shown}</span>
      </span>
      <input
        type="range"
        min={0}
        max={100}
        step={5}
        value={value}
        onChange={(e) => onChange(Math.min(range[1], Math.max(range[0], Number(e.target.value))))}
        className="accent-[var(--color-link)]"
      />
      <span className="flex text-[11px] text-muted">
        <span className="flex-1">{ends[0]}</span>
        {why && <span className="text-chance">{why}</span>}
        <span className="flex-1 text-right">{ends[1]}</span>
      </span>
    </label>
  );
}

export function TacticsEditor({
  initial,
  starters,
  bench,
}: {
  initial: TeamPlanView;
  /** The XI in 4-4-2 slot order (players.starter_slot). */
  starters: EditorPlayer[];
  bench: EditorPlayer[];
}) {
  const [tab, setTab] = useState(0);
  const [plan, setPlan] = useState<TeamPlanView>({
    ...initial,
    orders: fitOrders(initial.formation, initial.orders),
  });
  const [slot, setSlot] = useState<number | null>(null);
  const [saved, setSaved] = useState<string>(planText(initial));
  const [error, setError] = useState<string | null>(null);
  const [pending, start] = useTransition();
  const text = planText(plan);
  const dirty = text !== saved;

  // Unsaved changes are not lost by accident: the browser asks before a reload or closing the tab, and a click on a
  // link inside the app asks first (Next's <Link> skips navigation when the click is prevented).
  useEffect(() => {
    if (!dirty) return;
    const LEAVE = "You have unsaved tactics. Leave without saving?";
    const beforeUnload = (e: BeforeUnloadEvent) => e.preventDefault();
    const onClick = (e: MouseEvent) => {
      if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey)
        return;
      const a = (e.target as Element | null)?.closest?.("a[href]");
      if (!a || a.getAttribute("target") === "_blank" || a.hasAttribute("download")) return;
      const to = new URL(a.getAttribute("href")!, location.href);
      if (to.origin === location.origin && to.pathname === location.pathname) return;
      if (!window.confirm(LEAVE)) {
        e.preventDefault();
        e.stopPropagation();
      }
    };
    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", onClick, true);
    return () => {
      window.removeEventListener("beforeunload", beforeUnload);
      document.removeEventListener("click", onClick, true);
    };
  }, [dirty]);
  const map = assignToFormation(plan.formation);
  const xi = map.map((j) => starters[j] ?? null);
  const slots = FORMATIONS[plan.formation]!;
  const orders = plan.orders ?? fitOrders(plan.formation, null);
  const locks = tacticLocks(plan.team);
  const everyone = [...starters, ...bench];

  const setTeam = (patch: Partial<TeamInstructions>) =>
    setPlan((p) => ({ ...p, team: applyLocks({ ...p.team, ...patch }) }));
  const setOrder = (i: number, patch: Partial<(typeof orders)[number]>) =>
    setPlan((p) => {
      const next = [...(p.orders ?? fitOrders(p.formation, null))];
      next[i] = { ...(next[i] ?? DEFAULT_ORDERS), ...patch };
      return { ...p, orders: next };
    });
  const setTakers = (key: keyof SetPieces, idx: number, n: number) =>
    setPlan((p) => {
      const list = [...p.setPieces[key]];
      list[idx] = n;
      return {
        ...p,
        setPieces: { ...p.setPieces, [key]: list.filter((x, i) => x > 0 && list.indexOf(x) === i) },
      };
    });
  const save = () =>
    start(async () => {
      setError(null);
      const r = await saveTacticsAction(text);
      if (r?.error) setError(r.error);
      else setSaved(text);
    });

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex min-w-0 flex-1 basis-72 flex-col gap-1">
          <h1 className="text-[26px] font-extrabold text-text">Tactics</h1>
          <p className="text-sm text-muted">
            Saved plan · used in your next match and every friendly
          </p>
        </div>
        <button
          type="button"
          onClick={() =>
            setPlan({
              ...initial,
              team: { ...DEFAULT_TEAM },
              orders: fitOrders(plan.formation, null),
            })
          }
          className="h-10 rounded-lg px-4 text-sm font-semibold text-link hover:underline"
        >
          Reset to default
        </button>
        <button
          type="button"
          disabled={!dirty || pending}
          onClick={save}
          className="inline-flex h-10 items-center gap-2 rounded-lg bg-button px-5 text-sm font-semibold text-button-text hover:bg-border disabled:opacity-45"
        >
          {pending ? "Saving…" : dirty ? "Save tactics" : "Saved"}
          {!dirty && !pending && <AppIcon name="check" size={16} />}
        </button>
      </div>
      {error && (
        <p role="alert" className="rounded-lg bg-foul/16 p-3 text-sm text-text">
          {error}
        </p>
      )}
      <div
        role="tablist"
        aria-label="Tactics"
        className="flex gap-1 overflow-x-auto border-b border-divider"
      >
        {TABS.map((t, i) => (
          <button
            key={t}
            type="button"
            role="tab"
            id={`tactics-tab-${i}`}
            aria-selected={tab === i}
            aria-controls="tactics-panel"
            tabIndex={tab === i ? 0 : -1}
            onClick={() => setTab(i)}
            onKeyDown={(e) => {
              const next =
                e.key === "ArrowRight"
                  ? (i + 1) % TABS.length
                  : e.key === "ArrowLeft"
                    ? (i - 1 + TABS.length) % TABS.length
                    : e.key === "Home"
                      ? 0
                      : e.key === "End"
                        ? TABS.length - 1
                        : -1;
              if (next < 0) return;
              e.preventDefault();
              setTab(next);
              document.getElementById(`tactics-tab-${next}`)?.focus();
            }}
            className={`border-b-2 px-3.5 pt-2 pb-2.5 text-sm whitespace-nowrap transition-colors ${
              tab === i
                ? "border-link font-semibold text-text"
                : "border-transparent font-medium text-muted hover:text-text"
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      <div role="tabpanel" id="tactics-panel" aria-labelledby={`tactics-tab-${tab}`}>
        {tab === 0 && (
          <div className="flex flex-col gap-5 lg:flex-row">
            <Card title="Formation" className="lg:w-[430px]">
              <label className="flex items-center gap-2.5 text-[13px] text-muted">
                Shape
                <select
                  value={plan.formation}
                  onChange={(e) =>
                    setPlan((p) => ({
                      ...p,
                      formation: e.target.value,
                      orders: fitOrders(e.target.value, p.orders),
                    }))
                  }
                  className="rounded-lg border border-border bg-panel px-3 py-2 text-[13px] font-semibold text-text"
                >
                  {FORMATION_NAMES.map((f) => (
                    <option key={f}>{f}</option>
                  ))}
                </select>
              </label>
              <MiniPitch
                formation={plan.formation}
                names
                players={xi.map((p) => (p ? { number: p.number, name: p.short } : null))}
                className="aspect-[68/90]"
              />
              <p className="text-xs text-muted">
                Before kick-off every starter takes the nearest position of the shape.{" "}
                <Link href="/team/new/squad" className="font-semibold text-link hover:underline">
                  Change the starting XI
                </Link>
              </p>
            </Card>
            <Card title="Starting XI" className="flex-1 overflow-x-auto">
              <table className="w-full min-w-[560px] text-[13px]">
                <thead className="text-[11px] font-bold tracking-wide text-muted uppercase">
                  <tr>
                    <th className="w-12 py-2 text-left">Pos</th>
                    <th className="text-left">Name</th>
                    <th className="text-left">Role</th>
                    <th className="w-28 text-left">Stamina</th>
                    <th className="w-20 text-left">Fitness</th>
                    <th className="w-12 text-left">OVR</th>
                  </tr>
                </thead>
                <tbody>
                  {slots.map((s, i) => (
                    <tr key={i} className={i % 2 === 0 ? "bg-panel/60" : ""}>
                      <td className="py-2.5 pl-2 font-bold text-text">{s.abbr}</td>
                      <td className="font-semibold text-text">{xi[i]?.name ?? "—"}</td>
                      <td className="text-muted">{ROLES[orders[i]?.role ?? 0]}</td>
                      <td className="pr-4">{xi[i] && <Bar value={xi[i].stamina / 100} />}</td>
                      <td className="text-muted">{xi[i]?.fitness}</td>
                      <td>{xi[i] && <Ovr value={xi[i].ovr} />}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {bench.length > 0 && (
                <>
                  <p className="pt-2 text-[11px] font-bold tracking-[0.06em] text-muted">
                    BENCH ({bench.length})
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {bench.map((b) => (
                      <span
                        key={b.number}
                        className="rounded-lg border border-border bg-panel px-2.5 py-1.5 text-xs font-medium text-text"
                      >
                        #{b.number} {b.short}
                      </span>
                    ))}
                  </div>
                </>
              )}
            </Card>
          </div>
        )}

        {tab === 1 && (
          <div className="flex flex-col gap-5 lg:flex-row">
            <Card title="Team instructions" className="flex-1 gap-6!">
              <Slider
                label="Defensive line"
                value={plan.team.line}
                shown={lineLabel(plan.team.line)}
                ends={["Deep block", "High line"]}
                range={locks.range.line}
                why={locks.why.line}
                onChange={(v) => setTeam({ line: v })}
              />
              <Slider
                label="Width"
                value={plan.team.width}
                shown={widthLabel(plan.team.width)}
                ends={["Narrow", "Wide"]}
                range={locks.range.width}
                why={locks.why.width}
                onChange={(v) => setTeam({ width: v })}
              />
              {(
                [
                  ["Passing", "passing", PASSING.map((l, i) => [i, l])],
                  ["Tempo", "tempo", TEMPO.map((l, i) => [i, l])],
                  ["Aggression", "aggression", AGGRESSION.map((l, i) => [i, l])],
                  ["Defence", "defence", DEFENCE],
                  ["Attack", "attack", ATTACK],
                  ["Style", "special", STYLE],
                ] as [string, keyof typeof locks.off, [number, string][]][]
              ).map(([label, key, opts]) => (
                <div key={key} className="flex flex-col gap-2">
                  <span className="text-sm font-semibold text-text">{label}</span>
                  <Segmented
                    label={label}
                    options={opts}
                    value={plan.team[key]}
                    off={locks.off[key]}
                    onChange={(v) => setTeam({ [key]: v } as Partial<TeamInstructions>)}
                  />
                </div>
              ))}
            </Card>
            <div className="flex flex-col gap-5 lg:w-[360px]">
              <Card title="Summary">
                <p className="text-sm leading-[21px] text-text">{summaryLine(plan.team)}.</p>
                <p className="text-xs leading-[18px] text-muted">
                  In a live match the computer reads your tactics and the score, and answers every
                  few minutes. Greyed-out choices don&apos;t go with what you picked (hover for
                  why).
                </p>
              </Card>
              <Card title="Shape preview">
                <MiniPitch
                  formation={plan.formation}
                  players={xi.map((p) => (p ? { number: p.number } : null))}
                  className="aspect-[68/80]"
                />
              </Card>
            </div>
          </div>
        )}

        {tab === 2 && (
          <Card title="Individual orders" className="overflow-x-auto">
            <p className="text-xs text-muted">
              The role decides where a player goes, the orders how he plays.
            </p>
            <table className="w-full min-w-[860px] text-[13px]">
              <thead className="text-[11px] font-bold tracking-wide text-muted uppercase">
                <tr>
                  <th className="w-12 py-2 text-left">Pos</th>
                  <th className="text-left">Player</th>
                  <th className="w-48 text-left">Role</th>
                  <th className="w-44 text-left">Long shots</th>
                  <th className="w-44 text-left">Dribbling</th>
                  <th className="w-44 text-left">Aggression</th>
                </tr>
              </thead>
              <tbody>
                {slots.map((s, i) => {
                  const o = orders[i] ?? DEFAULT_ORDERS;
                  return (
                    <tr
                      key={i}
                      className={slot === i ? "bg-link/16" : i % 2 === 0 ? "bg-panel/60" : ""}
                      onFocus={() => setSlot(i)}
                    >
                      <td className="py-2 pl-2 font-bold text-text">{s.abbr}</td>
                      <td className="font-semibold text-text">{xi[i]?.name ?? "—"}</td>
                      <td className="pr-3">
                        <select
                          aria-label={`Role for ${s.abbr}`}
                          value={o.role}
                          onChange={(e) => setOrder(i, { role: Number(e.target.value) })}
                          className="w-full rounded-lg border border-border bg-panel px-2 py-1.5 text-xs font-medium text-text"
                        >
                          {KIND_ROLES[s.kind]!.map((r) => (
                            <option key={r} value={r}>
                              {ROLES[r]}
                            </option>
                          ))}
                        </select>
                      </td>
                      {(["longShots", "dribbling", "aggression"] as const).map((k) => (
                        <td key={k} className="pr-3">
                          <Segmented
                            label={`${k} for ${s.abbr}`}
                            options={AMOUNTS.map((l, v) => [v as Amount, l])}
                            value={o[k]}
                            onChange={(v) => setOrder(i, { [k]: v })}
                          />
                        </td>
                      ))}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </Card>
        )}

        {tab === 3 && (
          <Card title="Takers" className="max-w-[640px]">
            <p className="text-xs text-muted">
              First choice takes it; the second steps in when the first is off the pitch. Empty: the
              engine picks.
            </p>
            {(
              [
                ["Corners · right", "cornersRight"],
                ["Corners · left", "cornersLeft"],
                ["Free kicks · right", "freeKicksRight"],
                ["Free kicks · left", "freeKicksLeft"],
                ["Penalties", "penalties"],
              ] as [string, keyof SetPieces][]
            ).map(([label, key]) => (
              <div key={key} className="flex flex-wrap items-center gap-2.5">
                <span className="w-[140px] text-[13px] text-muted">{label}</span>
                {[0, 1].map((idx) => (
                  <select
                    key={idx}
                    aria-label={`${label}, choice ${idx + 1}`}
                    value={plan.setPieces[key][idx] ?? 0}
                    onChange={(e) => setTakers(key, idx, Number(e.target.value))}
                    className={`min-w-[170px] flex-1 rounded-lg border bg-panel px-2.5 py-2 text-xs font-semibold text-text ${idx === 0 ? "border-link" : "border-border"}`}
                  >
                    <option value={0}>{idx + 1}. Auto</option>
                    {everyone.map((p) => (
                      <option key={p.number} value={p.number}>
                        {idx + 1}. #{p.number} {p.short}
                      </option>
                    ))}
                  </select>
                ))}
              </div>
            ))}
          </Card>
        )}
      </div>
    </>
  );
}
