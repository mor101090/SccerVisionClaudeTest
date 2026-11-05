import "server-only";

import { z } from "zod";

import { serverEnv } from "@/env";
import {
  generatorPlayerSchema,
  SQUAD_SHAPE,
  type GeneratorPlayer,
  type NamePool,
} from "@/features/team/generator";

const responseSchema = z.object({
  seed: z.number().int(),
  players: z.array(generatorPlayerSchema).min(11).max(40),
});

export class SquadApiError extends Error {}

const squadSize = Object.values(SQUAD_SHAPE).reduce((a, b) => a + b, 0);

/**
 * Asks the team generator for the design's 22 (2 GK, 5 FB, 5 DEF, 5 MID, 2 WNG, 3 ST), named from the chosen pools.
 * `mixed` level: mostly Normal players with the odd Excellent and Weaker one. The same seed always gives the same squad.
 */
export async function generateSquad(
  seed: number,
  pools: NamePool[],
): Promise<{ seed: number; players: GeneratorPlayer[] }> {
  let res: Response;
  try {
    res = await fetch(`${serverEnv.SQUAD_API_URL}/api/v2/team`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ level: "mixed", positions: SQUAD_SHAPE, nationalities: pools, seed }),
      signal: AbortSignal.timeout(20_000),
      cache: "no-store",
    });
  } catch {
    throw new SquadApiError("The squad generator is not answering. Try again in a moment.");
  }
  if (!res.ok)
    throw new SquadApiError("The squad generator is not answering. Try again in a moment.");
  const parsed = responseSchema.safeParse(await res.json().catch(() => null));
  if (!parsed.success)
    throw new SquadApiError("The squad generator sent a squad we couldn't read.");
  // The XI picker needs exactly the asked-for squad (a keeper, wide players on both sides...): never store anything else.
  const counts: Record<string, number> = {};
  for (const p of parsed.data.players) counts[p.position] = (counts[p.position] ?? 0) + 1;
  const wrong = (Object.keys(SQUAD_SHAPE) as (keyof typeof SQUAD_SHAPE)[]).some(
    (pos) => counts[pos] !== SQUAD_SHAPE[pos],
  );
  if (wrong || parsed.data.players.length !== squadSize)
    throw new SquadApiError("The squad generator sent an incomplete squad. Try again in a moment.");
  return parsed.data;
}
