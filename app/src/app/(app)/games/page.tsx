import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";

import { PageSkeleton } from "@/components/app-shell/shell-skeleton";
import { ActionLink, Card, Chip, EmptyState, PageHeader } from "@/components/app-shell/ui";
import { clubFixtures, clubResults } from "@/server/home";
import { loadClub } from "@/server/team-flow";

export const metadata: Metadata = { title: "My Games" };

const date = (d: Date) =>
  d.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short" });
const COMP = {
  league: ["League", "muted"],
  cup: ["Cup", "chance"],
  friendly: ["Friendly", "info"],
} as const;

async function Games() {
  const { user, team } = await loadClub("/games");
  const [results, fixtures] = await Promise.all([
    clubResults(user.id, team.id),
    clubFixtures(team.id),
  ]);
  const upcoming = fixtures.filter((f) => f.status === "scheduled" || f.status === "live");
  return (
    <>
      <PageHeader
        crumb="My Games"
        title="My Games"
        sub={`${results.length} played · ${upcoming.length} scheduled`}
        actions={
          <>
            <ActionLink href="/lobby" variant="secondary">
              Play a friend
            </ActionLink>
            <ActionLink href="/play" icon="play">
              Play a friendly
            </ActionLink>
          </>
        }
      />
      <Card title="Upcoming" icon="calendar">
        {upcoming.length === 0 ? (
          <EmptyState icon="calendar" title="No fixtures scheduled">
            League and cup fixtures are coming soon. Friendlies against the computer or a friend can
            be played any time.
          </EmptyState>
        ) : (
          <ul className="divide-y divide-divider">
            {upcoming.map((f) => {
              const [label, tone] = COMP[f.competition];
              return (
                <li key={f.id} className="flex flex-wrap items-center gap-3 py-3 text-[13px]">
                  <span className="w-28 font-semibold text-text">{date(f.kickoffAt)}</span>
                  <span className="w-14 text-muted">
                    {f.kickoffAt.toLocaleTimeString("en-GB", {
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </span>
                  <span className="flex-1 text-text">
                    {f.homeName} <span className="text-muted">vs</span> {f.awayName}
                  </span>
                  <Chip tone={tone}>{f.round ? `${label} · R${f.round}` : label}</Chip>
                  {f.status === "live" && (
                    <Chip tone="foul" solid>
                      LIVE
                    </Chip>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </Card>
      <Card title="Results" icon="trophy">
        {results.length === 0 ? (
          <EmptyState
            icon="ball"
            tone="accent"
            title="No matches yet"
            action={
              <ActionLink href="/play" icon="play">
                Play your first friendly
              </ActionLink>
            }
          >
            Every match you play with your club is saved here with its replay.
          </EmptyState>
        ) : (
          <ul className="divide-y divide-divider" data-testid="match-list">
            {results.map((m) => (
              <li key={m.id}>
                <Link
                  href={`/matches/${m.id}`}
                  className="flex flex-wrap items-center gap-3 rounded-lg px-1 py-3 text-[13px] hover:bg-panel"
                >
                  <span className="w-28 font-semibold text-text">{date(m.playedAt)}</span>
                  <span className="flex-1 text-right text-text">{m.homeName}</span>
                  <span className="w-16 text-center text-base font-extrabold text-text">
                    {m.homeScore} – {m.awayScore}
                  </span>
                  <span className="flex-1 text-text">{m.awayName}</span>
                  <Chip tone={m.result === "W" ? "goal" : m.result === "L" ? "foul" : "muted"}>
                    {{ W: "Win", D: "Draw", L: "Loss" }[m.result]}
                  </Chip>
                  <span className="text-xs font-semibold text-link">Replay</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </>
  );
}

export default function GamesPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Games />
    </Suspense>
  );
}
