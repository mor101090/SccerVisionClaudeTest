import { z } from "zod";

import { planTextIsSafe } from "./plan-text";

export const lobbyCodeSchema = z.string().regex(/^\d{6}$/, "Enter the 6-digit game code");

export const inputActionSchema = z.discriminatedUnion("kind", [
  z.object({
    kind: z.literal("plan"),
    plan: z.string().max(20_000).refine(planTextIsSafe, "That plan cannot be read"),
  }),
  z.object({
    kind: z.literal("sub"),
    out: z.number().int().min(1).max(99),
    in: z.number().int().min(1).max(99),
  }),
]);
export type InputActionBody = z.infer<typeof inputActionSchema>;

export const progressSchema = z.object({
  frame: z.number().int().min(0),
  finished: z.boolean().default(false),
  applied: z
    .array(
      z.object({
        /** Guest request id being confirmed; absent for the host's own inputs. */
        id: z.uuid().optional(),
        team: z.union([z.literal(0), z.literal(1)]),
        action: inputActionSchema,
        frame: z.number().int().min(0),
        seq: z.number().int().min(1),
      }),
    )
    .max(50)
    .default([]),
});
export type ProgressBody = z.infer<typeof progressSchema>;

/** What both clients poll. */
export interface LobbyState {
  id: string;
  code: string;
  status: "open" | "ready" | "playing" | "finished" | "closed";
  role: "host" | "guest";
  seed: number;
  conditions: {
    weather: string;
    maintenance: number;
    wind: number;
    windAngle: number;
    night: boolean;
  };
  hostName: string;
  guestName: string | null;
  hostFrame: number;
  finalFrame: number | null;
  inputs: {
    id: string;
    team: 0 | 1;
    action: InputActionBody;
    frame: number | null;
    seq: number | null;
  }[];
}
