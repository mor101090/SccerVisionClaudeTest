import { describe, expect, it } from "vitest";

import { flagUrl, guessCountry, NOT_A_NATIONALITY } from "./countries";

const list = [
  { code: "IL", name: "Israel" },
  { code: "US", name: "United States" },
  { code: "GB-ENG", name: "England" },
];

describe("guessCountry", () => {
  it("prefers the IP country", () => {
    expect(guessCountry("il", "en-US,en;q=0.9", list)).toBe("IL");
  });
  it("falls back to the browser language's region", () => {
    expect(guessCountry(null, "en-US,en;q=0.9", list)).toBe("US");
    expect(guessCountry(undefined, "he-IL", list)).toBe("IL");
  });
  it("ignores codes that are not in the list (e.g. Vercel's XX, a language without region)", () => {
    expect(guessCountry("XX", "he", list)).toBeNull();
    expect(guessCountry("PS", null, list)).toBeNull();
  });
});

describe("country data", () => {
  it("never offers Palestinian Territories", () => {
    expect(NOT_A_NATIONALITY.has("PS")).toBe(true);
  });
  it("builds flagcdn URLs, also for the home nations", () => {
    expect(flagUrl("IL")).toBe("https://flagcdn.com/il.svg");
    expect(flagUrl("GB-ENG")).toBe("https://flagcdn.com/gb-eng.svg");
  });
});
