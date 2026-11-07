import "server-only";

import { randomInt } from "node:crypto";

import { and, asc, eq, gt, or, sql } from "drizzle-orm";
import { cacheLife } from "next/cache";
import { cache } from "react";

import { engineAttributeMap, positionLabel, toEngineTeam } from "@/features/team/engine-team";
import type { GeneratorPlayer, NamePool } from "@/features/team/generator";
import { generatorPlayerSchema } from "@/features/team/generator";
import { DEFAULT_KITS, toEngineKit } from "@/features/team/kits";
import { slotsFor, suggestLineup } from "@/features/team/lineup";
import { withStars } from "@/features/team/weak-foot";

import { db, schema } from "./db";
import { isUniqueViolation } from "./db/errors";
import type { Crest, TeamKits } from "./db/schema";
import { generateSquad } from "./squad-api";

const { teams, players, matches } = schema;

export type TeamRow = typeof teams.$inferSelect;
export type SquadPlayer = {
  id: string;
  starterSlot: number | null;
  benchOrder: number | null;
  p: GeneratorPlayer;
};

export class TeamError extends Error {}

export async function getTeam(userId: string): Promise<TeamRow | null> {
  "use cache: private";
  cacheLife("minutes");
  const [t] = await db().select().from(teams).where(eq(teams.ownerId, userId));
  return t ?? null;
}

export async function getSquad(teamId: string): Promise<SquadPlayer[]> {
  "use cache: private";
  cacheLife("minutes");
  const rows = await db()
    .select()
    .from(players)
    .where(eq(players.teamId, teamId))
    .orderBy(asc(players.number));
  return rows.map((r) => ({
    id: r.id,
    starterSlot: r.starterSlot,
    benchOrder: r.benchOrder,
    p: withStars(generatorPlayerSchema.parse(r.profile)),
  }));
}

/** getTeam / getSquad once per request, shared by the layout and the page. Renders only (not after a write). */
export const teamForRequest = cache(getTeam);

/** The furthest step this manager may open: manager → club → kits → squad. */
export type Step = "manager" | "club" | "kits" | "squad";
export function currentStep(
  profile: { nationality: string | null } | null,
  team: TeamRow | null,
): Step {
  if (!profile?.nationality) return "manager";
  if (!team) return "club";
  if (!team.kits) return "kits";
  return "squad";
}

// ------------------------------------------------------------------ club + kits

export async function saveClub(
  userId: string,
  club: { name: string; shortName: string; namePools: NamePool[]; crestPrompt?: string },
): Promise<void> {
  const crest: Crest = {
    kind: "shield",
    ...(club.crestPrompt ? { prompt: club.crestPrompt } : {}),
  };
  const values = { name: club.name, shortName: club.shortName, namePools: club.namePools, crest };
  await db()
    .insert(teams)
    .values({ ownerId: userId, kit: kitFor(DEFAULT_KITS), ...values })
    .onConflictDoUpdate({ target: teams.ownerId, set: values });
}

/** The engine kit column (home + keeper) kept in step with the picked kits. */
function kitFor(kits: TeamKits) {
  const k = toEngineKit(kits);
  return {
    primary: k.primary,
    secondary: k.secondary,
    number: k.number,
    gk: k.gkPrimary,
    gkNumber: k.gkNumber,
    gkTrim: k.gkSecondary,
  };
}

export async function saveKits(userId: string, kits: TeamKits): Promise<void> {
  const res = await db()
    .update(teams)
    .set({ kits, kit: kitFor(kits) })
    .where(eq(teams.ownerId, userId))
    .returning({ id: teams.id });
  if (!res.length) throw new TeamError("Name your club first.");
}

// ------------------------------------------------------------------ squad

/** Shirt numbers must be unique in a squad; the generator's rarely clash, but move any duplicate to a free number. */
function uniqueNumbers(squad: GeneratorPlayer[]): GeneratorPlayer[] {
  const used = new Set<number>();
  return squad.map((p) => {
    let n = p.shirt_number;
    if (used.has(n)) for (n = 2; used.has(n) && n < 99; n++);
    used.add(n);
    return n === p.shirt_number ? p : { ...p, shirt_number: n };
  });
}

/** Matches the club has played (saved with it as home or away team). */
async function clubMatches(teamId: string): Promise<number> {
  const [row] = await db()
    .select({ n: sql<number>`count(*)::int` })
    .from(matches)
    .where(or(eq(matches.homeTeamId, teamId), eq(matches.awayTeamId, teamId)));
  return row?.n ?? 0;
}

/**
 * Squad writes of one team run one at a time: every batch that replaces players or moves starters takes this
 * transaction lock first, so two requests (two tabs, a retry) never interleave their deletes and inserts.
 */
type Tx = Parameters<Parameters<ReturnType<typeof db>["transaction"]>[0]>[0];

/** Serializes squad writes for one team; held until the surrounding transaction ends. */
const lockTeam = (tx: Tx, teamId: string) =>
  tx.execute(sql`select pg_advisory_xact_lock(hashtextextended(${teamId}, 0))`);

/**
 * Makes the squad at the team generator and stores it with the suggested XI and bench.
 * `regenerate`: replaces an existing squad, which costs one of the two regenerations (none left after a first match).
 */
export async function makeSquad(userId: string, regenerate: boolean): Promise<void> {
  const team = await getTeam(userId);
  if (!team?.kits) throw new TeamError("Pick your kits first.");
  const existing = await db()
    .select({ id: players.id })
    .from(players)
    .where(eq(players.teamId, team.id))
    .limit(1);
  if (existing.length && !regenerate) return; // already made (a double click, a refresh)
  if (regenerate) {
    if ((await clubMatches(team.id)) > 0)
      throw new TeamError("Your squad is fixed after your first match.");
    // Take one regeneration atomically: two requests at once can't both use the last one.
    const taken = await db()
      .update(teams)
      .set({ regenerationsLeft: sql`${teams.regenerationsLeft} - 1` })
      .where(and(eq(teams.id, team.id), gt(teams.regenerationsLeft, 0)))
      .returning({ id: teams.id });
    if (!taken.length) throw new TeamError("No regenerations left.");
  }

  try {
    const pools = (team.namePools ?? ["american"]) as NamePool[];
    const { seed, players: made } = await generateSquad(randomInt(1, 2_000_000_000), pools);
    const squad = uniqueNumbers(made.map(withStars));
    const { starters, bench } = suggestLineup(squad);
    if (starters.length !== 11) throw new TeamError("That squad has no full XI. Try again.");

    const rows = squad.map((p, i) => ({
      teamId: team.id,
      number: p.shirt_number,
      name: p.name,
      shortName: p.name.split(" ").slice(1).join(" ") || p.name,
      role: positionLabel(p),
      attributes: engineAttributeMap(p),
      position: p.position,
      level: p.level,
      profile: p as unknown as Record<string, unknown>,
      starterSlot: starters.includes(i) ? starters.indexOf(i) : null,
      benchOrder: bench.includes(i) ? bench.indexOf(i) : null,
    }));

    // One transaction: lock the team, drop the old squad, store the new one, update the team.
    await db().transaction(async (tx) => {
      await lockTeam(tx, team.id);
      await tx.delete(players).where(eq(players.teamId, team.id));
      await tx.insert(players).values(rows);
      await tx.update(teams).set({ squadSeed: seed, captainId: null }).where(eq(teams.id, team.id));
    });
  } catch (err) {
    // Nothing was stored: give the regeneration back.
    if (regenerate)
      await db()
        .update(teams)
        .set({ regenerationsLeft: sql`${teams.regenerationsLeft} + 1` })
        .where(eq(teams.id, team.id))
        .catch(() => undefined);
    throw err;
  }
}

async function ownPlayer(userId: string, playerId: string) {
  const team = await getTeam(userId);
  if (!team) throw new TeamError("No club yet.");
  const [p] = await db()
    .select()
    .from(players)
    .where(and(eq(players.id, playerId), eq(players.teamId, team.id)));
  if (!p) throw new TeamError("That player is not in your squad.");
  return { team, p };
}

export async function setCaptain(userId: string, playerId: string): Promise<void> {
  const { team } = await ownPlayer(userId, playerId);
  await db().update(teams).set({ captainId: playerId }).where(eq(teams.id, team.id));
}

/**
 * Puts a player in the starting XI: he takes the slot of the starter in his own position (or the nearest slot for
 * it), and that starter takes his place on the bench (or out of the match-day 20).
 */
export async function setStarter(userId: string, playerId: string): Promise<void> {
  const { team, p } = await ownPlayer(userId, playerId);
  if (p.starterSlot !== null) return;
  const squad = await db().select().from(players).where(eq(players.teamId, team.id));
  const bySlot = new Map(
    squad.filter((r) => r.starterSlot !== null).map((r) => [r.starterSlot!, r]),
  );
  const slots = slotsFor(p.position as GeneratorPlayer["position"]);
  const slot = slots.find((s) => bySlot.get(s)?.position === p.position) ?? slots[0]!;
  const out = bySlot.get(slot);
  // Locked like makeSquad; the unique (team, starter slot) index rejects a swap based on a stale read.
  try {
    await db().transaction(async (tx) => {
      await lockTeam(tx, team.id);
      await tx
        .update(players)
        .set({ starterSlot: null, benchOrder: p.benchOrder })
        .where(eq(players.id, out?.id ?? p.id));
      await tx
        .update(players)
        .set({ starterSlot: slot, benchOrder: null })
        .where(eq(players.id, p.id));
    });
  } catch (err) {
    if (isUniqueViolation(err)) throw new TeamError("Your lineup just changed. Try again.");
    throw err;
  }
}

export async function finishTeam(userId: string): Promise<void> {
  const team = await getTeam(userId);
  if (!team) throw new TeamError("No club yet.");
  const [row] = await db()
    .select({
      all: sql<number>`count(*)::int`,
      xi: sql<number>`count(${players.starterSlot})::int`,
    })
    .from(players)
    .where(eq(players.teamId, team.id));
  if (!row?.all) throw new TeamError("Generate your squad first.");
  if (row.xi !== 11) throw new TeamError("Your starting XI needs exactly 11 players.");
  await db().update(teams).set({ status: "ready" }).where(eq(teams.id, team.id));
}

/** The manager's ready club as LiveEngine.UseHomeTeam text, or null (no club yet: the demo teams play). */
export async function engineTeamFor(
  team: TeamRow | null,
  manager: string,
): Promise<{ name: string; text: string; plan: string | null } | null> {
  if (!team || team.status !== "ready" || !team.kits) return null;
  const squad = await getSquad(team.id);
  const starters = squad
    .filter((s) => s.starterSlot !== null)
    .sort((a, b) => a.starterSlot! - b.starterSlot!);
  const bench = squad
    .filter((s) => s.benchOrder !== null)
    .sort((a, b) => a.benchOrder! - b.benchOrder!);
  if (starters.length !== 11) return null;
  const text = toEngineTeam({
    name: team.name,
    shortName: team.shortName,
    manager,
    kit: toEngineKit(team.kits),
    seed: team.squadSeed ?? 1,
    starters: starters.map((s) => s.p),
    bench: bench.slice(0, 9).map((s) => s.p),
  });
  return { name: team.name, text, plan: team.plan };
}
