import "server-only";

import { and, asc, count, desc, eq, gte, inArray, isNull, or } from "drizzle-orm";
import { cacheLife } from "next/cache";

import {
  type ClubResult,
  moodFromResults,
  type Mood,
  RATED_AFTER,
  type Result,
  resultOf,
  summarize,
  type Summary,
} from "@/features/home/rules";
import type { PlayerOrdersView } from "@/features/tactics/plan";
import { parsePlan } from "@/features/tactics/plan";
import { shortName } from "@/features/team/names";

import type { User } from "./auth";
import { db, schema } from "./db";
import type { Crest, TeamKits } from "./db/schema";
import { getProfile } from "./profiles";
import { getSquad, getTeam, type SquadPlayer, type TeamRow } from "./teams";

const {
  matches,
  fixtures,
  standings,
  leagues,
  teams,
  teamRatings,
  clubFinances,
  financeEntries,
  clubFans,
  stadiums,
  transferOffers,
  notifications,
  messages,
  seasons,
} = schema;

export type FixtureView = {
  id: string;
  competition: "league" | "cup" | "friendly";
  round: number | null;
  homeName: string;
  awayName: string;
  /** True when the manager's club is the home side. */
  atHome: boolean;
  kickoffAt: Date;
  status: "scheduled" | "live" | "finished" | "postponed";
  homeScore: number | null;
  awayScore: number | null;
};

export type MatchView = {
  id: string;
  homeName: string;
  awayName: string;
  homeScore: number;
  awayScore: number;
  playedAt: Date;
  result: Result;
};

export type StandingRow = {
  teamId: string;
  name: string;
  position: number;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  points: number;
  form: string;
};

export type HomeData = {
  manager: { name: string; username: string | null };
  club: {
    id: string;
    name: string;
    shortName: string;
    crest: Crest | null;
    kits: TeamKits | null;
    formation: string;
    /** False while Create your team is unfinished. */
    ready: boolean;
  };
  season: {
    number: number;
    status: "preseason" | "active" | "break" | "finished";
    startsAt: Date;
    endsAt: Date;
  } | null;
  nextFixture: FixtureView | null;
  /** Hours until the next fixture kicks off (negative once it started), null without one. */
  hoursToNext: number | null;
  lastMatch: MatchView | null;
  /** Club matches played (friendlies included), newest first, at most 50. */
  matchesPlayed: number;
  last5: Summary;
  season5: Summary;
  form: Result[];
  rating: { rating: number; worldRank: number; previousRank: number | null } | null;
  /** Matches still needed for a first rating (0 once rated). */
  matchesToRating: number;
  league: { name: string; rows: StandingRow[] } | null;
  finances: { balance: number; weeks: { weekOf: Date; net: number }[] } | null;
  fans: { mood: Mood; members: number | null; fromResults: boolean };
  stadium: {
    name: string;
    capacity: number;
    roof: boolean;
    seasonTickets: number;
    ticketPrice: number;
  } | null;
  tactics: {
    formation: string;
    captain: string | null;
    starters: { slot: number; number: number; shortName: string }[];
    orders: PlayerOrdersView[] | null;
  };
  counts: { notifications: number; messages: number; offers: number };
  onboardingSeen: boolean;
};

/** The club's side of a stored match, or null when the club did not play in it (a demo match). */
function clubSide(
  m: { homeTeamId: string | null; awayTeamId: string | null },
  teamId: string,
): "home" | "away" | null {
  if (m.homeTeamId === teamId) return "home";
  if (m.awayTeamId === teamId) return "away";
  return null;
}

function possessionOf(
  summary: Record<string, unknown> | null,
  side: "home" | "away",
): number | null {
  const sec = (team: unknown) => {
    const stats = (team as { stats?: { PossessionSeconds?: unknown } } | undefined)?.stats;
    return typeof stats?.PossessionSeconds === "number" ? stats.PossessionSeconds : null;
  };
  const h = sec(summary?.home);
  const a = sec(summary?.away);
  if (h === null || a === null || h + a <= 0) return null;
  return (side === "home" ? h : a) / (h + a);
}

async function clubMatches(userId: string, teamId: string) {
  const rows = await db()
    .select({
      id: matches.id,
      homeTeamId: matches.homeTeamId,
      awayTeamId: matches.awayTeamId,
      homeName: matches.homeName,
      awayName: matches.awayName,
      homeScore: matches.homeScore,
      awayScore: matches.awayScore,
      summary: matches.summary,
      playedAt: matches.playedAt,
    })
    .from(matches)
    .where(
      and(
        eq(matches.userId, userId),
        or(eq(matches.homeTeamId, teamId), eq(matches.awayTeamId, teamId)),
      ),
    )
    .orderBy(desc(matches.playedAt))
    .limit(50);
  return rows.flatMap((m) => {
    const side = clubSide(m, teamId);
    if (!side) return [];
    const goalsFor = side === "home" ? m.homeScore : m.awayScore;
    const goalsAgainst = side === "home" ? m.awayScore : m.homeScore;
    const r: ClubResult & { view: MatchView } = {
      goalsFor,
      goalsAgainst,
      possession: possessionOf(m.summary, side),
      view: {
        id: m.id,
        homeName: m.homeName,
        awayName: m.awayName,
        homeScore: m.homeScore,
        awayScore: m.awayScore,
        playedAt: m.playedAt,
        result: resultOf({ goalsFor, goalsAgainst }),
      },
    };
    return [r];
  });
}

async function nextFixture(teamId: string): Promise<FixtureView | null> {
  const [f] = await db()
    .select()
    .from(fixtures)
    .where(
      and(
        or(eq(fixtures.homeTeamId, teamId), eq(fixtures.awayTeamId, teamId)),
        inArray(fixtures.status, ["scheduled", "live"]),
      ),
    )
    .orderBy(asc(fixtures.kickoffAt))
    .limit(1);
  if (!f) return null;
  return {
    id: f.id,
    competition: f.competition,
    round: f.round,
    homeName: f.homeName,
    awayName: f.awayName,
    atHome: f.homeTeamId === teamId,
    kickoffAt: f.kickoffAt,
    status: f.status,
    homeScore: f.homeScore,
    awayScore: f.awayScore,
  };
}

async function currentSeason() {
  const [s] = await db()
    .select()
    .from(seasons)
    .where(inArray(seasons.status, ["preseason", "active", "break"]))
    .orderBy(desc(seasons.number))
    .limit(1);
  return s ? { number: s.number, status: s.status, startsAt: s.startsAt, endsAt: s.endsAt } : null;
}

/** The club's league table, rows around it (the top of the table plus its own row). */
export async function leagueTable(teamId: string, limit = 7) {
  "use cache: private";
  cacheLife("minutes");
  const [mine] = await db()
    .select({ leagueId: standings.leagueId })
    .from(standings)
    .where(eq(standings.teamId, teamId))
    .orderBy(desc(standings.updatedAt))
    .limit(1);
  if (!mine) return null;
  const [league] = await db()
    .select({ name: leagues.name })
    .from(leagues)
    .where(eq(leagues.id, mine.leagueId));
  const rows = await db()
    .select({
      teamId: standings.teamId,
      name: teams.name,
      position: standings.position,
      played: standings.played,
      won: standings.won,
      drawn: standings.drawn,
      lost: standings.lost,
      goalsFor: standings.goalsFor,
      goalsAgainst: standings.goalsAgainst,
      points: standings.points,
      form: standings.form,
    })
    .from(standings)
    .innerJoin(teams, eq(teams.id, standings.teamId))
    .where(eq(standings.leagueId, mine.leagueId))
    .orderBy(asc(standings.position));
  const top = rows.slice(0, limit);
  const own = rows.find((r) => r.teamId === teamId);
  if (own && !top.includes(own)) top[top.length - 1] = own;
  return { name: league?.name ?? "League", rows: limit ? top : rows, all: rows };
}

async function finances(teamId: string) {
  const [f] = await db().select().from(clubFinances).where(eq(clubFinances.teamId, teamId));
  if (!f) return null;
  const since = new Date(Date.now() - 8 * 7 * 86_400_000);
  const entries = await db()
    .select({ weekOf: financeEntries.weekOf, amount: financeEntries.amount })
    .from(financeEntries)
    .where(and(eq(financeEntries.teamId, teamId), gte(financeEntries.weekOf, since)))
    .orderBy(asc(financeEntries.weekOf));
  const byWeek = new Map<number, number>();
  for (const e of entries)
    byWeek.set(e.weekOf.getTime(), (byWeek.get(e.weekOf.getTime()) ?? 0) + e.amount);
  return {
    balance: f.balance,
    weeks: [...byWeek].map(([t, net]) => ({ weekOf: new Date(t), net })),
  };
}

async function counts(userId: string, teamId: string) {
  const [[n], [m], [o]] = await Promise.all([
    db()
      .select({ n: count() })
      .from(notifications)
      .where(and(eq(notifications.userId, userId), isNull(notifications.readAt))),
    db()
      .select({ n: count() })
      .from(messages)
      .where(and(eq(messages.toUserId, userId), isNull(messages.readAt))),
    db()
      .select({ n: count() })
      .from(transferOffers)
      .where(and(eq(transferOffers.sellerTeamId, teamId), eq(transferOffers.status, "pending"))),
  ]);
  return { notifications: n?.n ?? 0, messages: m?.n ?? 0, offers: o?.n ?? 0 };
}

function tacticsOf(team: TeamRow, squad: SquadPlayer[]): HomeData["tactics"] {
  const starters = squad
    .filter((s) => s.starterSlot !== null)
    .sort((a, b) => a.starterSlot! - b.starterSlot!)
    .map((s) => ({
      slot: s.starterSlot!,
      number: s.p.shirt_number,
      shortName: shortName(s.p.name),
    }));
  const cap = squad.find((s) => s.id === team.captainId);
  const captain = cap ? shortName(cap.p.name) : null;
  const plan = parsePlan(team.plan ?? "", team.formation);
  return {
    formation: plan.formation,
    captain,
    starters,
    orders: plan.orders,
  };
}

/**
 * Everything the homepage shows for a signed-in manager with a club, or null without one. Each card reads its own
 * table; an empty table is the new-club state, never an error.
 */
export async function getHomeData(user: User): Promise<HomeData | null> {
  "use cache: private";
  cacheLife("minutes");
  const [team, profile] = await Promise.all([getTeam(user.id), getProfile(user.id)]);
  if (!team) return null;
  const [squad, played, fixture, season, table, money, fanRow, ground, rating, badgeCounts] =
    await Promise.all([
      getSquad(team.id),
      clubMatches(user.id, team.id),
      nextFixture(team.id),
      currentSeason(),
      leagueTable(team.id),
      finances(team.id),
      db()
        .select()
        .from(clubFans)
        .where(eq(clubFans.teamId, team.id))
        .then((r) => r[0] ?? null),
      db()
        .select()
        .from(stadiums)
        .where(eq(stadiums.teamId, team.id))
        .then((r) => r[0] ?? null),
      db()
        .select()
        .from(teamRatings)
        .where(eq(teamRatings.teamId, team.id))
        .then((r) => r[0] ?? null),
      counts(user.id, team.id),
    ]);
  const last5 = played.slice(0, 5);
  const form = last5.map((m) => m.view.result).reverse();
  return {
    manager: { name: user.name, username: profile?.username ?? null },
    club: {
      id: team.id,
      name: team.name,
      shortName: team.shortName,
      crest: team.crest,
      kits: team.kits,
      formation: team.formation,
      ready: team.status === "ready",
    },
    season,
    nextFixture: fixture,
    hoursToNext: fixture ? (fixture.kickoffAt.getTime() - Date.now()) / 3_600_000 : null,
    lastMatch: played[0]?.view ?? null,
    matchesPlayed: played.length,
    last5: summarize(last5),
    season5: summarize(played),
    form,
    rating: rating
      ? { rating: rating.rating, worldRank: rating.worldRank, previousRank: rating.previousRank }
      : null,
    matchesToRating: rating ? 0 : Math.max(0, RATED_AFTER - played.length),
    league: table ? { name: table.name, rows: table.rows } : null,
    finances: money,
    fans: fanRow
      ? {
          mood: Math.min(5, Math.max(1, fanRow.mood)) as Mood,
          members: fanRow.members,
          fromResults: false,
        }
      : {
          mood: moodFromResults(last5.map((m) => m.view.result)),
          members: null,
          fromResults: true,
        },
    stadium: ground
      ? {
          name: ground.name,
          capacity: ground.capacity,
          roof: ground.roof,
          seasonTickets: ground.seasonTickets,
          ticketPrice: ground.ticketPrice,
        }
      : null,
    tactics: tacticsOf(team, squad),
    counts: badgeCounts,
    onboardingSeen: Boolean(profile?.onboardingSeenAt),
  };
}

/** Club matches for "My Games" (newest first). */
export async function clubResults(userId: string, teamId: string): Promise<MatchView[]> {
  "use cache: private";
  cacheLife("minutes");
  return (await clubMatches(userId, teamId)).map((m) => m.view);
}

/** Upcoming and recent fixtures of the club. */
export async function clubFixtures(teamId: string) {
  "use cache: private";
  cacheLife("minutes");
  return db()
    .select()
    .from(fixtures)
    .where(or(eq(fixtures.homeTeamId, teamId), eq(fixtures.awayTeamId, teamId)))
    .orderBy(asc(fixtures.kickoffAt))
    .limit(60);
}
