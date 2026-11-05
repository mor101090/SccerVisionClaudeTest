import "server-only";

import { and, asc, eq, inArray, isNull, ne, or, sql } from "drizzle-orm";
import { alias } from "drizzle-orm/pg-core";

import type { InputActionBody, LobbyState, ProgressBody } from "@/features/lobby/lobby-schema";

import { db, schema } from "./db";
import { isUniqueViolation } from "./db/errors";

const { lobbies, lobbyInputs, profiles } = schema;
const ACTIVE = ["open", "ready", "playing"] as const;

export class LobbyError extends Error {
  constructor(
    message: string,
    readonly status: 400 | 403 | 404 | 409,
  ) {
    super(message);
  }
}

function randomCode(): string {
  return String(Math.floor(100000 + Math.random() * 900000));
}

export async function createLobby(hostId: string): Promise<{ id: string; code: string }> {
  const seed = Math.floor(Math.random() * 2147483646) + 1;
  const conditions = {
    weather: "Sunny",
    maintenance: 1,
    wind: 0,
    windAngle: (seed * 97) % 360,
    night: false,
  };
  // Close this host's older active lobbies: one game at a time.
  await db()
    .update(lobbies)
    .set({ status: "closed" })
    .where(and(eq(lobbies.hostId, hostId), inArray(lobbies.status, [...ACTIVE])));
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const [row] = await db()
        .insert(lobbies)
        .values({ code: randomCode(), hostId, seed, conditions })
        .returning({ id: lobbies.id, code: lobbies.code });
      return row!;
    } catch (err) {
      if (!isUniqueViolation(err)) throw err;
    }
  }
  throw new LobbyError("Could not allocate a game code, try again", 409);
}

export async function joinLobby(userId: string, code: string): Promise<{ id: string }> {
  const [row] = await db()
    .update(lobbies)
    .set({ guestId: userId, status: "ready" })
    .where(
      and(
        eq(lobbies.code, code),
        eq(lobbies.status, "open"),
        isNull(lobbies.guestId),
        ne(lobbies.hostId, userId),
        sql`${lobbies.expiresAt} > now()`,
      ),
    )
    .returning({ id: lobbies.id });
  if (!row) throw new LobbyError("No open game with that code", 404);
  return row;
}

async function memberLobby(userId: string, id: string) {
  const [lobby] = await db()
    .select()
    .from(lobbies)
    .where(and(eq(lobbies.id, id), or(eq(lobbies.hostId, userId), eq(lobbies.guestId, userId))))
    .limit(1);
  if (!lobby) throw new LobbyError("Game not found", 404);
  return lobby;
}

export async function getLobbyState(userId: string, id: string): Promise<LobbyState> {
  const host = alias(profiles, "host");
  const guest = alias(profiles, "guest");
  // Read the lobby (watermark) BEFORE the inputs: any input applied at or below the watermark
  // was written before the watermark moved, so it is guaranteed to be in the second read.
  const [lobby] = await db()
    .select({
      l: lobbies,
      hostName: sql<string>`coalesce(${host.displayName}, ${host.username}, 'Manager')`,
      guestName: sql<string | null>`coalesce(${guest.displayName}, ${guest.username})`,
    })
    .from(lobbies)
    .leftJoin(host, eq(host.userId, lobbies.hostId))
    .leftJoin(guest, eq(guest.userId, lobbies.guestId))
    .where(and(eq(lobbies.id, id), or(eq(lobbies.hostId, userId), eq(lobbies.guestId, userId))))
    .limit(1);
  if (!lobby) throw new LobbyError("Game not found", 404);

  const inputs = await db()
    .select({
      id: lobbyInputs.id,
      team: lobbyInputs.team,
      kind: lobbyInputs.kind,
      payload: lobbyInputs.payload,
      frame: lobbyInputs.frame,
      seq: lobbyInputs.seq,
    })
    .from(lobbyInputs)
    .where(eq(lobbyInputs.lobbyId, id))
    .orderBy(asc(lobbyInputs.frame), asc(lobbyInputs.seq), asc(lobbyInputs.createdAt));

  const l = lobby.l;
  return {
    id: l.id,
    code: l.code,
    status: l.status,
    role: l.hostId === userId ? "host" : "guest",
    seed: l.seed,
    conditions: l.conditions,
    hostName: lobby.hostName,
    guestName: lobby.guestName,
    hostFrame: l.hostFrame,
    finalFrame: l.finalFrame,
    inputs: inputs.map((i) => ({
      id: i.id,
      team: i.team as 0 | 1,
      action: { kind: i.kind, ...i.payload } as InputActionBody,
      frame: i.frame,
      seq: i.seq,
    })),
  };
}

export async function startLobby(hostId: string, id: string): Promise<void> {
  const [row] = await db()
    .update(lobbies)
    .set({ status: "playing" })
    .where(and(eq(lobbies.id, id), eq(lobbies.hostId, hostId), eq(lobbies.status, "ready")))
    .returning({ id: lobbies.id });
  if (!row) throw new LobbyError("The game is not ready to start", 409);
}

/** The guest asks for a change; the host's engine applies it and assigns the frame. */
export async function requestInput(userId: string, id: string, action: InputActionBody) {
  const lobby = await memberLobby(userId, id);
  if (lobby.guestId !== userId) throw new LobbyError("Only the guest sends requests", 403);
  if (lobby.status !== "playing") throw new LobbyError("The game is not being played", 409);
  const { kind, ...payload } = action;
  const [row] = await db()
    .insert(lobbyInputs)
    .values({ lobbyId: id, userId, team: 1, kind, payload })
    .returning({ id: lobbyInputs.id });
  return row!;
}

/**
 * The host reports its engine's progress: inputs it applied (with frame + order), then the new
 * watermark. Inputs are written first so a guest that sees the watermark also sees them.
 */
export async function reportProgress(hostId: string, id: string, body: ProgressBody) {
  const lobby = await memberLobby(hostId, id);
  if (lobby.hostId !== hostId) throw new LobbyError("Only the host reports progress", 403);
  if (lobby.status !== "playing") throw new LobbyError("The game is not being played", 409);

  for (const a of body.applied) {
    if (a.id) {
      await db()
        .update(lobbyInputs)
        .set({ frame: a.frame, seq: a.seq })
        .where(
          and(eq(lobbyInputs.id, a.id), eq(lobbyInputs.lobbyId, id), isNull(lobbyInputs.frame)),
        );
    } else {
      const { kind, ...payload } = a.action;
      await db()
        .insert(lobbyInputs)
        .values({
          lobbyId: id,
          userId: hostId,
          team: a.team,
          kind,
          payload,
          frame: a.frame,
          seq: a.seq,
        })
        .onConflictDoNothing();
    }
  }

  await db()
    .update(lobbies)
    .set({
      hostFrame: sql`greatest(${lobbies.hostFrame}, ${body.frame})`,
      ...(body.finished ? { finalFrame: body.frame, status: "finished" as const } : {}),
    })
    .where(eq(lobbies.id, id));
}
