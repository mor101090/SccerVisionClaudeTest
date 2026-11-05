import { readFileSync } from "node:fs";
import { resolve } from "node:path";

import { describe, expect, it } from "vitest";

/**
 * app/src/engine holds copies of web/src renderer + audio code (web/ is not deployed but is still
 * edited on main). This fails when the two drift, so a rebase that changes web/src shows up in CI.
 * To fix: copy the file from web/src again (keep the import-path rewrite below).
 */
const root = resolve(__dirname, "../../..");
const COPIES: [app: string, web: string][] = [
  ["app/src/engine/replay/replay.ts", "web/src/replay/replay.ts"],
  ["app/src/engine/replay/drawPitch.ts", "web/src/replay/drawPitch.ts"],
  ["app/src/engine/replay/ball3d.ts", "web/src/replay/ball3d.ts"],
  ["app/src/engine/replay/conditions.ts", "web/src/replay/conditions.ts"],
  ["app/src/engine/replay/textures.ts", "web/src/replay/textures.ts"],
  ["app/src/engine/replay/settings.ts", "web/src/settings/devSettings.ts"],
  ["app/src/engine/audio/matchAudio.ts", "web/src/audio/matchAudio.ts"],
  ["app/src/engine/audio/crowdReactions.ts", "web/src/audio/crowdReactions.ts"],
];

/** Formatting and the one import path that differs are not drift. */
function normalize(src: string): string {
  return src
    .replace(/"\.\.\/settings\/devSettings"/g, '"./settings"')
    .replace(/\s+/g, "")
    .replace(/,([)\]}])/g, "$1");
}

describe("engine copies are in sync with web/src", () => {
  it.each(COPIES)("%s", (app, web) => {
    const a = normalize(readFileSync(resolve(root, app), "utf8"));
    const w = normalize(readFileSync(resolve(root, web), "utf8"));
    expect(a === w, `${app} differs from ${web}: copy it again`).toBe(true);
  });
});
