import { z } from "zod";

/** Max gzipped replay upload (Vercel function bodies are capped at 4.5 MB). */
export const MAX_REPLAY_BYTES = 4 * 1024 * 1024;

export const matchConditionsSchema = z.object({
  weather: z.enum(["Sunny", "Cloudy", "Rain", "Snow"]),
  maintenance: z.number().min(0).max(1),
  wind: z.number().min(0).max(40),
  windAngle: z.number().min(0).max(360),
  night: z.boolean(),
});

export const saveMatchSchema = z.object({
  mode: z.enum(["staged", "random", "live", "online"]),
  seed: z.number().int().min(0).max(2147483647),
  engineVersion: z.string().regex(/^[0-9a-f]{12}$/),
  conditions: matchConditionsSchema,
  homeName: z.string().min(1).max(80),
  awayName: z.string().min(1).max(80),
  homeScore: z.number().int().min(0).max(99),
  awayScore: z.number().int().min(0).max(99),
  homePlan: z.string().max(20_000).optional(),
  awayPlan: z.string().max(20_000).optional(),
  /** The manager's club as LiveEngine.UseHomeTeam text, when it played at home (checked against the stored club). */
  homeSquad: z.string().max(20_000).optional(),
  summary: z.record(z.string(), z.unknown()).optional(),
});

export type SaveMatchInput = z.infer<typeof saveMatchSchema>;
