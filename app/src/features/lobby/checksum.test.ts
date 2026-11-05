import { describe, expect, it } from "vitest";

import { framesChecksum } from "./checksum";

describe("framesChecksum", () => {
  it("is stable and sensitive to any change", () => {
    const a = [
      [0, 1, 2],
      [10, -5, 300000],
    ];
    expect(framesChecksum(a)).toBe(framesChecksum(structuredClone(a)));
    const b = structuredClone(a);
    b[1]![2] = 300001;
    expect(framesChecksum(b)).not.toBe(framesChecksum(a));
  });
});
