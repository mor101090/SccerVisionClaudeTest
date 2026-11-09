import "server-only";

import { and, asc, count, desc, eq, inArray } from "drizzle-orm";

import { db, schema } from "./db";

const { leagues, leagueMembers, standings, teams, fixtures, leagueFollows } = schema;

/**
 * Leagues for the Leagues pages (Figma "Leagues – browse" / "League – another league"). A league without standings
 * rows has not started; nothing here is invented.
 */

export type LeagueCard = {
  id: string;
  name: string;
  season: number;
  clubs: number;
  leader: string | null;
  mine: boolean;
  following: boolean;
};

export async function listLeagues(userId: string, myTeamId: string | null): Promise<LeagueCard[]> {
  const rows = await db()
    .select({
      id: leagues.id,
      name: leagues.name,
      season: leagues.season,
      clubs: count(leagueMembers.teamId),
    })
    .from(leagues)
    .leftJoin(leagueMembers, eq(leagueMembers.leagueId, leagues.id))
    .groupBy(leagues.id)
    .orderBy(asc(leagues.name))
    .limit(200);
  if (!rows.length) return [];
  const ids = rows.map((r) => r.id);
  const [leaders, follows, mine] = await Promise.all([
    db()
      .select({ leagueId: standings.leagueId, name: teams.name })
      .from(standings)
      .innerJoin(teams, eq(teams.id, standings.teamId))
      .where(and(inArray(standings.leagueId, ids), eq(standings.position, 1))),
    db()
      .select({ leagueId: leagueFollows.leagueId })
      .from(leagueFollows)
      .where(eq(leagueFollows.userId, userId)),
    myTeamId
      ? db()
          .select({ leagueId: leagueMembers.leagueId })
          .from(leagueMembers)
          .where(eq(leagueMembers.teamId, myTeamId))
      : Promise.resolve([] as { leagueId: string }[]),
  ]);
  const leader = new Map(leaders.map((l) => [l.leagueId, l.name]));
  const followed = new Set(follows.map((f) => f.leagueId));
  const my = new Set(mine.map((m) => m.leagueId));
  return rows.map((r) => ({
    ...r,
    leader: leader.get(r.id) ?? null,
    mine: my.has(r.id),
    following: followed.has(r.id),
  }));
}

export async function leagueDetail(id: string, userId: string) {
  const [league] = await db()
    .select({ id: leagues.id, name: leagues.name, season: leagues.season })
    .from(leagues)
    .where(eq(leagues.id, id));
  if (!league) return null;
  const [table, upcoming, results, follow] = await Promise.all([
    db()
      .select({
        teamId: standings.teamId,
        name: teams.name,
        shortName: teams.shortName,
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
      .where(eq(standings.leagueId, id))
      .orderBy(asc(standings.position)),
    db()
      .select({
        id: fixtures.id,
        homeName: fixtures.homeName,
        awayName: fixtures.awayName,
        kickoffAt: fixtures.kickoffAt,
        round: fixtures.round,
      })
      .from(fixtures)
      .where(and(eq(fixtures.leagueId, id), eq(fixtures.status, "scheduled")))
      .orderBy(asc(fixtures.kickoffAt))
      .limit(6),
    db()
      .select({
        id: fixtures.id,
        homeName: fixtures.homeName,
        awayName: fixtures.awayName,
        homeScore: fixtures.homeScore,
        awayScore: fixtures.awayScore,
        kickoffAt: fixtures.kickoffAt,
      })
      .from(fixtures)
      .where(and(eq(fixtures.leagueId, id), eq(fixtures.status, "finished")))
      .orderBy(desc(fixtures.kickoffAt))
      .limit(6),
    db()
      .select({ leagueId: leagueFollows.leagueId })
      .from(leagueFollows)
      .where(and(eq(leagueFollows.userId, userId), eq(leagueFollows.leagueId, id))),
  ]);
  return { league, table, upcoming, results, following: follow.length > 0 };
}

export async function setFollow(userId: string, leagueId: string, follow: boolean): Promise<void> {
  if (follow) {
    await db().insert(leagueFollows).values({ userId, leagueId }).onConflictDoNothing();
  } else {
    await db()
      .delete(leagueFollows)
      .where(and(eq(leagueFollows.userId, userId), eq(leagueFollows.leagueId, leagueId)));
  }
}
