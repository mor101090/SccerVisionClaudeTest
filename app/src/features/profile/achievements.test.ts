import { describe, expect, it } from "vitest";

import { achievementsOf, formationOf, type MatchFact, recordOf } from "./achievements";

const m = (gf: number, ga: number, extra: Partial<MatchFact> = {}, day = 1): MatchFact => ({
  goalsFor: gf,
  goalsAgainst: ga,
  online: false,
  formation: null,
  players: [],
  playedAt: new Date(2026, 10, day),
  ...extra,
});

const none = { squadComplete: false, invitedWhoPlayed: 0, supporter: false };
const earned = (list: ReturnType<typeof achievementsOf>) =>
  list.filter((a) => a.earned).map((a) => a.id);

describe("manager record", () => {
  it("counts results, goals and the best rating", () => {
    const r = recordOf([
      m(3, 1, { players: [{ rating: 8.7, goals: 2 }] }),
      m(1, 1),
      m(0, 2, { players: [{ rating: 6.1, goals: 0 }] }),
    ]);
    expect(r).toEqual({
      played: 3,
      won: 1,
      drawn: 1,
      lost: 1,
      goalsFor: 4,
      goalsAgainst: 4,
      bestRating: 8.7,
    });
    expect(recordOf([]).bestRating).toBeNull();
  });
});

describe("achievements", () => {
  it("has nothing for a new manager, with progress on the counted ones", () => {
    const list = achievementsOf([], none);
    expect(earned(list)).toEqual([]);
    expect(list.find((a) => a.id === "unbeaten-10")?.progress).toBe("0 / 10");
    expect(list).toHaveLength(12);
  });

  it("earns match, player and community badges from real facts", () => {
    const list = achievementsOf(
      [
        m(5, 0, { formation: "4-4-2", players: [{ rating: 9.1, goals: 3 }] }, 1),
        m(2, 1, { formation: "4-3-3", online: true }, 2),
        m(1, 0, { formation: "3-5-2" }, 3),
      ],
      { squadComplete: true, invitedWhoPlayed: 1, supporter: true },
    );
    expect(earned(list)).toEqual([
      "first-win",
      "clean-sheet",
      "goal-fest",
      "hat-trick",
      "full-squad",
      "tactician",
      "recruiter",
      "supporter",
      "online-debut",
    ]);
  });

  it("counts the longest unbeaten run in date order", () => {
    const run = Array.from({ length: 10 }, (_, i) => m(1, 1, {}, i + 2));
    expect(earned(achievementsOf([m(0, 1, {}, 1), ...run], none))).toContain("unbeaten-10");
    expect(earned(achievementsOf([...run.slice(0, 9), m(0, 1, {}, 30)], none))).not.toContain(
      "unbeaten-10",
    );
  });

  it("reads the formation from a saved plan", () => {
    expect(formationOf("formation=4-2-3-1;orders=0.1.1.1")).toBe("4-2-3-1");
    expect(formationOf(null)).toBeNull();
  });
});
