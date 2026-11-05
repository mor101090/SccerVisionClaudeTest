import { describe, expect, it } from "vitest";

import {
  fieldErrors,
  finishSchema,
  passwordStrength,
  safeRedirect,
  signUpSchema,
  verifySchema,
} from "./policy";

const valid = {
  email: " Coach@RedRovers.club ",
  password: "kickoff-2026",
  confirmPassword: "kickoff-2026",
  terms: "on",
};

describe("signUpSchema", () => {
  it("normalizes the email and accepts a valid form", () => {
    const r = signUpSchema.safeParse(valid);
    expect(r.success && r.data.email).toBe("coach@redrovers.club");
  });

  it("reports each field: mismatch, weak password, terms", () => {
    const r = signUpSchema.safeParse({
      ...valid,
      password: "letters",
      confirmPassword: "x",
      terms: undefined,
    });
    expect(r.success).toBe(false);
    const errors = fieldErrors(r.error!);
    expect(errors.password).toBe("At least 8 characters.");
    expect(errors.terms).toBe("Please accept the Terms and Conditions to continue.");
    expect(errors.confirmPassword).toBe("The passwords don't match.");
  });

  it("requires a number or a symbol", () => {
    const r = signUpSchema.safeParse({
      ...valid,
      password: "onlyletters",
      confirmPassword: "onlyletters",
    });
    expect(fieldErrors(r.error!).password).toBe("Add a number or a symbol.");
  });

  it("flags mismatched passwords on the confirm field", () => {
    const r = signUpSchema.safeParse({ ...valid, confirmPassword: "kickoff-2027" });
    expect(fieldErrors(r.error!).confirmPassword).toBe("The passwords don't match.");
  });
});

describe("other schemas", () => {
  it("accepts only 6-digit codes", () => {
    expect(verifySchema.safeParse({ email: "a@b.co", otp: "123456" }).success).toBe(true);
    expect(verifySchema.safeParse({ email: "a@b.co", otp: "12345a" }).success).toBe(false);
  });

  it("checks usernames", () => {
    expect(finishSchema.safeParse({ username: "red_coach1" }).success).toBe(true);
    expect(finishSchema.safeParse({ username: "rc" }).success).toBe(false);
    expect(finishSchema.safeParse({ username: "red coach" }).success).toBe(false);
  });
});

describe("passwordStrength", () => {
  it("scores from Weak to Strong", () => {
    expect(passwordStrength("").label).toBe("");
    expect(passwordStrength("abc").label).toBe("Weak");
    expect(passwordStrength("abcdefgh").label).toBe("Weak");
    expect(passwordStrength("abcdefg1").label).toBe("Fair");
    expect(passwordStrength("abcdefghij1!").label).toBe("Good");
    expect(passwordStrength("Abcdefghij1!").label).toBe("Strong");
  });
});

describe("safeRedirect", () => {
  it("allows only same-site paths", () => {
    expect(safeRedirect("/matches")).toBe("/matches");
    expect(safeRedirect("//evil.com")).toBe("/play");
    expect(safeRedirect("/\\evil.com")).toBe("/play");
    expect(safeRedirect("https://evil.com")).toBe("/play");
    expect(safeRedirect(null)).toBe("/play");
  });

  it("refuses paths a browser turns into another site", () => {
    expect(safeRedirect("/\t/evil.com")).toBe("/play");
    expect(safeRedirect("/\n/evil.com")).toBe("/play");
    expect(safeRedirect("/\r\n/evil.com")).toBe("/play");
    expect(safeRedirect("/a\\b")).toBe("/play");
    expect(safeRedirect("/.//evil.com")).toBe("/play");
    expect(safeRedirect("/a/..//evil.com")).toBe("/play");
    expect(safeRedirect("/%2e//evil.com")).toBe("/play");
    expect(safeRedirect("/%2F%2Fevil.com")).toBe("/%2F%2Fevil.com"); // stays a path here
  });

  it("keeps the query and hash of a same-site path", () => {
    expect(safeRedirect("/lobby/abc?x=1#top")).toBe("/lobby/abc?x=1#top");
  });
});
