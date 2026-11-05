import { describe, expect, it } from "vitest";

import {
  deviceLabel,
  passkeyAuthenticatedAt,
  passkeyUsedAt,
  loggedInRecently,
  RECENT_LOGIN_SECONDS,
  OFFER_WINDOW_SECONDS,
  offerDismissedKey,
  passkeyErrorKind,
  passkeyErrorMessage,
  shouldOfferPasskey,
} from "./passkeys";

describe("passkeyErrorKind", () => {
  it("treats a closed prompt or an aborted ceremony as a cancel", () => {
    expect(passkeyErrorKind({ code: "ERROR_CEREMONY_ABORTED" })).toBe("cancelled");
    expect(passkeyErrorKind({ name: "NotAllowedError" })).toBe("cancelled");
    expect(
      passkeyErrorKind({
        code: "ERROR_PASSTHROUGH_SEE_CAUSE_PROPERTY",
        cause: { name: "NotAllowedError" },
      }),
    ).toBe("cancelled");
  });

  it("recognises an expired challenge and a device that already has a passkey", () => {
    expect(passkeyErrorKind({ code: "webauthn_challenge_expired" })).toBe("expired");
    expect(passkeyErrorKind({ code: "ERROR_AUTHENTICATOR_PREVIOUSLY_REGISTERED" })).toBe(
      "already_registered",
    );
  });

  it("falls back to failed", () => {
    expect(passkeyErrorKind(new Error("boom"))).toBe("failed");
    expect(passkeyErrorKind(null)).toBe("failed");
  });
});

describe("passkeyErrorMessage", () => {
  it("stays silent on cancel and explains the rest", () => {
    expect(passkeyErrorMessage({ name: "NotAllowedError" }, "signin")).toBeNull();
    expect(passkeyErrorMessage({ code: "webauthn_challenge_expired" }, "signin")).toContain(
      "Try again",
    );
    expect(passkeyErrorMessage(new Error("x"), "create")).toContain("couldn't create");
  });
});

describe("shouldOfferPasskey", () => {
  const now = 1_800_000_000;
  const fresh = [{ method: "oauth", timestamp: now - 60 }];

  it("offers after a fresh sign-in without a passkey", () => {
    expect(shouldOfferPasskey(fresh, now, false)).toBe(true);
    expect(shouldOfferPasskey([{ method: "password", timestamp: now - 30 }], now, false)).toBe(
      true,
    );
  });

  it("doesn't offer when dismissed, stale, or the sign-in used a passkey", () => {
    expect(shouldOfferPasskey(fresh, now, true)).toBe(false);
    expect(
      shouldOfferPasskey(
        [{ method: "oauth", timestamp: now - OFFER_WINDOW_SECONDS - 1 }],
        now,
        false,
      ),
    ).toBe(false);
    expect(shouldOfferPasskey([{ method: "passkey", timestamp: now - 10 }], now, false)).toBe(
      false,
    );
    expect(shouldOfferPasskey(undefined, now, false)).toBe(false);
  });
});

it("remembers a dismissal per user", () => {
  expect(offerDismissedKey("u1")).not.toBe(offerDismissedKey("u2"));
});

describe("deviceLabel", () => {
  it("names common browsers and systems", () => {
    expect(
      deviceLabel(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36",
      ),
    ).toBe("Chrome on Mac");
    expect(
      deviceLabel(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
      ),
    ).toBe("Safari on iPhone");
    expect(
      deviceLabel(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36 Edg/140.0",
      ),
    ).toBe("Edge on Windows");
    expect(deviceLabel("")).toBe("Browser");
  });
});

describe("loggedInRecently", () => {
  const now = 1_800_000_000;
  it("accepts a login in the last 10 minutes only", () => {
    expect(loggedInRecently([{ method: "password", timestamp: now - 120 }], now)).toBe(true);
    expect(
      loggedInRecently([{ method: "oauth", timestamp: now - RECENT_LOGIN_SECONDS - 1 }], now),
    ).toBe(false);
    expect(loggedInRecently([], now)).toBe(false);
    expect(loggedInRecently(undefined, now)).toBe(false);
  });

  it("uses the newest authentication of the session", () => {
    expect(
      loggedInRecently(
        [
          { method: "password", timestamp: now - 86_400 },
          { method: "oauth", timestamp: now - 30 },
        ],
        now,
      ),
    ).toBe(true);
  });
});

describe("passkeyAuthenticatedAt", () => {
  it("finds a passkey login and ignores other methods", () => {
    expect(passkeyAuthenticatedAt({ amr: [{ method: "passkey", timestamp: 100 }] })).toBe(100);
    expect(passkeyAuthenticatedAt({ amr: [{ method: "oauth", timestamp: 100 }] })).toBeNull();
    expect(
      passkeyAuthenticatedAt({
        amr: [
          { method: "password", timestamp: 50 },
          { method: "passkey", timestamp: 90 },
        ],
      }),
    ).toBe(90);
    expect(passkeyAuthenticatedAt(null)).toBeNull();
  });
});

describe("passkeyUsedAt", () => {
  const at = Date.parse("2026-11-05T20:00:00Z") / 1000;
  it("picks the passkey whose last use matches the login", () => {
    expect(
      passkeyUsedAt(
        [
          { id: "old", last_used_at: "2026-11-01T10:00:00Z" },
          { id: "now", last_used_at: "2026-11-05T20:00:02Z" },
        ],
        at,
      ),
    ).toBe("now");
  });

  it("returns null when no passkey was used around that time", () => {
    expect(passkeyUsedAt([{ id: "old", last_used_at: "2026-11-01T10:00:00Z" }], at)).toBeNull();
    expect(passkeyUsedAt([{ id: "new" }], at)).toBeNull();
  });
});
