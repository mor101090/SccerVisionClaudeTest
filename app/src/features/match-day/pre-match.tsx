"use client";

import { AppIcon } from "@/components/app-shell/app-icon";
import { Card, PageHeader } from "@/components/app-shell/ui";
import type { Replay } from "@/engine/replay/replay";
import { MiniPitch } from "@/features/tactics/mini-pitch";
import { lineLabel, TEMPO, widthLabel } from "@/features/tactics/plan";

import { initials, teamOf, type HeaderTeam } from "./derive";
import { MENTALITIES } from "./mentality";
import type { PlanChanges } from "./plan-changes";
import { btn, PosBadge, Segmented } from "./ui";

/** The eleven in formation slot order (header `slot`), for the mini pitch. */
function bySlot(team: HeaderTeam) {
  const out: { number: number; name: string }[] = [];
  team.players.slice(0, 11).forEach((p, i) => {
    const slot = (p as { slot?: number }).slot ?? i;
    out[slot] = { number: p.number, name: p.shortName };
  });
  return out;
}

export function weatherText(replay: Replay): string {
  const c = replay.conditions;
  if (!c) return "";
  const wind = c.wind >= 0.5 ? ` · wind ${Math.round(c.wind * 3.6)} km/h` : "";
  return `${c.weather}${c.night ? " · night" : ""}${wind}`;
}

function Crest({ team, size = 48 }: { team: HeaderTeam; size?: number }) {
  return (
    <span
      className="flex shrink-0 items-center justify-center rounded-full border-2 border-white/30 font-extrabold"
      style={{
        width: size,
        height: size,
        background: team.kit.primary,
        color: team.kit.number,
        fontSize: size * 0.32,
      }}
      aria-hidden
    >
      {initials(team.name)}
    </span>
  );
}
export { Crest };

export function PreMatch({
  replay,
  strength,
  plan,
  onKickOff,
  onCancel,
}: {
  replay: Replay;
  strength: string;
  plan: PlanChanges;
  onKickOff: () => void;
  onCancel: () => void;
}) {
  const home = teamOf(replay, 0);
  const away = teamOf(replay, 1);
  const t = plan.base?.team;
  const best = [...away.players.slice(0, 11)]
    .sort((a, b) => b.passing + b.speed - (a.passing + a.speed))
    .slice(0, 3);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        crumb="Play"
        title="Pre-match"
        sub="Check both line-ups, set your mentality, then kick off."
      />

      <section className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-border bg-panel-2 px-6 py-5">
        <div className="flex min-w-0 items-center gap-3">
          <Crest team={home} />
          <div className="min-w-0">
            <h2 className="truncate text-xl font-extrabold text-text">{home.name}</h2>
            <p className="text-xs text-muted">You · {home.formation ?? "4-4-2"}</p>
          </div>
        </div>
        <div className="flex flex-col items-center gap-1 text-center">
          <span className="rounded-full bg-accent/25 px-3 py-0.5 text-[11px] font-semibold text-text">
            Friendly
          </span>
          <span className="text-3xl font-extrabold text-muted">v</span>
          <span className="text-xs text-muted">{weatherText(replay)}</span>
        </div>
        <div className="flex min-w-0 items-center gap-3 text-right">
          <div className="min-w-0">
            <h2 className="truncate text-xl font-extrabold text-text">{away.name}</h2>
            <p className="text-xs text-muted">
              Computer · {strength} · {away.formation ?? "4-4-2"}
            </p>
          </div>
          <Crest team={away} />
        </div>
      </section>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card
          title="Your line-up"
          icon="users"
          link={{ href: "/tactics", label: "Edit in Tactics" }}
        >
          <MiniPitch
            formation={home.formation ?? "4-4-2"}
            players={bySlot(home)}
            names
            className="aspect-[68/105] w-full"
          />
          {home.bench?.length ? (
            <div className="flex flex-col gap-2">
              <h3 className="text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
                Bench
              </h3>
              <ul className="flex flex-wrap gap-1.5">
                {home.bench.map((b) => (
                  <li
                    key={b.number}
                    className="rounded-md bg-panel px-2 py-1 text-[11px] text-text"
                  >
                    {b.number} {b.shortName}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </Card>

        <Card title="Opponent report" icon="search">
          <dl className="grid grid-cols-[110px_1fr] gap-x-3 gap-y-2.5 text-[13px]">
            <dt className="text-muted">Strength</dt>
            <dd className="font-semibold text-text">{strength}</dd>
            <dt className="text-muted">Shape</dt>
            <dd className="font-semibold text-text">{away.formation ?? "—"}</dd>
            {away.manager ? (
              <>
                <dt className="text-muted">Manager</dt>
                <dd className="font-semibold text-text">{away.manager}</dd>
              </>
            ) : null}
            <dt className="text-muted">Plays</dt>
            <dd className="text-text">
              {away.tactics
                ? `${lineLabel(away.tactics.line)} line, ${widthLabel(away.tactics.width).toLowerCase()}, ${TEMPO[Math.max(0, Math.min(2, away.tactics.tempo))]!.toLowerCase()} tempo`
                : "—"}
            </dd>
          </dl>
          <div className="flex flex-col gap-2">
            <h3 className="text-[11px] font-bold tracking-[0.06em] text-muted uppercase">
              Players to watch
            </h3>
            <ul className="flex flex-col gap-1.5">
              {best.map((p) => (
                <li key={p.number} className="flex items-center gap-2 text-[13px] text-text">
                  <span className="w-6 text-muted tabular-nums">{p.number}</span>
                  <span className="flex-1 truncate">{p.name}</span>
                  <PosBadge role={p.role} />
                </li>
              ))}
            </ul>
            <p className="text-xs text-muted">Picked by passing and pace.</p>
          </div>
        </Card>

        <Card title="Your plan" icon="flag">
          {plan.changes && (
            <div className="flex flex-col gap-2">
              <span className="text-[13px] text-muted">Mentality</span>
              <Segmented
                label="Mentality"
                options={MENTALITIES}
                value={plan.changes.mentality}
                onChange={(mentality) => plan.apply({ mentality })}
              />
            </div>
          )}
          {t && (
            <dl className="grid grid-cols-[110px_1fr] gap-x-3 gap-y-2.5 text-[13px]">
              <dt className="text-muted">Formation</dt>
              <dd className="font-semibold text-text">{plan.base?.formation}</dd>
              <dt className="text-muted">Defensive line</dt>
              <dd className="font-semibold text-text">{lineLabel(t.line)}</dd>
              <dt className="text-muted">Width</dt>
              <dd className="font-semibold text-text">{widthLabel(t.width)}</dd>
              <dt className="text-muted">Tempo</dt>
              <dd className="font-semibold text-text">{TEMPO[t.tempo]}</dd>
            </dl>
          )}
          <p className="flex items-start gap-2 rounded-lg bg-info/16 p-3 text-[13px] leading-5 text-text">
            <AppIcon name="info" size={16} className="mt-0.5 text-info" />
            During the match: mentality, touchline instructions, substitutions and tactics, applied
            at the next stoppage.
          </p>
          <div className="mt-auto flex flex-col gap-2">
            <button type="button" className={btn("primary")} onClick={onKickOff} data-autofocus>
              Kick off
            </button>
            <button type="button" className={btn("ghost", "sm")} onClick={onCancel}>
              Back to match setup
            </button>
          </div>
        </Card>
      </div>
    </div>
  );
}
