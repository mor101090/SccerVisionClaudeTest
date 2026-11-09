import Link from "next/link";

import { AppIcon } from "@/components/app-shell/app-icon";
import {
  ActionLink,
  Bar,
  Card,
  Chip,
  EmptyState,
  FormDots,
  Gauge,
  OpensLater,
} from "@/components/app-shell/ui";
import { assignToFormation } from "@/features/tactics/board";
import { MiniPitch } from "@/features/tactics/mini-pitch";
import { Crest } from "@/features/team/ui/pieces";
import type { HomeData } from "@/server/home";

import { Countdown, Greeting, Tour } from "./live";
import {
  euros,
  initials,
  MOOD_LABELS,
  MOOD_LINES,
  type Mood,
  pointsShare,
  subtitle,
} from "./rules";

const COMPETITION = { league: "League", cup: "Cup", friendly: "Friendly" } as const;

function seasonLabel(d: HomeData): string | null {
  if (!d.season) return null;
  if (d.season.status === "break")
    return `Season ${d.season.number} is over — the next one starts soon.`;
  if (d.season.status === "preseason")
    return `Season ${d.season.number} starts ${d.season.startsAt.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short" })}.`;
  return `Season ${d.season.number}`;
}

function NextMatch({ d }: { d: HomeData }) {
  const f = d.nextFixture;
  const kit = d.club.kits?.home;
  if (!f) {
    return (
      <Card title="Next match" icon="ball" className="flex-1" id="tour-next-match">
        <div className="flex flex-1 flex-col gap-6 md:flex-row md:items-center">
          <div className="flex flex-1 flex-col gap-2.5">
            <h3 className="text-[22px] font-extrabold text-text">No official fixture yet</h3>
            <p className="text-[13px] leading-5 text-muted">
              Leagues are coming soon. For now, play friendlies against the computer or a friend —
              the results count for your form, stats and fan mood.
            </p>
            <div className="flex flex-wrap gap-2.5 pt-1">
              <ActionLink href="/play" icon="play">
                Play the computer
              </ActionLink>
              <ActionLink href="/lobby" variant="secondary">
                Challenge a friend
              </ActionLink>
            </div>
          </div>
          {d.lastMatch && (
            <Link
              href={`/matches/${d.lastMatch.id}`}
              className="flex w-full flex-col gap-2 rounded-xl bg-panel p-4 hover:bg-border md:w-[300px]"
            >
              <span className="text-[11px] font-bold tracking-[0.06em] text-muted">LAST MATCH</span>
              <span className="flex items-center gap-2.5 text-[15px] font-bold text-text">
                <span className="truncate">{d.lastMatch.homeName}</span>
                <span className="text-2xl font-extrabold">
                  {d.lastMatch.homeScore} – {d.lastMatch.awayScore}
                </span>
                <span className="truncate">{d.lastMatch.awayName}</span>
              </span>
              <span>
                <Chip
                  tone={
                    d.lastMatch.result === "W"
                      ? "goal"
                      : d.lastMatch.result === "L"
                        ? "foul"
                        : "muted"
                  }
                >
                  {{ W: "Win", D: "Draw", L: "Loss" }[d.lastMatch.result]}
                </Chip>
              </span>
              <span className="inline-flex items-center gap-1 text-[13px] font-semibold text-link">
                Watch replay <AppIcon name="chevronRight" size={14} />
              </span>
            </Link>
          )}
        </div>
      </Card>
    );
  }
  const live = f.status === "live";
  const team = (name: string, side: "HOME" | "AWAY", ours: boolean) => (
    <div className="flex w-[120px] flex-col items-center gap-2 text-center md:w-[180px]">
      {ours ? (
        <Crest kit={kit} initials={initials(d.club.name)} size={60} />
      ) : (
        <span className="flex size-[60px] items-center justify-center rounded-full bg-info text-xl font-extrabold text-white">
          {initials(name)}
        </span>
      )}
      <span className="text-[15px] font-bold text-text">{name}</span>
      <span className="text-[11px] font-bold tracking-[0.1em] text-muted">{side}</span>
    </div>
  );
  return (
    <Card
      title={live ? "Live now" : "Next match"}
      icon="ball"
      className={`flex-1 ${live ? "border-foul" : ""}`}
      id="tour-next-match"
      link={{ href: live ? "/play" : "/games", label: live ? "Watch live" : "Match preview" }}
      action={
        live ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-foul px-2 py-0.5 text-[11px] font-bold text-white">
            <span className="size-2 animate-sv-pulse rounded-full bg-white" /> LIVE
          </span>
        ) : null
      }
    >
      <div className="flex items-center justify-between gap-2">
        {team(f.homeName, "HOME", f.atHome)}
        <div className="flex flex-1 flex-col items-center gap-2">
          <Chip
            tone={
              f.competition === "cup" ? "chance" : f.competition === "friendly" ? "info" : "muted"
            }
            icon="trophy"
          >
            {COMPETITION[f.competition]}
            {f.round ? ` · Round ${f.round}` : ""}
          </Chip>
          {live ? (
            <span className="text-5xl font-extrabold text-text tabular-nums">
              {f.homeScore ?? 0} – {f.awayScore ?? 0}
            </span>
          ) : (
            <Countdown kickoff={f.kickoffAt.toISOString()} />
          )}
          <span className="text-[13px] text-muted">
            {f.kickoffAt.toLocaleString("en-GB", {
              weekday: "short",
              day: "numeric",
              month: "short",
              hour: "2-digit",
              minute: "2-digit",
            })}
          </span>
        </div>
        {team(f.awayName, "AWAY", !f.atHome)}
      </div>
      <div className="flex flex-wrap items-center gap-2.5 border-t border-divider pt-4 text-xs text-muted">
        Your form <FormDots form={d.form} />
        <span className="flex-1" />
        <ActionLink href="/tactics" variant="secondary">
          Set lineup
        </ActionLink>
      </div>
    </Card>
  );
}

function Rank({ d }: { d: HomeData }) {
  const r = d.rating;
  const delta = r?.previousRank ? r.previousRank - r.worldRank : 0;
  return (
    <Card
      title="Team rank"
      icon="trophy"
      className="lg:w-[330px]"
      link={r ? { href: "/rankings", label: "Rankings" } : undefined}
    >
      {r ? (
        <>
          <div className="flex items-center gap-2.5">
            <span className="text-[40px] font-extrabold text-text">
              #{r.worldRank.toLocaleString("en-US")}
            </span>
            {delta !== 0 && (
              <Chip tone={delta > 0 ? "goal" : "foul"} icon={delta > 0 ? "arrowUp" : "arrowDown"}>
                {Math.abs(delta)} this week
              </Chip>
            )}
          </div>
          <p className="text-[13px] text-muted">Rating {r.rating.toLocaleString("en-US")}</p>
        </>
      ) : (
        <>
          <span className="text-[40px] font-extrabold text-text">Unranked</span>
          <p className="text-[13px] text-muted">
            {d.matchesToRating > 0
              ? `Play ${d.matchesToRating} more match${d.matchesToRating === 1 ? "" : "es"} to get a rating.`
              : "World ratings are coming soon. Your matches already count."}
          </p>
          <Bar value={(3 - d.matchesToRating) / 3} />
        </>
      )}
    </Card>
  );
}

function Stats({ d }: { d: HomeData }) {
  const wdl = (s: HomeData["last5"]) => (s.games ? `${s.won}–${s.drawn}–${s.lost}` : "—");
  const pct = (v: number | null) => (v === null ? "—" : `${Math.round(v * 100)}%`);
  const mood: Mood = d.fans.mood;
  const moodTone = (["foul", "chance", "card", "goal", "accent"] as const)[mood - 1];
  return (
    <div id="tour-stats" className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
      <Card title="Results" link={{ href: "/games", label: "Fixtures" }} delay={2}>
        <div className="flex justify-around">
          <Gauge
            value={d.last5.games ? pointsShare(d.last5) : null}
            label={wdl(d.last5)}
            caption="Last 5 games"
            tone="goal"
          />
          <Gauge
            value={d.season5.games ? pointsShare(d.season5) : null}
            label={wdl(d.season5)}
            caption="All matches"
            tone="goal"
          />
        </div>
      </Card>
      <Card title="Goals per game" link={{ href: "/games", label: "Stats" }} delay={3}>
        <div className="flex justify-around">
          <Gauge
            value={d.last5.goalsPerGame === null ? null : Math.min(1, d.last5.goalsPerGame / 4)}
            label={d.last5.goalsPerGame?.toFixed(1) ?? "—"}
            caption="Last 5 games"
          />
          <Gauge
            value={d.season5.goalsPerGame === null ? null : Math.min(1, d.season5.goalsPerGame / 4)}
            label={d.season5.goalsPerGame?.toFixed(1) ?? "—"}
            caption="All matches"
          />
        </div>
      </Card>
      <Card title="Possession" link={{ href: "/games", label: "Stats" }} delay={4}>
        <div className="flex justify-around">
          <Gauge
            value={d.last5.possession}
            label={pct(d.last5.possession)}
            caption="Last 5 games"
            tone="info"
          />
          <Gauge
            value={d.season5.possession}
            label={pct(d.season5.possession)}
            caption="All matches"
            tone="info"
          />
        </div>
      </Card>
      <Card title="Fan mood" icon="smile" delay={5}>
        <div className="flex gap-2" aria-hidden>
          {(["foul", "chance", "card", "goal", "accent"] as const).map((tone, i) => (
            <span key={tone} className="flex flex-1 flex-col items-center gap-1">
              <AppIcon
                name="smile"
                size={22}
                className={i + 1 === mood ? `text-${tone}` : "text-border"}
              />
              <span
                className={`h-1 w-full rounded-full ${i < mood ? `bg-${tone}` : "bg-border"}`}
              />
            </span>
          ))}
        </div>
        <p className="text-sm">
          <span className={`text-lg font-extrabold text-${moodTone}`}>{MOOD_LABELS[mood]}</span>{" "}
          <span className="text-muted">— {MOOD_LINES[mood]}</span>
        </p>
        <p className="text-xs text-muted">
          {d.fans.fromResults
            ? d.matchesPlayed
              ? "Based on your last 5 matches"
              : "Fans are waiting for your first match"
            : `${d.fans.members?.toLocaleString("en-US")} members`}
        </p>
      </Card>
    </div>
  );
}

function TacticsCard({ d }: { d: HomeData }) {
  const map = assignToFormation(d.tactics.formation);
  const bySlot = new Map(d.tactics.starters.map((s) => [s.slot, s]));
  const players = map.map((j) => {
    const s = bySlot.get(j);
    return s ? { number: s.number } : null;
  });
  return (
    <Card
      title="Tactics"
      icon="tactics"
      link={{ href: "/tactics", label: "Edit" }}
      className="lg:w-[330px]"
      id="tour-tactics"
      delay={6}
    >
      <p className="text-base font-extrabold text-text">{d.tactics.formation}</p>
      <MiniPitch formation={d.tactics.formation} players={players} className="aspect-[68/80]" />
      <p className="text-xs text-muted">
        {d.tactics.captain ? `Captain: ${d.tactics.captain}` : "No captain picked yet"}
      </p>
    </Card>
  );
}

function League({ d }: { d: HomeData }) {
  if (!d.league) {
    const xi = d.tactics.starters.length === 11;
    return (
      <Card title="League table" icon="chart" className="flex-1" id="tour-league" delay={7}>
        <EmptyState icon="trophy" title="Leagues are coming soon">
          There is no league table yet. Friendlies against the computer or a friend are open now.
        </EmptyState>
        <ul className="flex flex-col gap-2 rounded-xl bg-panel p-3.5 text-[13px] text-text">
          <li className="flex items-center gap-2">
            <AppIcon name="check" size={16} className="text-goal" /> Club ready
          </li>
          <li className="flex items-center gap-2">
            <AppIcon
              name={xi ? "check" : "clock"}
              size={16}
              className={xi ? "text-goal" : "text-chance"}
            />
            {xi ? "Lineup saved" : "Pick a starting XI"}
          </li>
          <li className="flex items-center gap-2">
            <AppIcon name="clock" size={16} className="text-chance" /> Leagues: coming soon
          </li>
        </ul>
      </Card>
    );
  }
  return (
    <Card
      title={d.league.name}
      icon="chart"
      className="flex-1"
      id="tour-league"
      link={{ href: "/league", label: "Full table" }}
      delay={7}
    >
      <table className="w-full text-[13px]">
        <thead className="text-[11px] font-bold text-muted">
          <tr>
            <th className="w-7 py-1.5 text-left">#</th>
            <th className="text-left">Club</th>
            {["P", "W", "D", "L", "Pts"].map((h) => (
              <th key={h} className="w-9 text-right">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {d.league.rows.map((r) => (
            <tr key={r.teamId} className={r.teamId === d.club.id ? "bg-link/16" : ""}>
              <td className="py-2 pl-1 text-muted">{r.position}</td>
              <td className="font-semibold text-text">{r.name}</td>
              <td className="text-right text-muted">{r.played}</td>
              <td className="text-right text-muted">{r.won}</td>
              <td className="text-right text-muted">{r.drawn}</td>
              <td className="text-right text-muted">{r.lost}</td>
              <td className="pr-1 text-right font-bold text-text">{r.points}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
}

function ClubColumn({ d }: { d: HomeData }) {
  const f = d.finances;
  const max = f ? Math.max(1, ...f.weeks.map((w) => Math.abs(w.net))) : 1;
  return (
    <div className="flex flex-col gap-5 lg:w-[330px]">
      <Card
        title="Budget"
        icon="wallet"
        link={f ? { href: "/finances", label: "Finances" } : undefined}
        delay={8}
      >
        {f ? (
          <>
            <span className="text-[28px] font-extrabold text-text">{euros(f.balance)}</span>
            <div className="flex h-11 items-end gap-1.5" aria-hidden>
              {f.weeks.map((w, i) => (
                <span
                  key={i}
                  className={`flex-1 rounded-[3px] ${w.net >= 0 ? "bg-link" : "bg-foul"}`}
                  style={{ height: `${Math.max(8, (Math.abs(w.net) / max) * 100)}%` }}
                />
              ))}
            </div>
            <p className="text-xs text-muted">Net result per week, last 8 weeks</p>
          </>
        ) : (
          <>
            <p className="text-lg font-extrabold text-text">Coming soon</p>
            <p className="text-xs leading-[18px] text-muted">
              Club finances aren&apos;t part of the game yet. Friendlies cost nothing.
            </p>
          </>
        )}
      </Card>
      <Card
        title={d.stadium?.name ?? "Home ground"}
        icon="stadium"
        className="flex-1"
        link={d.stadium ? { href: "/stadium", label: "Manage" } : undefined}
        delay={9}
      >
        {d.stadium ? (
          <dl className="grid grid-cols-[auto_1fr] gap-x-2 gap-y-1.5 text-[13px]">
            <dt className="text-muted">Capacity:</dt>
            <dd className="font-semibold text-text">
              {d.stadium.capacity.toLocaleString("en-US")}
            </dd>
            <dt className="text-muted">Roof:</dt>
            <dd className="font-semibold text-text">{d.stadium.roof ? "Yes" : "None"}</dd>
            <dt className="text-muted">Season tickets:</dt>
            <dd className="font-semibold text-text">
              {d.stadium.seasonTickets.toLocaleString("en-US")}
            </dd>
          </dl>
        ) : (
          <OpensLater>Stadium management is coming soon.</OpensLater>
        )}
      </Card>
    </div>
  );
}

/** The signed-in homepage (Figma: Home – default / Home – no league yet). */
export function Dashboard({ d }: { d: HomeData }) {
  const sub = subtitle({
    liveFixture: d.nextFixture?.status === "live",
    hoursToNext: d.hoursToNext,
    lineupConfirmed: d.tactics.starters.length === 11,
    pendingOffers: d.counts.offers,
    seasonLabel: seasonLabel(d),
    matchesPlayed: d.matchesPlayed,
  });
  return (
    <>
      <div className="flex animate-sv-enter flex-wrap items-center gap-4">
        <div className="flex min-w-0 flex-1 basis-72 flex-col gap-1">
          <Greeting name={d.manager.username ?? d.manager.name} />
          <p className="text-sm text-muted">{sub}</p>
        </div>
        <Chip icon="calendar">{d.season ? `Season ${d.season.number}` : "Friendlies"}</Chip>
        {d.nextFixture ? (
          <ActionLink href="/tactics">Set lineup</ActionLink>
        ) : (
          <ActionLink href="/play" icon="play">
            Play a friendly
          </ActionLink>
        )}
      </div>
      <div className="flex flex-col gap-5 lg:flex-row">
        <NextMatch d={d} />
        <Rank d={d} />
      </div>
      <Stats d={d} />
      <div className="flex flex-col gap-5 lg:flex-row">
        <TacticsCard d={d} />
        <League d={d} />
        <ClubColumn d={d} />
      </div>
      {!d.onboardingSeen && <Tour />}
    </>
  );
}
