import { describe, expect, it } from "vitest";

import { planTextIsSafe } from "./plan-text";

describe("planTextIsSafe", () => {
  it("accepts a normal plan", () => {
    expect(
      planTextIsSafe(
        "formation=4-3-3;slots=1,2,5,3,4,6,8,10,7,9,11;orders=0.1.1.1,1.0.0.0,2.1.2.1,0.0.0.0,0.0.0.0,0.0.0.0,0.0.0.0,0.0.0.0,0.0.0.0,0.0.0.0,0.0.0.0;line=0.5;passing=1",
      ),
    ).toBe(true);
  });

  it("rejects a numeric field the engine would fail to parse", () => {
    expect(planTextIsSafe("line=high")).toBe(false);
    expect(planTextIsSafe("slots=1,2,5,3,4,6,8,10,7,9,no")).toBe(false);
    expect(planTextIsSafe("cpa=7:wide:1")).toBe(false);
    expect(planTextIsSafe(`line=${"1".repeat(100)}x`)).toBe(false);
    expect(planTextIsSafe("passing=999999999999")).toBe(false);
    expect(planTextIsSafe("orders=0.1..1,0,0,0,0,0,0,0,0,0,0")).toBe(false);
  });
});
