import type { IconName } from "@/components/app-shell/app-icon";

/**
 * Manager record and achievements (Figma "Profile – your profile" / "Profile – achievements"), computed from the
 * saved matches only. Achievements are badges: they never change how a match plays.
 */

/** One saved match from the manager's side. */
export type MatchFact = {
  goalsFor: number;
  goalsAgainst: number;
  online: boolean;
  /** Formation the manager's side started with (from the saved plan), when known. */
  formation: string | null;
  /** Ratings and goals of the manager's players (full-time records), when saved. */
  players: { rating: number; goals: number }[];
  playedAt: Date;
};

export type Record_ = {
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  /** Best single player rating in any match, null before a rated match. */
  bestRating: number | null;
};

export function recordOf(matches: MatchFact[]): Record_ {
  let won = 0;
  let drawn = 0;
  let lost = 0;
  let goalsFor = 0;
  let goalsAgainst = 0;
  let best: number | null = null;
  for (const m of matches) {
    if (m.goalsFor > m.goalsAgainst) won++;
    else if (m.goalsFor < m.goalsAgainst) lost++;
    else drawn++;
    goalsFor += m.goalsFor;
    goalsAgainst += m.goalsAgainst;
    for (const p of m.players) if (best === null || p.rating > best) best = p.rating;
  }
  return { played: matches.length, won, drawn, lost, goalsFor, goalsAgainst, bestRating: best };
}

export type Achievement = {
  id: string;
  group: "Matches" | "Players" | "Community";
  icon: IconName;
  title: string;
  /** How to earn it. */
  how: string;
  earned: boolean;
  /** "4 / 10" for counted ones not earned yet. */
  progress?: string;
};

export type CommunityFacts = {
  squadComplete: boolean;
  /** Managers this one invited who have played at least one match. */
  invitedWhoPlayed: number;
  supporter: boolean;
};

/** Longest run of matches without a defeat, oldest first. */
function longestUnbeaten(matches: MatchFact[]): number {
  let run = 0;
  let best = 0;
  for (const m of [...matches].sort((a, b) => a.playedAt.getTime() - b.playedAt.getTime())) {
    run = m.goalsFor >= m.goalsAgainst ? run + 1 : 0;
    best = Math.max(best, run);
  }
  return best;
}

const progress = (have: number, need: number) => (have >= need ? undefined : `${have} / ${need}`);

export function achievementsOf(matches: MatchFact[], c: CommunityFacts): Achievement[] {
  const wins = matches.filter((m) => m.goalsFor > m.goalsAgainst);
  const unbeaten = longestUnbeaten(matches);
  const formations = new Set(wins.map((m) => m.formation).filter(Boolean));
  const players = matches.flatMap((m) => m.players);
  return [
    {
      id: "first-win",
      group: "Matches",
      icon: "trophy",
      title: "First win",
      how: "Win a match",
      earned: wins.length > 0,
    },
    {
      id: "clean-sheet",
      group: "Matches",
      icon: "shield",
      title: "Clean sheet",
      how: "Win without conceding",
      earned: wins.some((m) => m.goalsAgainst === 0),
    },
    {
      id: "goal-fest",
      group: "Matches",
      icon: "ball",
      title: "Goal fest",
      how: "Score 5 in a match",
      earned: matches.some((m) => m.goalsFor >= 5),
    },
    {
      id: "unbeaten-10",
      group: "Matches",
      icon: "star",
      title: "Unbeaten 10",
      how: "10 matches in a row without a defeat",
      earned: unbeaten >= 10,
      progress: progress(unbeaten, 10),
    },
    {
      id: "hat-trick",
      group: "Players",
      icon: "ball",
      title: "Hat-trick hero",
      how: "One of your players scores 3",
      earned: players.some((p) => p.goals >= 3),
    },
    {
      id: "perfect-10",
      group: "Players",
      icon: "star",
      title: "Perfect 10",
      how: "One of your players is rated 10",
      earned: players.some((p) => p.rating >= 10),
    },
    {
      id: "full-squad",
      group: "Players",
      icon: "users",
      title: "Full squad",
      how: "Sign a squad of 22",
      earned: c.squadComplete,
    },
    {
      id: "tactician",
      group: "Players",
      icon: "flag",
      title: "Tactician",
      how: "Win with 3 different formations",
      earned: formations.size >= 3,
      progress: progress(formations.size, 3),
    },
    {
      id: "recruiter",
      group: "Community",
      icon: "share",
      title: "Recruiter",
      how: "A friend you invited plays a match",
      earned: c.invitedWhoPlayed >= 1,
    },
    {
      id: "club-founder",
      group: "Community",
      icon: "users",
      title: "Club founder",
      how: "10 friends you invited play a match",
      earned: c.invitedWhoPlayed >= 10,
      progress: progress(c.invitedWhoPlayed, 10),
    },
    {
      id: "supporter",
      group: "Community",
      icon: "heart",
      title: "Supporter",
      how: "Support the game",
      earned: c.supporter,
    },
    {
      id: "online-debut",
      group: "Community",
      icon: "globe",
      title: "Online debut",
      how: "Play a friend online",
      earned: matches.some((m) => m.online),
    },
  ];
}

/** "formation=4-3-3;orders=…" → "4-3-3". */
export function formationOf(plan: string | null | undefined): string | null {
  const m = /(?:^|;)formation=([^;]+)/.exec(plan ?? "");
  return m?.[1] ?? null;
}
