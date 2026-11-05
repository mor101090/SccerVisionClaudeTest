import { createHash } from "node:crypto";

import type { GeneratorPlayer } from "./generator";

/**
 * The weaker foot in stars (1-5), port of build_teams.weak_foot_stars. The generator sends `weak_foot_stars`; squads
 * saved before it had stars carry only the 1-3 level, turned into stars the way the generator's own squads show:
 * level 1 = 1 star (9%) or 2 (91%), 2 = 3 stars, 3 = 4 stars (87%) or 5 (13%), which one fixed by a hash of the
 * player's id (or, for squads stored without one, his name and shirt number), so a squad never changes between reads.
 * Server-only (node:crypto): squads reach the client with the stars already filled in by withStars().
 */
export function weakFootStars(p: GeneratorPlayer): number {
  const stars = p.preferred_foot.weak_foot_stars ?? p.weak_foot_stars;
  if (stars != null) return stars;
  const level = p.preferred_foot.weak_foot_level;
  const key = p.id ?? `${p.name}#${p.shirt_number}`;
  const h = Number(BigInt(`0x${createHash("md5").update(key).digest("hex")}`) % 100n) / 100;
  return level === 1 ? (h < 0.09 ? 1 : 2) : level === 2 ? 3 : h < 0.13 ? 5 : 4;
}

/** The player with `preferred_foot.weak_foot_stars` always set. */
export function withStars(p: GeneratorPlayer): GeneratorPlayer {
  if (p.preferred_foot.weak_foot_stars != null) return p;
  return { ...p, preferred_foot: { ...p.preferred_foot, weak_foot_stars: weakFootStars(p) } };
}
