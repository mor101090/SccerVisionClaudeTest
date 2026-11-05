import type { KitPick, TeamKits } from "@/server/db/schema";

import type { EngineKit } from "./engine-team";

/**
 * The 14 kit colours of the Figma "Your kits" palette. These are user content (shirt fabric), not UI colours,
 * so they live here as data rather than as palette tokens.
 */
export const KIT_COLORS = [
  { id: "red", label: "Red", hex: "#d32f2f" },
  { id: "blue", label: "Blue", hex: "#1a54df" },
  { id: "white", label: "White", hex: "#f2f2f2" },
  { id: "black", label: "Black", hex: "#1b1b1b" },
  { id: "green", label: "Green", hex: "#2e9e4f" },
  { id: "yellow", label: "Yellow", hex: "#f9d71c" },
  { id: "orange", label: "Orange", hex: "#f57c00" },
  { id: "sky", label: "Sky blue", hex: "#4fb3e8" },
  { id: "navy", label: "Navy", hex: "#14244f" },
  { id: "purple", label: "Purple", hex: "#6a3fb5" },
  { id: "maroon", label: "Maroon", hex: "#7b1e2b" },
  { id: "pink", label: "Pink", hex: "#e05aa0" },
  { id: "grey", label: "Grey", hex: "#7d8796" },
  { id: "cyan", label: "Cyan", hex: "#18c6c6" },
] as const;

export type KitColorId = (typeof KIT_COLORS)[number]["id"];
export const KIT_COLOR_IDS = KIT_COLORS.map((c) => c.id) as [KitColorId, ...KitColorId[]];

export function kitHex(id: string): string {
  return KIT_COLORS.find((c) => c.id === id)?.hex ?? "#7d8796";
}

/** Colours too close to tell apart on the pitch (Figma greys these out). Symmetric. */
const NEAR: Record<KitColorId, KitColorId[]> = {
  red: ["orange", "maroon"],
  blue: ["sky", "purple", "cyan"],
  white: ["grey"],
  black: ["navy"],
  green: [],
  yellow: ["orange"],
  orange: ["red", "yellow"],
  sky: ["blue", "cyan"],
  navy: ["black"],
  purple: ["blue"],
  maroon: ["red"],
  pink: [],
  grey: ["white"],
  cyan: ["blue", "sky"],
};

export function clashes(a: string, b: string): boolean {
  return a === b || (NEAR[a as KitColorId]?.includes(b as KitColorId) ?? false);
}

export type KitName = keyof TeamKits;
export type KitPart = keyof KitPick;

/**
 * Whether a colour is greyed out for one part of one kit:
 * the home and away shirts must differ, the keeper's shirt must differ from both, and every number must read on
 * its own shirt. Trim and socks are free.
 */
export function isBlocked(kits: TeamKits, kit: KitName, part: KitPart, color: string): boolean {
  if (part === "number") return clashes(color, kits[kit].shirt);
  if (part !== "shirt") return false;
  if (kit === "home") return clashes(color, kits.away.shirt);
  if (kit === "away") return clashes(color, kits.home.shirt);
  return clashes(color, kits.home.shirt) || clashes(color, kits.away.shirt);
}

/** Every picked colour is allowed (the server checks this before saving). */
export function kitsValid(kits: TeamKits): boolean {
  return (["home", "away", "keeper"] as const).every((k) =>
    (["shirt", "trim", "socks", "number"] as const).every(
      (p) => !isBlocked(kits, k, p, kits[k][p]),
    ),
  );
}

export const DEFAULT_KITS: TeamKits = {
  home: { shirt: "red", trim: "black", socks: "black", number: "white" },
  away: { shirt: "blue", trim: "white", socks: "white", number: "white" },
  keeper: { shirt: "yellow", trim: "black", socks: "black", number: "black" },
};

/** The engine plays the home kit and the keeper's kit (KitColors in TeamSetup). */
export function toEngineKit(kits: TeamKits): EngineKit {
  return {
    primary: kitHex(kits.home.shirt),
    secondary: kitHex(kits.home.trim),
    number: kitHex(kits.home.number),
    gkPrimary: kitHex(kits.keeper.shirt),
    gkNumber: kitHex(kits.keeper.number),
    gkSecondary: kitHex(kits.keeper.trim),
  };
}

/**
 * After a pick, moves any colour it made clash to the first one allowed (the keeper's shirt, then every number),
 * so the form never holds an impossible combination.
 */
export function repairKits(kits: TeamKits): TeamKits {
  const out: TeamKits = {
    home: { ...kits.home },
    away: { ...kits.away },
    keeper: { ...kits.keeper },
  };
  const fix = (kit: KitName, part: KitPart, prefer: string[]) => {
    if (!isBlocked(out, kit, part, out[kit][part])) return;
    const order = [...prefer, ...KIT_COLOR_IDS];
    out[kit][part] = order.find((c) => !isBlocked(out, kit, part, c)) ?? out[kit][part];
  };
  fix("keeper", "shirt", ["yellow", "green", "black", "pink"]);
  for (const k of ["home", "away", "keeper"] as const) fix(k, "number", ["white", "black"]);
  return out;
}
