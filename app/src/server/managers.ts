import "server-only";

import { and, count, desc, eq, exists, inArray, sql } from "drizzle-orm";
import { cache } from "react";

import { formationOf, type MatchFact } from "@/features/profile/achievements";

import { db, schema } from "./db";
import type { TeamRow } from "./teams";

const { profiles, matches, donations, teams } = schema;

/**
 * Managers as other managers see them (Figma "Profile – …"): the public profile, the match record from saved
 * matches, the people they invited and whether they support the game. Every number comes from stored rows.
 */

export type PublicManager = {
  userId: string;
  username: string;
  displayName: string | null;
  nationality: string | null;
  bio: string | null;
  favouriteClub: string | null;
  showProfile: boolean;
  showHistory: boolean;
  isAdmin: boolean;
  since: Date;
};

const managerColumns = {
  userId: profiles.userId,
  username: profiles.username,
  displayName: profiles.displayName,
  nationality: profiles.nationality,
  bio: profiles.bio,
  favouriteClub: profiles.favouriteClub,
  showProfile: profiles.showProfile,
  showHistory: profiles.showHistory,
  isAdmin: profiles.isAdmin,
  since: profiles.createdAt,
};

export async function managerByUsername(username: string): Promise<PublicManager | null> {
  const [row] = await db()
    .select(managerColumns)
    .from(profiles)
    .where(sql`lower(${profiles.username}) = lower(${username})`);
  return row?.username ? { ...row, username: row.username } : null;
}

export const managerById = cache(async (userId: string): Promise<PublicManager | null> => {
  const [row] = await db().select(managerColumns).from(profiles).where(eq(profiles.userId, userId));
  return row?.username ? { ...row, username: row.username } : null;
});

export type RecentMatch = {
  id: string;
  homeName: string;
  awayName: string;
  homeScore: number;
  awayScore: number;
  online: boolean;
  playedAt: Date;
  /** W / D / L from the manager's side. */
  result: "W" | "D" | "L";
};

type RecordRow = { rating: number; goals: number };

/**
 * The manager's saved matches as facts from their side. The manager is the home side, unless only the away name is
 * their club's (an online match saved by the friend who hosted).
 */
export async function matchFacts(
  userId: string,
  clubName: string | null,
  limit = 300,
): Promise<{ facts: MatchFact[]; recent: RecentMatch[] }> {
  const rows = await db()
    .select({
      id: matches.id,
      homeName: matches.homeName,
      awayName: matches.awayName,
      homeScore: matches.homeScore,
      awayScore: matches.awayScore,
      mode: matches.mode,
      homePlan: matches.homePlan,
      awayPlan: matches.awayPlan,
      // Only the two records arrays, not the whole full-time header (formations, players, stats: kilobytes a row).
      homeRecords: sql<RecordRow[] | null>`${matches.summary}->'home'->'records'`,
      awayRecords: sql<RecordRow[] | null>`${matches.summary}->'away'->'records'`,
      playedAt: matches.playedAt,
    })
    .from(matches)
    .where(eq(matches.userId, userId))
    .orderBy(desc(matches.playedAt))
    .limit(limit);
  const facts: MatchFact[] = [];
  const recent: RecentMatch[] = [];
  for (const m of rows) {
    const away = !!clubName && m.awayName === clubName && m.homeName !== clubName;
    const gf = away ? m.awayScore : m.homeScore;
    const ga = away ? m.homeScore : m.awayScore;
    const records = (away ? m.awayRecords : m.homeRecords) ?? [];
    facts.push({
      goalsFor: gf,
      goalsAgainst: ga,
      online: m.mode === "online",
      formation: formationOf(away ? m.awayPlan : m.homePlan),
      players: records.map((r) => ({ rating: Number(r.rating) || 0, goals: Number(r.goals) || 0 })),
      playedAt: m.playedAt,
    });
    recent.push({
      id: m.id,
      homeName: m.homeName,
      awayName: m.awayName,
      homeScore: m.homeScore,
      awayScore: m.awayScore,
      online: m.mode === "online",
      playedAt: m.playedAt,
      result: gf > ga ? "W" : gf < ga ? "L" : "D",
    });
  }
  return { facts, recent };
}

/** My saved matches against a club (by name), from my side. */
export async function headToHead(myUserId: string, myClub: string | null, theirClub: string) {
  const { recent } = await matchFacts(myUserId, myClub);
  const list = recent.filter((m) => m.homeName === theirClub || m.awayName === theirClub);
  return {
    won: list.filter((m) => m.result === "W").length,
    drawn: list.filter((m) => m.result === "D").length,
    lost: list.filter((m) => m.result === "L").length,
    recent: list.slice(0, 3),
  };
}

export type InvitedManager = {
  username: string;
  displayName: string | null;
  joinedAt: Date;
  played: boolean;
};

/** Managers who named this one as their inviter, and whether each has played a match. */
export async function invitedManagers(userId: string): Promise<InvitedManager[]> {
  const rows = await db()
    .select({
      username: profiles.username,
      displayName: profiles.displayName,
      joinedAt: profiles.createdAt,
      played: exists(
        db()
          .select({ one: sql`1` })
          .from(matches)
          .where(eq(matches.userId, profiles.userId)),
      ),
    })
    .from(profiles)
    .where(eq(profiles.invitedBy, userId))
    .orderBy(desc(profiles.createdAt));
  return rows
    .filter((r): r is typeof r & { username: string } => !!r.username)
    .map((r) => ({ ...r, played: Boolean(r.played) }));
}

/** Supporter badge: at least one donation with the badge switched on. */
export async function isSupporter(userId: string): Promise<boolean> {
  const [row] = await db()
    .select({ n: count() })
    .from(donations)
    .where(and(eq(donations.userId, userId), eq(donations.showBadge, true)));
  return (row?.n ?? 0) > 0;
}

/** The club of a manager, if it is ready. */
export const clubOf = cache(async (userId: string): Promise<TeamRow | null> => {
  const [t] = await db()
    .select()
    .from(teams)
    .where(and(eq(teams.ownerId, userId), eq(teams.status, "ready")));
  return t ?? null;
});

export async function clubById(id: string): Promise<TeamRow | null> {
  const [t] = await db()
    .select()
    .from(teams)
    .where(and(eq(teams.id, id), eq(teams.status, "ready")));
  return t ?? null;
}

/** Usernames of several managers at once (club pages link to their manager). */
export async function usernames(userIds: string[]): Promise<Map<string, string>> {
  if (!userIds.length) return new Map();
  const rows = await db()
    .select({ userId: profiles.userId, username: profiles.username })
    .from(profiles)
    .where(inArray(profiles.userId, userIds));
  return new Map(rows.filter((r) => r.username).map((r) => [r.userId, r.username!]));
}
