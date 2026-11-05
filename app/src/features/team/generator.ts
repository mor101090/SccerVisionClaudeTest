import { z } from "zod";

/**
 * A player as the Soccer Vision team generator makes him (https://soccer-api.dev-spirit.com, v2) — the same shape as
 * data/teams/*.json. Skill groups the API returns as null (a keeper's "defense", an outfield player's "goalkeeping")
 * are dropped, as tools/teams/fetch_teams.py does.
 */
const group = z.record(z.string(), z.number());

export const generatorPlayerSchema = z.object({
  id: z.string().max(80).optional(),
  name: z.string().min(1).max(80),
  shirt_number: z.number().int().min(1).max(99),
  position: z.enum(["goalkeeper", "fullback", "defender", "midfielder", "winger", "striker"]),
  level: z.enum(["Normal", "Excellent", "Weaker"]),
  age: z.number().int().min(14).max(50),
  height: z.number().int().min(140).max(220),
  preferred_foot: z.object({
    dominant_foot: z.enum(["Left", "Right"]),
    weak_foot_level: z.number().int().min(1).max(3),
    /** 1-5 (the strong foot is 5). Older squads have only the level: see weak-foot.ts. */
    weak_foot_stars: z.number().int().min(1).max(5).optional(),
  }),
  weak_foot_stars: z.number().int().min(1).max(5).optional(),
  stamina: z.number().int().min(1).max(100),
  fitness: z.enum(["Low", "Moderate", "Peak"]),
  nationality: z.string().max(40),
  stats: z
    .record(z.string(), group.nullable())
    .transform(
      (s) =>
        Object.fromEntries(Object.entries(s).filter(([, v]) => v !== null)) as Record<
          string,
          Record<string, number>
        >,
    ),
});

export type GeneratorPlayer = z.infer<typeof generatorPlayerSchema>;
export type GeneratorPosition = GeneratorPlayer["position"];

/** The design's 22: 2 GK, 5 FB, 5 DEF, 5 MID, 2 WNG, 3 ST. */
export const SQUAD_SHAPE: Record<GeneratorPosition, number> = {
  goalkeeper: 2,
  fullback: 5,
  defender: 5,
  midfielder: 5,
  winger: 2,
  striker: 3,
};

/** Name pools the generator knows (chips on the Club step). */
export const NAME_POOLS = [
  { id: "american", label: "American" },
  { id: "israel", label: "Israeli" },
  { id: "spanish", label: "Spanish" },
] as const;
export type NamePool = (typeof NAME_POOLS)[number]["id"];

/** Badge per position (Figma: POS column), shortest first. */
export const POSITION_BADGE: Record<
  GeneratorPosition,
  { code: string; label: string; plural: string }
> = {
  goalkeeper: { code: "GK", label: "Goalkeeper", plural: "Goalkeepers" },
  fullback: { code: "FB", label: "Full-back", plural: "Full-backs" },
  defender: { code: "DEF", label: "Defender", plural: "Defenders" },
  midfielder: { code: "MID", label: "Midfielder", plural: "Midfielders" },
  winger: { code: "WNG", label: "Winger", plural: "Wingers" },
  striker: { code: "ST", label: "Striker", plural: "Strikers" },
};

export const POSITION_ORDER: GeneratorPosition[] = [
  "goalkeeper",
  "fullback",
  "defender",
  "midfielder",
  "winger",
  "striker",
];

/** Group averages as the squad table shows them (MEN ATH DEF ATT PLY SET GK); null where a player has none. */
export function groupAverage(p: GeneratorPlayer, g: string): number | null {
  const v = p.stats[g];
  if (!v) return null;
  const xs = Object.values(v);
  return xs.length ? Math.round(xs.reduce((a, b) => a + b, 0) / xs.length) : null;
}
