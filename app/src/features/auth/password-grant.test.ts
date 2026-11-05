import { describe, expect, it } from "vitest";

import { isPasswordGrant } from "./password-grant";

describe("isPasswordGrant", () => {
  it("matches the password token endpoint", () => {
    expect(isPasswordGrant("https://example.supabase.co/auth/v1/token?grant_type=password")).toBe(
      true,
    );
    expect(
      isPasswordGrant(
        new Request("https://example.supabase.co/auth/v1/token?grant_type=password", {
          method: "POST",
        }),
      ),
    ).toBe(true);
  });

  it("leaves refresh, OAuth and other auth calls alone", () => {
    expect(
      isPasswordGrant("https://example.supabase.co/auth/v1/token?grant_type=refresh_token"),
    ).toBe(false);
    expect(isPasswordGrant("https://example.supabase.co/auth/v1/signup")).toBe(false);
    expect(isPasswordGrant("https://example.supabase.co/auth/v1/authorize")).toBe(false);
    expect(isPasswordGrant("not a url")).toBe(false);
  });
});
