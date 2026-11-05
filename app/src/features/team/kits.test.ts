import { describe, expect, it } from "vitest";

import { DEFAULT_KITS, isBlocked, kitsValid } from "./kits";

describe("kit clash rules (Figma: Your kits)", () => {
  it("greys out the home shirt colours too close to the away shirt", () => {
    for (const c of ["blue", "sky", "purple", "cyan"])
      expect(isBlocked(DEFAULT_KITS, "home", "shirt", c)).toBe(true);
    for (const c of ["red", "white", "yellow", "navy"])
      expect(isBlocked(DEFAULT_KITS, "home", "shirt", c)).toBe(false);
  });

  it("greys out keeper shirts close to either outfield shirt", () => {
    for (const c of ["red", "orange", "maroon", "blue", "sky"])
      expect(isBlocked(DEFAULT_KITS, "keeper", "shirt", c)).toBe(true);
    expect(isBlocked(DEFAULT_KITS, "keeper", "shirt", "yellow")).toBe(false);
  });

  it("greys out numbers that would not read on the shirt", () => {
    expect(isBlocked(DEFAULT_KITS, "home", "number", "red")).toBe(true);
    expect(isBlocked(DEFAULT_KITS, "keeper", "number", "orange")).toBe(true);
    expect(isBlocked(DEFAULT_KITS, "home", "number", "white")).toBe(false);
  });

  it("never blocks trim or socks, and accepts the defaults", () => {
    expect(isBlocked(DEFAULT_KITS, "home", "trim", "red")).toBe(false);
    expect(kitsValid(DEFAULT_KITS)).toBe(true);
    expect(kitsValid({ ...DEFAULT_KITS, away: { ...DEFAULT_KITS.away, shirt: "red" } })).toBe(
      false,
    );
  });
});

describe("repairKits", () => {
  it("moves the keeper off a shirt colour the home kit just took, and fixes unreadable numbers", async () => {
    const { repairKits } = await import("./kits");
    const r = repairKits({
      ...DEFAULT_KITS,
      home: { ...DEFAULT_KITS.home, shirt: "yellow", number: "yellow" },
    });
    expect(r.keeper.shirt).not.toBe("yellow");
    expect(r.home.number).toBe("white");
    expect(kitsValid(r)).toBe(true);
  });
});
