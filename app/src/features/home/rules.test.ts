import { describe, expect, it } from "vitest";

import {
  badge,
  countdown,
  euros,
  greeting,
  initials,
  moodFromResults,
  pointsShare,
  resultOf,
  subtitle,
  summarize,
} from "./rules";

describe("home rules", () => {
  it("greets by local hour", () => {
    expect(greeting(4)).toBe("Good evening");
    expect(greeting(5)).toBe("Good morning");
    expect(greeting(12)).toBe("Good afternoon");
    expect(greeting(18)).toBe("Good evening");
  });

  it("summarizes results and averages only what exists", () => {
    const s = summarize([
      { goalsFor: 2, goalsAgainst: 1, possession: 0.6 },
      { goalsFor: 1, goalsAgainst: 1, possession: null },
      { goalsFor: 0, goalsAgainst: 3, possession: 0.4 },
    ]);
    expect(s).toEqual({ games: 3, won: 1, drawn: 1, lost: 1, goalsPerGame: 1, possession: 0.5 });
    expect(pointsShare(s)).toBeCloseTo(4 / 9);
    expect(summarize([])).toEqual({
      games: 0,
      won: 0,
      drawn: 0,
      lost: 0,
      goalsPerGame: null,
      possession: null,
    });
    expect(resultOf({ goalsFor: 0, goalsAgainst: 0 })).toBe("D");
  });

  it("derives fan mood from recent results, hopeful with none", () => {
    expect(moodFromResults([])).toBe(3);
    expect(moodFromResults(["W", "W", "W", "W", "D"])).toBe(5);
    expect(moodFromResults(["L", "L", "L", "L", "L"])).toBe(1);
    expect(moodFromResults(["W", "D", "L"])).toBe(3);
  });

  it("counts down to kick-off and stops once it starts", () => {
    const now = new Date("2026-01-14T16:24:00Z");
    expect(countdown(new Date("2026-01-16T19:00:00Z"), now)).toEqual({
      days: 2,
      hours: 2,
      minutes: 36,
    });
    expect(countdown(now, now)).toBeNull();
  });

  it("picks the subtitle by priority", () => {
    const base = {
      liveFixture: false,
      hoursToNext: null,
      lineupConfirmed: true,
      pendingOffers: 0,
      seasonLabel: null,
      matchesPlayed: 0,
    };
    expect(subtitle(base)).toMatch(/Play a friendly/);
    expect(subtitle({ ...base, matchesPlayed: 4 })).toMatch(/Leagues are coming soon/);
    expect(subtitle({ ...base, pendingOffers: 2 })).toBe(
      "2 transfer offers waiting for your answer.",
    );
    expect(
      subtitle({ ...base, hoursToNext: 20, lineupConfirmed: false, pendingOffers: 2 }),
    ).toMatch(/lineup is not confirmed/);
    expect(subtitle({ ...base, liveFixture: true, hoursToNext: 1 })).toMatch(/on the pitch/);
  });

  it("formats initials, badges and money", () => {
    expect(initials("redcoach")).toBe("RE");
    expect(initials("Red Coach")).toBe("RC");
    expect(initials("red_coach")).toBe("RC");
    expect(badge(0)).toBeNull();
    expect(badge(3)).toBe("3");
    expect(badge(12)).toBe("9+");
    expect(euros(673000)).toBe("€673,000");
    expect(euros(-14100)).toBe("−€14,100");
  });
});
