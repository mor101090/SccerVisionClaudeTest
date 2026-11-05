import { describe, expect, it } from "vitest";

import {
  isRecoverySession,
  passwordAuthenticatedAt,
  sessionStartedDuringPause,
} from "./recovery-session";

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

describe("sessionStartedDuringPause", () => {
  const start = new Date(NOW * 1000);

  it("is a password session issued once the pause had started", () => {
    const claims = { amr: [{ method: "password", timestamp: NOW + 5 }] };
    expect(sessionStartedDuringPause(passwordAuthenticatedAt(claims), start, NOW + 10)).toBe(true);
  });

  it("leaves an older password session and a social session signed in", () => {
    const older = { amr: [{ method: "password", timestamp: NOW - 60 }] };
    const social = { amr: [{ method: "oauth", timestamp: NOW + 5 }] };
    expect(sessionStartedDuringPause(passwordAuthenticatedAt(older), start, NOW + 10)).toBe(false);
    expect(sessionStartedDuringPause(passwordAuthenticatedAt(social), start, NOW + 10)).toBe(false);
    expect(sessionStartedDuringPause(passwordAuthenticatedAt(older), null, NOW)).toBe(false);
  });
});
