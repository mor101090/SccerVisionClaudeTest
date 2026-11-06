import "server-only";

import { desc, eq, sql } from "drizzle-orm";
import { cacheLife } from "next/cache";

import type { User } from "./auth";
import { db, schema } from "./db";
import type { KitPick } from "./db/schema";
import { getProfile } from "./profiles";
import { getTeam } from "./teams";

const { notifications, messages, transferOffers, profiles, teams } = schema;

export type ShellNotification = {
  id: string;
  kind: "match" | "transfer" | "training" | "injury" | "finance" | "club" | "system";
  title: string;
  body: string | null;
  href: string | null;
  unread: boolean;
  createdAt: Date;
};

export type ShellMessage = {
  id: string;
  from: string;
  body: string;
  unread: boolean;
  createdAt: Date;
};

export type ShellData = {
  manager: { name: string; username: string | null; email: string };
  club: { name: string; meta: string; kit: KitPick | null } | null;
  counts: { notifications: number; messages: number; offers: number };
  notifications: ShellNotification[];
  messages: ShellMessage[];
};

/** What the sidebar and top bar show on every signed-in page. */
export async function getShellData(user: User): Promise<ShellData> {
  "use cache: private";
  cacheLife("minutes");
  const [team, profile, recent, inbox, badgeRows] = await Promise.all([
    getTeam(user.id),
    getProfile(user.id),
    db()
      .select()
      .from(notifications)
      .where(eq(notifications.userId, user.id))
      .orderBy(desc(notifications.createdAt))
      .limit(6),
    db()
      .select({
        id: messages.id,
        body: messages.body,
        readAt: messages.readAt,
        createdAt: messages.createdAt,
        from: profiles.username,
        fromName: profiles.displayName,
      })
      .from(messages)
      .leftJoin(profiles, eq(profiles.userId, messages.fromUserId))
      .where(eq(messages.toUserId, user.id))
      .orderBy(desc(messages.createdAt))
      .limit(5),
    // the three badge counts in one round trip
    db().execute<{ notifications: number; messages: number; offers: number }>(sql`
      select
        (select count(*)::int from ${notifications}
          where ${notifications.userId} = ${user.id} and ${notifications.readAt} is null) as notifications,
        (select count(*)::int from ${messages}
          where ${messages.toUserId} = ${user.id} and ${messages.readAt} is null) as messages,
        (select count(*)::int from ${transferOffers} join ${teams} on ${teams.id} = ${transferOffers.sellerTeamId}
          where ${teams.ownerId} = ${user.id} and ${transferOffers.status} = 'pending') as offers
    `),
  ]);
  const counts = badgeRows[0] ?? { notifications: 0, messages: 0, offers: 0 };
  return {
    manager: { name: user.name, username: profile?.username ?? null, email: user.email },
    club: team
      ? {
          name: team.name,
          meta: team.status === "ready" ? "Friendlies" : "Club in progress",
          kit: team.kits?.home ?? null,
        }
      : null,
    counts: {
      notifications: Number(counts.notifications),
      messages: Number(counts.messages),
      offers: Number(counts.offers),
    },
    notifications: recent.map((n) => ({
      id: n.id,
      kind: n.kind,
      title: n.title,
      body: n.body,
      href: n.href,
      unread: !n.readAt,
      createdAt: n.createdAt,
    })),
    messages: inbox.map((m) => ({
      id: m.id,
      from: m.from ?? m.fromName ?? "Manager",
      body: m.body,
      unread: !m.readAt,
      createdAt: m.createdAt,
    })),
  };
}
