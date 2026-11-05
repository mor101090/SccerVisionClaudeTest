import { describe, expect, it } from "vitest";

import { classifyAuthError } from "./auth-errors";

describe("classifyAuthError", () => {
  it("reads Neon's rewritten 422 by its message", () => {
    expect(
      classifyAuthError({
        status: 422,
        code: "validation_failed",
        message: "User already exists. Use another email.",
      }),
    ).toBe("user_exists");
  });

  it("uses Better Auth codes when they survive", () => {
    expect(classifyAuthError({ status: 401, code: "INVALID_EMAIL_OR_PASSWORD" })).toBe(
      "invalid_credentials",
    );
    expect(classifyAuthError({ status: 403, code: "EMAIL_NOT_VERIFIED" })).toBe(
      "email_not_verified",
    );
    expect(classifyAuthError({ status: 400, message: "OTP expired" })).toBe("otp_expired");
    expect(classifyAuthError({ status: 400, message: "Invalid OTP" })).toBe("invalid_otp");
    expect(classifyAuthError({ status: 403, message: "Too many attempts" })).toBe(
      "too_many_attempts",
    );
    expect(classifyAuthError({ status: 400, message: "Invalid token" })).toBe("invalid_token");
  });

  it("detects rate limiting and unknowns", () => {
    expect(classifyAuthError({ status: 429 })).toBe("rate_limited");
    expect(classifyAuthError({ status: 500, message: "boom" })).toBe("unknown");
    expect(classifyAuthError(null)).toBe("unknown");
  });
});
