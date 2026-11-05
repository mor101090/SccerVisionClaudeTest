import { describe, expect, it } from "vitest";

import { classifyAuthError } from "./auth-errors";

describe("classifyAuthError", () => {
  it("maps Supabase Auth error codes", () => {
    expect(classifyAuthError({ status: 400, code: "invalid_credentials" })).toBe(
      "invalid_credentials",
    );
    expect(classifyAuthError({ status: 400, code: "email_not_confirmed" })).toBe(
      "email_not_verified",
    );
    expect(classifyAuthError({ status: 422, code: "user_already_exists" })).toBe("user_exists");
    expect(classifyAuthError({ status: 422, code: "email_exists" })).toBe("user_exists");
    expect(classifyAuthError({ status: 403, code: "otp_expired" })).toBe("otp_invalid_or_expired");
    expect(classifyAuthError({ status: 422, code: "weak_password" })).toBe("weak_password");
    expect(classifyAuthError({ status: 422, code: "same_password" })).toBe("same_password");
  });

  it("detects rate limiting by code or status", () => {
    expect(classifyAuthError({ status: 429 })).toBe("rate_limited");
    expect(classifyAuthError({ status: 429, code: "over_email_send_rate_limit" })).toBe(
      "rate_limited",
    );
    expect(classifyAuthError({ code: "over_request_rate_limit" })).toBe("rate_limited");
  });

  it("falls back to unknown", () => {
    expect(classifyAuthError({ status: 500, message: "boom" })).toBe("unknown");
    expect(classifyAuthError(null)).toBe("unknown");
  });
});
