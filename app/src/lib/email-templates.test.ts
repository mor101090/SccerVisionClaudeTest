import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

/**
 * The auth email templates (docs/email-templates, pasted into Supabase) can't use CSS variables, so they write colors as
 * hex. Every hex there must be the value of a color token in globals.css: change a token, and this test points at the
 * templates to update.
 */
const root = join(process.cwd(), "..");
const css = readFileSync(join(process.cwd(), "src/app/globals.css"), "utf8");
const tokenValues = new Set(
  [...css.matchAll(/--[\w-]+:\s*(#[0-9a-fA-F]{3,8})\b/g)].map((m) => m[1]!.toLowerCase()),
);
const dir = join(root, "docs/email-templates");
const templates = readdirSync(dir).filter((f) => f.endsWith(".html"));

describe("email templates use the color tokens", () => {
  it("finds the tokens and the templates", () => {
    expect(tokenValues.has("#17062e")).toBe(true);
    expect(templates.length).toBeGreaterThan(0);
  });

  it.each(templates)("%s: every hex color is a token value", (file) => {
    const html = readFileSync(join(dir, file), "utf8");
    const used = [...html.matchAll(/#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b/g)].map((m) =>
      m[0].toLowerCase(),
    );
    expect(used.length).toBeGreaterThan(0);
    expect(used.filter((hex) => !tokenValues.has(hex))).toEqual([]);
  });
});
