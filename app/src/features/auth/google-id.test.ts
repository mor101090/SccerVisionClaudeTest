import { beforeEach, describe, expect, it } from "vitest";

import {
  CONFIRM_TTL_MS,
  googleAuthorizeURL,
  readGoogleConfirm,
  saveGoogleConfirm,
  parseGoogleReturn,
  savePending,
  sha256Hex,
  takePending,
} from "./google-id";

describe("googleAuthorizeURL", () => {
  it("asks Google for an ID token that returns to this site, bound to the hashed nonce and state", () => {
    const url = new URL(
      googleAuthorizeURL({
        clientId: "123-abc.apps.googleusercontent.com",
        origin: "https://soccer.dev-spirit.com",
        state: "s1",
        hashedNonce: "h1",
      }),
    );
    expect(url.origin + url.pathname).toBe("https://accounts.google.com/o/oauth2/v2/auth");
    expect(Object.fromEntries(url.searchParams)).toMatchObject({
      client_id: "123-abc.apps.googleusercontent.com",
      redirect_uri: "https://soccer.dev-spirit.com/auth/google",
      response_type: "id_token",
      scope: "openid email profile",
      nonce: "h1",
      state: "s1",
    });
  });
});

describe("parseGoogleReturn", () => {
  it("reads the token and state from the fragment", () => {
    expect(parseGoogleReturn("#state=s1&id_token=t.o.k&authuser=0")).toEqual({
      ok: true,
      idToken: "t.o.k",
      state: "s1",
    });
  });

  it("reports Google's error, or a missing token", () => {
    expect(parseGoogleReturn("#error=access_denied&state=s1")).toEqual({
      ok: false,
      error: "access_denied",
      state: "s1",
    });
    expect(parseGoogleReturn("")).toEqual({ ok: false, error: "missing_token", state: null });
  });
});

it("sha256Hex matches the known digest", async () => {
  expect(await sha256Hex("abc")).toBe(
    "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
  );
});

describe("pending sign-in", () => {
  beforeEach(() => sessionStorage.clear());

  it("can be taken once", () => {
    savePending({ state: "s", nonce: "n", mode: "link", redirectTo: "/settings" });
    expect(takePending()).toEqual({
      state: "s",
      nonce: "n",
      mode: "link",
      redirectTo: "/settings",
    });
    expect(takePending()).toBeNull();
  });

  it("rejects a tampered entry", () => {
    sessionStorage.setItem("sv_google_pending", JSON.stringify({ state: "s", mode: "admin" }));
    expect(takePending()).toBeNull();
  });
});

describe("waiting Google confirm", () => {
  beforeEach(() => sessionStorage.clear());
  const c = {
    token: "t",
    nonce: "n",
    redirectTo: "/play",
    email: "a@b.c",
    account: { username: "Idan", clubName: null, signedUpWith: "Facebook" },
  };

  it("is read back while fresh", () => {
    saveGoogleConfirm(c);
    expect(readGoogleConfirm()).toMatchObject(c);
  });

  it("expires and is then removed", () => {
    saveGoogleConfirm(c);
    expect(readGoogleConfirm(Date.now() + CONFIRM_TTL_MS + 1)).toBeNull();
    expect(sessionStorage.getItem("sv_google_confirm")).toBeNull();
  });

  it("drops a malformed entry", () => {
    sessionStorage.setItem("sv_google_confirm", '{"token":1}');
    expect(readGoogleConfirm()).toBeNull();
  });
});
