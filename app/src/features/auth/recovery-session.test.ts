import { describe, expect, it } from "vitest";

import { isPasswordSession, isRecoverySession } from "./recovery-session";

const NOW = 1_700_000_000;

describe("isRecoverySession", () => {
  it("accepts a fresh recovery OTP session", () => {
    expect(isRecoverySession({ amr: [{ method: "otp", timestamp: NOW - 30 }] }, NOW)).toBe(true);
    expect(isRecoverySession({ amr: [{ method: "magiclink", timestamp: NOW }] }, NOW)).toBe(true);
  });

  it("rejects ordinary logins, stale recovery sessions and empty claims", () => {
    expect(isRecoverySession({ amr: [{ method: "password", timestamp: NOW }] }, NOW)).toBe(false);
    expect(isRecoverySession({ amr: [{ method: "oauth", timestamp: NOW }] }, NOW)).toBe(false);
    expect(
      isRecoverySession(
        {
          amr: [
            { method: "otp", timestamp: NOW },
            { method: "password", timestamp: NOW },
          ],
        },
        NOW,
      ),
    ).toBe(false);
    expect(isRecoverySession({ amr: [{ method: "otp", timestamp: NOW - 16 * 60 }] }, NOW)).toBe(
      false,
    );
    expect(isRecoverySession({ amr: [] }, NOW)).toBe(false);
    expect(isRecoverySession(null, NOW)).toBe(false);
  });
});

describe("isPasswordSession", () => {
  it("is only a password grant", () => {
    expect(isPasswordSession({ amr: [{ method: "password", timestamp: NOW }] })).toBe(true);
    expect(isPasswordSession({ amr: [{ method: "oauth", timestamp: NOW }] })).toBe(false);
    expect(isPasswordSession({ amr: [{ method: "otp", timestamp: NOW }] })).toBe(false);
    expect(isPasswordSession(null)).toBe(false);
  });
});
