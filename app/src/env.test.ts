import { describe, expect, it } from "vitest";

import { blankToUnset } from "./env";

describe("env", () => {
  it("treats empty values as unset (vercel pull writes Sensitive variables as empty)", () => {
    expect(blankToUnset({ A: "", B: "x", C: undefined })).toEqual({
      A: undefined,
      B: "x",
      C: undefined,
    });
  });
});
