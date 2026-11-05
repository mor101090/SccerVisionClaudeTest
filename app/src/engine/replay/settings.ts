// Developer settings shared by the replay viewer and the Unity view: match pace, how the pitch and the sky look, and
// the weather and pitch the engine plays in. Kept in this browser's localStorage.

export type GrassPattern = "along" | "across" | "circles";
export type Sky = "Sunny" | "Cloudy" | "Rain" | "Snow";
/** Pitch upkeep: 2 perfect, 1 average, 0 neglected. */
export type Upkeep = 0 | 1 | 2;

export interface TeamColours {
  primary: string;
  secondary: string;
  /** Shirt numbers (default: black or white, whichever reads best on the shirt). */
  number?: string;
  /** Goalkeeper's shirt (his number follows it until picked). */
  gk?: string;
  gkTrim?: string;
  gkNumber?: string;
  /** Boots (default light grey). */
  boots?: string;
}

/** Referee and assistants: shirt, trim and letter (trim and letter follow the shirt until picked). */
export interface RefColours {
  shirt?: string;
  trim?: string;
  letter?: string;
  boots?: string;
}

export type Stadium = "open" | "stand" | "bowl";

export interface DevSettings {
  /** Match pace in percent (50-100): slows the whole match down. */
  pace: number;
  grass: GrassPattern;
  upkeep: Upkeep;
  sky: Sky;
  /** Strong wind (12 m/s, ~43 km/h) or calm. */
  windy: boolean;
  night: boolean;
  net: string;
  /** null: the team's own kit. */
  home: TeamColours | null;
  away: TeamColours | null;
  /** Referee and assistants (null: black with yellow trim). */
  refs: RefColours | null;
  /** What shades the pitch from a low sun: nothing, a main stand, or a bowl. */
  stadium: Stadium;
  /** Line paint in the snow. */
  snowLines: "yellow" | "orange";
}

export const DEFAULT_SETTINGS: DevSettings = {
  pace: 100,
  grass: "along",
  upkeep: 2,
  sky: "Sunny",
  windy: false,
  night: false,
  net: "#ebf0eb",
  home: null,
  away: null,
  refs: null,
  snowLines: "yellow",
  stadium: "stand",
};

const KEY = "sv-dev-settings-react";

export function loadSettings(): DevSettings {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) {
      const s = { ...DEFAULT_SETTINGS, ...(JSON.parse(raw) as Partial<DevSettings>) };
      if (typeof s.refs === "string") s.refs = { shirt: s.refs }; // older settings
      return s;
    }
  } catch {
    // storage unavailable: defaults
  }
  return { ...DEFAULT_SETTINGS };
}

export function saveSettings(s: DevSettings) {
  try {
    localStorage.setItem(KEY, JSON.stringify(s));
  } catch {
    // storage unavailable: settings last for this visit only
  }
}

/** Engine conditions of these settings (what changes the football). */
export function engineConditions(s: DevSettings) {
  return {
    weather: s.sky === "Rain" ? "rain" : s.sky === "Snow" ? "snow" : "sunny",
    maintenance: s.upkeep === 2 ? 1 : s.upkeep === 1 ? 0.5 : 0,
    wind: s.windy ? 12 : 0,
  };
}

/** Same football (same recording) for both settings? */
export function samePlay(a: DevSettings, b: DevSettings): boolean {
  const x = engineConditions(a);
  const y = engineConditions(b);
  return x.weather === y.weather && x.maintenance === y.maintenance && x.wind === y.wind;
}

/** A team's kit with the colours picked in the settings applied. */
export function applyKit<K extends { primary: string; secondary: string; number: string; gk: string; gkNumber: string; boots?: string }>(kit: K, c: TeamColours | null): K {
  if (!c) return kit;
  return {
    ...kit,
    primary: c.primary,
    secondary: c.secondary,
    number: c.number ?? textOn(c.primary),
    gk: c.gk ?? kit.gk,
    gkNumber: c.gkNumber ?? (c.gk ? textOn(c.gk) : kit.gkNumber),
    gkTrim: c.gkTrim ?? c.secondary,
    boots: c.boots ?? kit.boots,
  };
}

/** The officials' colours with the defaults filled in. */
export function refColours(r: RefColours | null): Required<RefColours> {
  const shirt = r?.shirt ?? "#141414";
  const auto = r?.shirt ? textOn(r.shirt) : "#f6d43a";
  return { shirt, trim: r?.trim ?? auto, letter: r?.letter ?? auto, boots: r?.boots ?? "#e9ecef" };
}

/** Black or white, whichever reads best on the colour. */
export function textOn(hex: string): string {
  const n = parseInt(hex.slice(1), 16);
  return ((n >> 16) * 0.299 + ((n >> 8) & 255) * 0.587 + (n & 255) * 0.114) > 160 ? "#111111" : "#ffffff";
}
