import "server-only";

import { and, asc, desc, eq, gte, ilike, isNull, lte, or, sql } from "drizzle-orm";
import { cacheLife } from "next/cache";

import { db, schema } from "./db";

const {
  teamRatings,
  teams,
  clubFinances,
  financeEntries,
  stadiums,
  staff,
  trainingPlans,
  transferOffers,
  players,
  forumThreads,
  forumPosts,
  notifications,
  messages,
  profiles,
} = schema;

/** Clubs around this one in the world ranking (null: the club is not rated yet). */
export async function rankingAround(teamId: string, span = 4) {
  "use cache: private";
  cacheLife("minutes");
  const [me] = await db().select().from(teamRatings).where(eq(teamRatings.teamId, teamId));
  if (!me) return null;
  const rows = await db()
    .select({
      teamId: teamRatings.teamId,
      name: teams.name,
      rating: teamRatings.rating,
      worldRank: teamRatings.worldRank,
      previousRank: teamRatings.previousRank,
    })
    .from(teamRatings)
    .innerJoin(teams, eq(teams.id, teamRatings.teamId))
    .where(
      and(
        gte(teamRatings.worldRank, me.worldRank - span),
        lte(teamRatings.worldRank, me.worldRank + span),
      ),
    )
    .orderBy(asc(teamRatings.worldRank));
  const [{ total } = { total: 0 }] = await db()
    .select({ total: sql<number>`count(*)::int` })
    .from(teamRatings);
  return { me, rows, total };
}

export async function financeOverview(teamId: string) {
  "use cache: private";
  cacheLife("minutes");
  const [f] = await db().select().from(clubFinances).where(eq(clubFinances.teamId, teamId));
  if (!f) return null;
  const since = new Date(Date.now() - 12 * 7 * 86_400_000);
  const entries = await db()
    .select()
    .from(financeEntries)
    .where(and(eq(financeEntries.teamId, teamId), gte(financeEntries.weekOf, since)))
    .orderBy(asc(financeEntries.weekOf));
  return { balance: f.balance, entries };
}

export async function stadiumOf(teamId: string) {
  "use cache: private";
  cacheLife("minutes");
  const [s] = await db().select().from(stadiums).where(eq(stadiums.teamId, teamId));
  return s ?? null;
}

export async function staffOf(teamId: string) {
  "use cache: private";
  cacheLife("minutes");
  return db().select().from(staff).where(eq(staff.teamId, teamId));
}

export async function trainingOf(teamId: string) {
  "use cache: private";
  cacheLife("minutes");
  return db()
    .select()
    .from(trainingPlans)
    .where(eq(trainingPlans.teamId, teamId))
    .orderBy(asc(trainingPlans.weekday));
}

/** Pending offers for the club's players (incoming) and by the club (outgoing). */
export async function offersOf(teamId: string) {
  "use cache: private";
  cacheLife("minutes");
  const select = {
    id: transferOffers.id,
    amount: transferOffers.amount,
    status: transferOffers.status,
    expiresAt: transferOffers.expiresAt,
    player: players.name,
    club: teams.name,
  };
  const [incoming, outgoing] = await Promise.all([
    db()
      .select(select)
      .from(transferOffers)
      .innerJoin(players, eq(players.id, transferOffers.playerId))
      .innerJoin(teams, eq(teams.id, transferOffers.bidderTeamId))
      .where(and(eq(transferOffers.sellerTeamId, teamId), eq(transferOffers.status, "pending")))
      .orderBy(asc(transferOffers.expiresAt)),
    db()
      .select(select)
      .from(transferOffers)
      .innerJoin(players, eq(players.id, transferOffers.playerId))
      .innerJoin(teams, eq(teams.id, transferOffers.sellerTeamId))
      .where(and(eq(transferOffers.bidderTeamId, teamId), eq(transferOffers.status, "pending")))
      .orderBy(asc(transferOffers.expiresAt)),
  ]);
  return { incoming, outgoing };
}

export async function forumThreadList(category: string | null) {
  "use cache: private";
  cacheLife("minutes");
  return db()
    .select({
      id: forumThreads.id,
      title: forumThreads.title,
      category: forumThreads.category,
      replies: forumThreads.replies,
      lastPostAt: forumThreads.lastPostAt,
      author: profiles.username,
    })
    .from(forumThreads)
    .leftJoin(profiles, eq(profiles.userId, forumThreads.authorId))
    .where(category ? eq(forumThreads.category, category as "general") : undefined)
    .orderBy(desc(forumThreads.lastPostAt))
    .limit(50);
}

export async function forumThread(id: string) {
  "use cache: private";
  cacheLife("minutes");
  const [t] = await db()
    .select({ id: forumThreads.id, title: forumThreads.title, category: forumThreads.category })
    .from(forumThreads)
    .where(eq(forumThreads.id, id));
  if (!t) return null;
  const posts = await db()
    .select({
      id: forumPosts.id,
      body: forumPosts.body,
      createdAt: forumPosts.createdAt,
      author: profiles.username,
    })
    .from(forumPosts)
    .leftJoin(profiles, eq(profiles.userId, forumPosts.authorId))
    .where(eq(forumPosts.threadId, id))
    .orderBy(asc(forumPosts.createdAt));
  return { ...t, posts };
}

export async function allNotifications(userId: string) {
  "use cache: private";
  cacheLife("minutes");
  return db()
    .select()
    .from(notifications)
    .where(eq(notifications.userId, userId))
    .orderBy(desc(notifications.createdAt))
    .limit(100);
}

export async function inbox(userId: string) {
  "use cache: private";
  cacheLife("minutes");
  return db()
    .select({
      id: messages.id,
      body: messages.body,
      createdAt: messages.createdAt,
      readAt: messages.readAt,
      from: profiles.username,
    })
    .from(messages)
    .leftJoin(profiles, eq(profiles.userId, messages.fromUserId))
    .where(eq(messages.toUserId, userId))
    .orderBy(desc(messages.createdAt))
    .limit(100);
}

export async function markMessagesRead(userId: string) {
  await db()
    .update(messages)
    .set({ readAt: new Date() })
    .where(and(eq(messages.toUserId, userId), isNull(messages.readAt)));
}

/** Clubs (by name) and managers (by username) for the top bar search. Plain substring match, at most 20 each. */
export async function search(q: string) {
  "use cache: private";
  cacheLife("minutes");
  const term = `%${q.replace(/[%_\\]/g, (c) => `\\${c}`)}%`;
  const [clubs, managers] = await Promise.all([
    db()
      .select({ id: teams.id, name: teams.name, shortName: teams.shortName })
      .from(teams)
      .where(
        and(eq(teams.status, "ready"), or(ilike(teams.name, term), ilike(teams.shortName, term))),
      )
      .limit(20),
    db()
      .select({ username: profiles.username })
      .from(profiles)
      .where(ilike(profiles.username, term))
      .limit(20),
  ]);
  return { clubs, managers: managers.flatMap((m) => (m.username ? [m.username] : [])) };
}
