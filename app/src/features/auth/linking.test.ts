import { describe, expect, it } from "vitest";

import {
  canDisconnect,
  justAutoLinked,
  linkNotice,
  linkReturnPath,
  signedInWith,
  signUpMethod,
  unlinkNotice,
} from "./linking";

describe("linkNotice", () => {
  it("is silent without a Connect round trip or for an unknown provider", () => {
    expect(linkNotice({}, ["google"])).toBeNull();
    expect(linkNotice({ linking: "github" }, ["github"])).toBeNull();
  });

  it("reports success only when the account really has the identity", () => {
    expect(linkNotice({ linking: "google" }, ["email", "google"])?.tone).toBe("success");
    // Back without an error, but nothing was linked (cancelled, error in the fragment): not a success.
    expect(linkNotice({ linking: "google" }, ["email"])).toEqual({
      tone: "error",
      text: "We couldn't connect Google. Try again in a moment.",
    });
  });

  it("explains an identity that belongs to another account", () => {
    const n = linkNotice({ linking: "facebook", linkError: "identity_already_exists" }, ["email"]);
    expect(n?.tone).toBe("error");
    expect(n?.text).toContain("already used by another Soccer Vision account");
  });

  it("an error wins over an identity that was already there", () => {
    expect(linkNotice({ linking: "discord", linkError: "access_denied" }, ["discord"])?.tone).toBe(
      "error",
    );
  });
});

describe("unlinkNotice", () => {
  it("maps the Server Action's outcomes", () => {
    expect(unlinkNotice({ unlinked: "discord" })?.text).toBe("Discord is disconnected.");
    expect(unlinkNotice({ unlinkError: "last" })?.text).toContain("only way to log in");
    expect(unlinkNotice({ unlinkError: "failed" })?.tone).toBe("error");
    expect(unlinkNotice({ unlinked: "<script>" })).toBeNull();
  });
});

describe("signedInWith", () => {
  it("picks the identity used most recently, not the first one", () => {
    const r = signedInWith([
      { provider: "facebook", last_sign_in_at: "2026-11-05T18:00:00Z" },
      { provider: "google", last_sign_in_at: "2026-11-05T19:30:00Z" },
    ]);
    expect(r).toEqual({ current: "google", others: ["facebook"] });
  });

  it("ignores email/password and handles accounts without a social identity", () => {
    expect(signedInWith([{ provider: "email", last_sign_in_at: "2026-11-05T20:00:00Z" }])).toEqual({
      current: null,
      others: [],
    });
    expect(
      signedInWith([
        { provider: "email", last_sign_in_at: "2026-11-05T20:00:00Z" },
        { provider: "discord", last_sign_in_at: null },
      ]).current,
    ).toBe("discord");
  });
});

describe("canDisconnect", () => {
  it("keeps at least one way to sign in", () => {
    expect(canDisconnect(1)).toBe(false);
    expect(canDisconnect(2)).toBe(true);
  });
});

it("Connect returns to the settings page", () => {
  expect(linkReturnPath("google")).toBe("/settings?linking=google");
});

describe("justAutoLinked", () => {
  const now = Date.parse("2026-11-05T20:00:00Z");
  const fb = {
    identity_id: "fb-1",
    provider: "facebook",
    created_at: "2026-10-01T10:00:00Z",
    last_sign_in_at: "2026-10-01T10:00:00Z",
  };
  const google = {
    identity_id: "g-1",
    provider: "google",
    created_at: "2026-11-05T19:58:00Z",
    last_sign_in_at: "2026-11-05T19:58:00Z",
  };

  it("finds the identity this sign-in just merged into an older account", () => {
    expect(justAutoLinked([fb, google], now)).toEqual({ provider: "google", identityId: "g-1" });
  });

  it("ignores a returning sign-in, a new account, and an old link", () => {
    const returning = { ...fb, last_sign_in_at: "2026-11-05T19:59:00Z" };
    expect(justAutoLinked([returning, google], now)).toBeNull();
    expect(justAutoLinked([google], now)).toBeNull();
    expect(justAutoLinked([fb, google], now + 11 * 60_000)).toBeNull();
  });

  it("an email/password sign-in is never treated as a merge to confirm", () => {
    const email = { ...google, identity_id: "e-1", provider: "email" };
    expect(justAutoLinked([fb, email], now)).toBeNull();
  });
});

it("signUpMethod names the oldest identity", () => {
  expect(
    signUpMethod([
      { provider: "google", created_at: "2026-11-05T19:58:00Z" },
      { provider: "email", created_at: "2026-01-01T00:00:00Z" },
    ]),
  ).toBe("email and password");
  expect(signUpMethod([{ provider: "discord", created_at: "2026-01-01T00:00:00Z" }])).toBe(
    "Discord",
  );
});
