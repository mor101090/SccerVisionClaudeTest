/**
 * Display rules for the homepage (Figma "Data & logic" board, file xMPwRHYFfISl5Q6nRADVM6). Pure functions: the
 * server reads the rows, these decide what the cards say. A brand-new club has no league, money or rating rows,
 * so every rule has a "nothing yet" answer instead of a zero that looks like a real number.
 */

export type Result = "W" | "D" | "L";

/** A finished match from the club's point of view. */
export type ClubResult = {
  goalsFor: number;
  goalsAgainst: number;
  /** Share of possession 0..1, when the replay summary had it. */
  possession: number | null;
};

/** Matches needed before a club gets a world rating (Figma: "Play 3 matches to get a rating"). */
export const RATED_AFTER = 3;

export function resultOf(m: { goalsFor: number; goalsAgainst: number }): Result {
  if (m.goalsFor > m.goalsAgainst) return "W";
  if (m.goalsFor < m.goalsAgainst) return "L";
  return "D";
}

/** "Good morning" 05–12, "Good afternoon" 12–18, "Good evening" 18–05, by the manager's local hour. */
export function greeting(hour: number): string {
  if (hour >= 5 && hour < 12) return "Good morning";
  if (hour >= 12 && hour < 18) return "Good afternoon";
  return "Good evening";
}

export type Summary = {
  games: number;
  won: number;
  drawn: number;
  lost: number;
  goalsPerGame: number | null;
  possession: number | null;
};

/** Totals over the given matches (newest first). Averages are null when there is nothing to average. */
export function summarize(results: ClubResult[]): Summary {
  let won = 0;
  let drawn = 0;
  let lost = 0;
  let goals = 0;
  let pos = 0;
  let posGames = 0;
  for (const r of results) {
    const res = resultOf(r);
    if (res === "W") won += 1;
    else if (res === "D") drawn += 1;
    else lost += 1;
    goals += r.goalsFor;
    if (r.possession !== null) {
      pos += r.possession;
      posGames += 1;
    }
  }
  const games = results.length;
  return {
    games,
    won,
    drawn,
    lost,
    goalsPerGame: games ? Math.round((goals / games) * 10) / 10 : null,
    possession: posGames ? Math.round((pos / posGames) * 100) / 100 : null,
  };
}

/** Points share of a summary, 0..1 (a win 3, a draw 1): how full the results gauge is. */
export function pointsShare(s: Summary): number {
  return s.games ? (s.won * 3 + s.drawn) / (s.games * 3) : 0;
}

export type Mood = 1 | 2 | 3 | 4 | 5;
export const MOOD_LABELS: Record<Mood, string> = {
  1: "Furious",
  2: "Unhappy",
  3: "Hopeful",
  4: "Happy",
  5: "Excellent",
};
export const MOOD_LINES: Record<Mood, string> = {
  1: "fans want answers",
  2: "fans expect better",
  3: "waiting to be won over",
  4: "fans like what they see",
  5: "fans love the results",
};

/**
 * Fan mood from the last five results when the club has no supporters row yet: 3 (hopeful) with no matches,
 * then up or down with the points taken.
 */
export function moodFromResults(last5: Result[]): Mood {
  if (last5.length === 0) return 3;
  const pts = last5.reduce((n, r) => n + (r === "W" ? 3 : r === "D" ? 1 : 0), 0);
  const share = pts / (last5.length * 3);
  if (share >= 0.8) return 5;
  if (share >= 0.55) return 4;
  if (share >= 0.35) return 3;
  if (share >= 0.15) return 2;
  return 1;
}

/** Countdown to a kick-off: null once it has started. Days, hours, minutes rounded down. */
export function countdown(
  kickoff: Date,
  now: Date,
): { days: number; hours: number; minutes: number } | null {
  const ms = kickoff.getTime() - now.getTime();
  if (ms <= 0) return null;
  const minutes = Math.floor(ms / 60_000);
  return {
    days: Math.floor(minutes / 1440),
    hours: Math.floor((minutes % 1440) / 60),
    minutes: minutes % 60,
  };
}

export type SubtitleInput = {
  liveFixture: boolean;
  /** Hours until the next official fixture, null without one. */
  hoursToNext: number | null;
  lineupConfirmed: boolean;
  pendingOffers: number;
  seasonLabel: string | null;
  matchesPlayed: number;
};

/** The line under the greeting, in priority order (Figma "Subtitle priority"). */
export function subtitle(s: SubtitleInput): string {
  if (s.liveFixture) return "Your team is on the pitch right now.";
  if (s.hoursToNext !== null && s.hoursToNext < 48 && !s.lineupConfirmed)
    return "Match in under 2 days — your lineup is not confirmed yet.";
  if (s.pendingOffers > 0)
    return `${s.pendingOffers} transfer offer${s.pendingOffers === 1 ? "" : "s"} waiting for your answer.`;
  if (s.seasonLabel) return s.seasonLabel;
  if (s.matchesPlayed === 0)
    return "Your club is ready. Play a friendly to see your squad in action.";
  return "Leagues are coming soon. Friendlies count for your form, stats and fan mood.";
}

/** "RC" from "redcoach" / "Red Coach": first letters of the first two words, else the first two letters. */
export function initials(name: string): string {
  const words = name
    .trim()
    .split(/[\s_.-]+/)
    .filter(Boolean);
  if (words.length >= 2) return (words[0]![0]! + words[1]![0]!).toUpperCase();
  return (words[0] ?? "?").slice(0, 2).toUpperCase();
}

/** Badge text: hidden at 0, "9+" above nine. */
export function badge(count: number): string | null {
  if (count <= 0) return null;
  return count > 9 ? "9+" : String(count);
}

/** Whole euros with thousands separators: €673,000. */
export function euros(amount: number): string {
  const sign = amount < 0 ? "−" : "";
  return `${sign}€${Math.abs(Math.round(amount)).toLocaleString("en-US")}`;
}
