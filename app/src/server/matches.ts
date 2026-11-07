import "server-only";

import { and, count, desc, eq } from "drizzle-orm";

import type { SaveMatchInput } from "@/features/matches/match-schema";
import { REPLAY_CONTENT_TYPE } from "@/features/matches/replay-codec";

import { db, schema } from "./db";
import { BUCKETS, deleteObject, getObjectBytes, putObject } from "./storage";

const { matches } = schema;

export function replayKey(userId: string, matchId: string) {
  return `users/${userId}/matches/${matchId}.json.gz`;
}

/** Stores the replay first, then the row; removes the replay if the insert fails. */
export async function saveMatch(
  userId: string,
  input: SaveMatchInput,
  replayGz: Uint8Array,
  homeTeamId: string | null = null,
) {
  const id = crypto.randomUUID();
  const key = replayKey(userId, id);
  await putObject(
    BUCKETS.replays,
    key,
    replayGz,
    REPLAY_CONTENT_TYPE,
    "private, max-age=31536000, immutable",
  );
  try {
    const [row] = await db()
      .insert(matches)
      .values({ id, userId, ...input, homeTeamId, replayKey: key })
      .returning({ id: matches.id });
    return row!;
  } catch (err) {
    await deleteObject(BUCKETS.replays, key).catch(() => undefined);
    throw err;
  }
}

export async function listMatches(userId: string, limit = 50) {
  return db()
    .select({
      id: matches.id,
      mode: matches.mode,
      seed: matches.seed,
      homeName: matches.homeName,
      awayName: matches.awayName,
      homeScore: matches.homeScore,
      awayScore: matches.awayScore,
      playedAt: matches.playedAt,
    })
    .from(matches)
    .where(eq(matches.userId, userId))
    .orderBy(desc(matches.playedAt))
    .limit(limit);
}

export async function countMatches(userId: string): Promise<number> {
  const [row] = await db().select({ n: count() }).from(matches).where(eq(matches.userId, userId));
  return row?.n ?? 0;
}

/** A match only its owner can read. */
export async function getMatch(userId: string, id: string) {
  const [row] = await db()
    .select()
    .from(matches)
    .where(and(eq(matches.id, id), eq(matches.userId, userId)))
    .limit(1);
  return row ?? null;
}

export async function getReplayBytes(userId: string, id: string) {
  const match = await getMatch(userId, id);
  if (!match?.replayKey) return null;
  return getObjectBytes(BUCKETS.replays, match.replayKey);
}
