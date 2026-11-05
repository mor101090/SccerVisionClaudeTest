"use client";

import { useEffect, useRef, useState, useTransition } from "react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { TeamKits } from "@/server/db/schema";

import {
  regenerateSquadAction,
  setCaptainAction,
  setStarterAction,
  startPlayingAction,
  type TeamFormState,
} from "../actions";
import {
  groupAverage,
  POSITION_ORDER,
  type GeneratorPlayer,
  type GeneratorPosition,
} from "../generator";
import { Crest, initialsOf, KitPreview, PositionBadge } from "./pieces";

export type SquadRow = {
  id: string;
  starterSlot: number | null;
  benchOrder: number | null;
  p: GeneratorPlayer;
};

type Props = {
  team: {
    name: string;
    shortName: string;
    kits: TeamKits;
    regenerationsLeft: number;
    captainId: string | null;
  };
  manager: string;
  squad: SquadRow[];
};

/** 80+ light green, 65+ text, 50+ muted, under 50 red (Figma: Columns legend). */
function tone(v: number | null): string {
  if (v === null) return "text-muted/50";
  if (v >= 80) return "text-goal";
  if (v >= 65) return "text-text";
  if (v >= 50) return "text-muted";
  return "text-foul";
}

const BAR = (v: number) =>
  v >= 80 ? "bg-goal" : v >= 65 ? "bg-accent" : v >= 50 ? "bg-muted" : "bg-foul";

function LevelPill({ level }: { level: GeneratorPlayer["level"] }) {
  if (level === "Excellent") {
    return (
      <span className="rounded-full bg-card/20 px-2 py-0.5 text-xs text-card">★ Excellent</span>
    );
  }
  if (level === "Weaker")
    return <span className="rounded-full bg-foul/25 px-2 py-0.5 text-xs text-foul">Weaker</span>;
  return <span className="rounded-full bg-panel px-2 py-0.5 text-xs text-muted">Normal</span>;
}

/** The weaker foot in stars, 1-5 (filled on the server by withStars). */
const stars = (p: GeneratorPlayer) => {
  const n = Math.max(1, Math.min(5, p.preferred_foot.weak_foot_stars ?? 1));
  return "★".repeat(n) + "☆".repeat(5 - n);
};

const foot = (p: GeneratorPlayer) =>
  `${p.preferred_foot.dominant_foot === "Left" ? "L" : "R"} · ${p.preferred_foot.weak_foot_stars ?? "-"}★`;

const COLUMNS: [string, string][] = [
  ["MEN", "mental"],
  ["ATH", "athletic"],
  ["DEF", "defense"],
  ["ATT", "attack"],
  ["PLY", "playmaking"],
  ["SET", "set_pieces"],
  ["GK", "goalkeeping"],
];

/** Suggested XI rows (Figma): slot kinds, each listing its starters. */
const XI_ROWS: { position: GeneratorPosition; slots: number[] }[] = [
  { position: "goalkeeper", slots: [0] },
  { position: "fullback", slots: [4, 1] },
  { position: "defender", slots: [2, 3] },
  { position: "winger", slots: [5, 8] },
  { position: "midfielder", slots: [6, 7] },
  { position: "striker", slots: [9, 10] },
];

const DETAIL_GROUPS: { title: string; group: string; keys: [string, string][] }[] = [
  {
    title: "GOALKEEPING",
    group: "goalkeeping",
    keys: [
      ["aerial_ability", "Aerial ability"],
      ["handling", "Handling"],
      ["kicking", "Kicking"],
      ["one_on_one", "One on one"],
      ["reflexes", "Reflexes"],
    ],
  },
  {
    title: "DEFENSE",
    group: "defense",
    keys: [
      ["tackling", "Tackling"],
      ["marking", "Marking"],
      ["positioning", "Positioning"],
    ],
  },
  {
    title: "ATHLETIC",
    group: "athletic",
    keys: [
      ["physical_power", "Physical power"],
      ["ball_control", "Ball control"],
      ["dribbling", "Dribbling"],
      ["jumping", "Jumping"],
      ["acceleration", "Acceleration"],
      ["speed", "Speed"],
      ["heading", "Heading"],
    ],
  },
  {
    title: "PLAYMAKING",
    group: "playmaking",
    keys: [
      ["creative", "Creativity"],
      ["passing", "Passing"],
      ["crossing", "Crossing"],
    ],
  },
  {
    title: "SET PIECES",
    group: "set_pieces",
    keys: [
      ["free_kicks", "Free kicks"],
      ["penalty", "Penalty"],
    ],
  },
  {
    title: "MENTAL",
    group: "mental",
    keys: [
      ["aggression", "Aggression"],
      ["teamwork", "Teamwork"],
      ["decisions", "Decisions"],
      ["composure", "Composure"],
    ],
  },
  {
    title: "ATTACK",
    group: "attack",
    keys: [
      ["finishing", "Finishing"],
      ["long_shots", "Long shots"],
      ["off_the_ball", "Off the ball"],
    ],
  },
];

const FITNESS = { Low: "Low", Moderate: "Moderate", Peak: "Peak" } as const;

/** Figma: Create team – 6 Player details (right-hand drawer). */
function PlayerDrawer({
  row,
  kits,
  captain,
  onClose,
  onAction,
  busy,
}: {
  row: SquadRow;
  kits: TeamKits;
  captain: boolean;
  onClose: () => void;
  onAction: (fn: (id: string) => Promise<TeamFormState>) => void;
  busy: boolean;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const p = row.p;
  useEffect(() => {
    ref.current?.showModal();
  }, []);
  const kit = p.position === "goalkeeper" ? kits.keeper : kits.home;
  const tiles: [string, string][] = [
    ["Shirt", `#${p.shirt_number}`],
    ["Age", String(p.age)],
    ["Height", `${p.height} cm`],
    ["Foot", `${p.preferred_foot.dominant_foot} · weak ${stars(p)}`],
    ["Stamina", String(p.stamina)],
    ["Fitness", FITNESS[p.fitness]],
  ];
  return (
    <dialog
      ref={ref}
      onClose={onClose}
      aria-labelledby="player-name"
      className="m-0 ml-auto h-dvh max-h-none w-[440px] max-w-full border-l border-border bg-panel-2 p-0 text-text backdrop:bg-black/55"
    >
      <div className="flex h-full flex-col">
        <div className="flex flex-1 flex-col gap-5 overflow-y-auto p-6">
          <div className="flex items-start gap-4">
            <KitPreview kit={kit} number={String(p.shirt_number)} size={64} />
            <div className="flex flex-1 flex-col gap-2">
              <h2 id="player-name" className="text-[22px] leading-tight font-extrabold">
                {p.name}
                {captain && (
                  <span className="ml-2 rounded bg-chance px-1.5 align-middle text-xs text-accent-ink">
                    C
                  </span>
                )}
              </h2>
              <div className="flex items-center gap-2">
                <PositionBadge position={p.position} />
                <LevelPill level={p.level} />
              </div>
            </div>
            <button
              type="button"
              onClick={() => ref.current?.close()}
              aria-label="Close"
              className="rounded p-1 text-muted hover:text-text"
            >
              <svg width="20" height="20" viewBox="0 0 20 20" aria-hidden>
                <path
                  d="M4 4l12 12M16 4L4 16"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                />
              </svg>
            </button>
          </div>
          <dl className="grid grid-cols-3 gap-2">
            {tiles.map(([k, v]) => (
              <div key={k} className="rounded-lg bg-panel px-3 py-2">
                <dt className="text-xs text-muted">{k}</dt>
                <dd className="text-[15px] font-semibold">{v}</dd>
              </div>
            ))}
          </dl>
          {DETAIL_GROUPS.filter((g) => p.stats[g.group]).map((g) => (
            <section key={g.group} className="flex flex-col gap-1.5">
              <h3 className="text-xs font-semibold tracking-[1px] text-muted">
                {g.title} · {groupAverage(p, g.group)}
              </h3>
              {g.keys.map(([key, label]) => {
                const v = p.stats[g.group]![key] ?? 0;
                return (
                  <div
                    key={key}
                    className="grid grid-cols-[120px_1fr_28px] items-center gap-3 text-sm"
                  >
                    <span className="text-muted">{label}</span>
                    <span className="h-1 rounded-full bg-panel">
                      <span
                        className={`block h-full rounded-full ${BAR(v)}`}
                        style={{ width: `${v}%` }}
                      />
                    </span>
                    <span className="text-right font-medium">{v}</span>
                  </div>
                );
              })}
            </section>
          ))}
        </div>
        <div className="grid grid-cols-2 gap-3 border-t border-border p-6">
          <Button
            variant="secondary"
            disabled={busy || captain}
            onClick={() => onAction(setCaptainAction)}
          >
            {captain ? "Captain" : "Make captain"}
          </Button>
          <Button
            disabled={busy || row.starterSlot !== null}
            onClick={() => onAction(setStarterAction)}
          >
            {row.starterSlot !== null ? "Starter" : "Set as starter"}
          </Button>
        </div>
      </div>
    </dialog>
  );
}

/** Figma: Create team – 5 Your squad. */
export function SquadView({ team, manager, squad }: Props) {
  const [openId, setOpenId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, start] = useTransition();

  const rows = [...squad].sort(
    (a, b) =>
      POSITION_ORDER.indexOf(a.p.position) - POSITION_ORDER.indexOf(b.p.position) ||
      Number(a.starterSlot === null) - Number(b.starterSlot === null) ||
      a.p.shirt_number - b.p.shirt_number,
  );
  const bySlot = new Map(
    squad.filter((s) => s.starterSlot !== null).map((s) => [s.starterSlot!, s]),
  );
  const avg = (f: (p: GeneratorPlayer) => number) =>
    squad.reduce((a, s) => a + f(s.p), 0) / squad.length;
  const stats: [string, string][] = [
    [String(squad.length), "Players"],
    [avg((p) => p.age).toFixed(1), "Average age"],
    [String(Math.round(avg((p) => p.stamina))), "Average stamina"],
    [String(squad.filter((s) => s.p.level === "Excellent").length), "★ Excellent"],
    [String(squad.filter((s) => s.p.level === "Weaker").length), "Weaker"],
  ];
  const open = squad.find((s) => s.id === openId) ?? null;

  const act = (fn: () => Promise<TeamFormState>) =>
    start(async () => {
      setError(null);
      const r = await fn();
      if (r?.error) setError(r.error);
    });

  return (
    <div className="flex w-full max-w-[1368px] flex-col gap-5">
      <header className="flex flex-wrap items-center gap-6 rounded-[14px] border border-border bg-panel-2 px-6 py-5">
        <Crest kit={team.kits.home} initials={initialsOf(team.name)} size={44} />
        <div className="flex flex-1 flex-col">
          <h1 className="text-[26px] leading-tight font-extrabold">{team.name}</h1>
          <p className="text-sm text-muted">
            {team.shortName} · Manager {manager} · 4-4-2
          </p>
        </div>
        <dl className="flex flex-wrap gap-8 text-center">
          {stats.map(([v, k]) => (
            <div key={k} className="flex flex-col-reverse">
              <dt className="text-xs text-muted">{k}</dt>
              <dd className="text-[22px] font-bold">{v}</dd>
            </div>
          ))}
        </dl>
      </header>

      {error && <Alert tone="error">{error}</Alert>}

      <div className="grid gap-5 xl:grid-cols-[1fr_346px]">
        <div className="overflow-x-auto rounded-[14px] border border-border bg-panel-2">
          <table className="w-full min-w-[900px] text-sm">
            <caption className="sr-only">Your 22 players. Select a player for details.</caption>
            <thead>
              <tr className="text-left text-[11px] font-semibold tracking-[0.6px] text-muted">
                {[
                  "#",
                  "NAME",
                  "POS",
                  "AGE",
                  "HEIGHT",
                  "FOOT",
                  "LEVEL",
                  "STAMINA",
                  ...COLUMNS.map((c) => c[0]),
                ].map((h) => (
                  <th
                    key={h}
                    scope="col"
                    className="px-2.5 py-3 font-semibold first:pl-5 [&:nth-child(n+8)]:text-center"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((s) => {
                const p = s.p;
                return (
                  <tr
                    key={s.id}
                    onClick={() => setOpenId(s.id)}
                    className={`cursor-pointer border-t border-border/40 hover:bg-panel ${openId === s.id ? "bg-accent/30" : ""}`}
                  >
                    <td className="py-2.5 pr-2.5 pl-5 text-right text-muted tabular-nums">
                      {p.shirt_number}
                    </td>
                    <td className="px-2.5 py-2.5">
                      <button
                        type="button"
                        className="text-left font-medium text-text hover:underline focus-visible:outline-2 focus-visible:outline-link"
                        onClick={(e) => {
                          e.stopPropagation();
                          setOpenId(s.id);
                        }}
                      >
                        {p.name}
                      </button>
                      {s.starterSlot !== null && (
                        <span
                          className="ml-2 text-[10px] font-bold text-accent"
                          title="In the starting XI"
                        >
                          XI
                        </span>
                      )}
                      {team.captainId === s.id && (
                        <span
                          className="ml-1 rounded bg-chance px-1 text-[10px] font-bold text-accent-ink"
                          title="Captain"
                        >
                          C
                        </span>
                      )}
                    </td>
                    <td className="px-2.5">
                      <PositionBadge position={p.position} />
                    </td>
                    <td className="px-2.5 text-right tabular-nums">{p.age}</td>
                    <td className="px-2.5 text-right tabular-nums">{p.height} cm</td>
                    <td className="px-2.5 text-center tabular-nums">{foot(p)}</td>
                    <td className="px-2.5">
                      <LevelPill level={p.level} />
                    </td>
                    <td className="px-2.5 text-center tabular-nums">{p.stamina}</td>
                    {COLUMNS.map(([k, g]) => {
                      const v = groupAverage(p, g);
                      return (
                        <td key={k} className={`px-2.5 text-center tabular-nums ${tone(v)}`}>
                          {v ?? "–"}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <aside className="flex flex-col gap-4">
          <section className="flex flex-col gap-3 rounded-[14px] border border-border bg-panel-2 p-5">
            <h2 className="text-[17px] font-bold">Suggested XI · 4-4-2</h2>
            <p className="text-[13px] text-muted">
              Picked the way the engine picks: best per role, left-footers on the left.
            </p>
            <ul className="flex flex-col gap-2">
              {XI_ROWS.map((r) => (
                <li key={r.position} className="flex items-center gap-2.5 text-sm">
                  <PositionBadge position={r.position} />
                  <span>
                    {r.slots
                      .map((sl) => bySlot.get(sl))
                      .filter(Boolean)
                      .map((s) => `${s!.p.shirt_number} ${s!.p.name}`)
                      .join(" · ")}
                  </span>
                </li>
              ))}
            </ul>
          </section>
          <section className="flex flex-col gap-2 rounded-[14px] border border-border bg-panel-2 p-5">
            <h2 className="text-[15px] font-bold">Columns</h2>
            <p className="text-xs leading-[1.45] text-muted">
              MEN mental · ATH athletic · DEF defense · ATT attack · PLY playmaking · SET set pieces
              · GK goalkeeping. Foot: strong foot · weak foot in stars (1–5).
            </p>
            <p className="flex gap-3 text-xs font-semibold">
              <span className="text-goal">80+</span>
              <span className="text-text">65+</span>
              <span className="text-muted">50+</span>
              <span className="text-foul">&lt;50</span>
            </p>
          </section>
          <Button disabled={busy} onClick={() => act(startPlayingAction)}>
            Start playing →
          </Button>
          <Button
            variant="secondary"
            disabled={busy || team.regenerationsLeft <= 0}
            onClick={() => act(regenerateSquadAction)}
          >
            ↻ Regenerate squad ({team.regenerationsLeft} left)
          </Button>
          <p className="text-[13px] text-muted">
            You can regenerate twice before your first match.
          </p>
        </aside>
      </div>

      {open && (
        <PlayerDrawer
          key={open.id}
          row={open}
          kits={team.kits}
          captain={team.captainId === open.id}
          busy={busy}
          onClose={() => setOpenId(null)}
          onAction={(fn) => act(() => fn(open.id))}
        />
      )}
    </div>
  );
}
