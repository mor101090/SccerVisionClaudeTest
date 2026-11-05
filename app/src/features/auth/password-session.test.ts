import { describe, expect, it } from "vitest";

import { sessionIdFromAccessToken, sessionIdOf } from "./password-session";

function token(payload: object): string {
  return `h.${Buffer.from(JSON.stringify(payload)).toString("base64url")}.s`;
}

describe("sessionIdFromAccessToken", () => {
  it("reads the session id and ignores a token without one", () => {
    expect(sessionIdFromAccessToken(token({ session_id: "sess-1" }))).toBe("sess-1");
    expect(sessionIdFromAccessToken(token({ sub: "user" }))).toBeNull();
    expect(sessionIdFromAccessToken("not-a-jwt")).toBeNull();
  });
});

describe("sessionIdOf", () => {
  it("reads the claim", () => {
    expect(sessionIdOf({ session_id: "sess-1" })).toBe("sess-1");
    expect(sessionIdOf({})).toBeNull();
    expect(sessionIdOf(null)).toBeNull();
  });
});
