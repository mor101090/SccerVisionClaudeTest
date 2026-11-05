/* eslint-disable no-console -- CLI script: progress goes to stdout. */
/**
 * Runs first in `yarn dev` (Yarn 4 has no pre-scripts): builds the engine WASM and the classic page when they are missing or
 * their sources changed since the last build, so a fresh checkout or an engine change just works.
 *
 * Each build stores a hash of its inputs next to its output (`public/engine/.source-hash`,
 * `public/classic/.source-hash`). Same hash → nothing to do (a few ms). A build with no hash file (made by hand with
 * `yarn engine:build` / `classic:build`, or by CI) is taken as current and adopted, so it is not built twice. A missing .NET 8 SDK or a failed build only
 * warns: the dev server still starts, and only the screens that need the engine show "Engine not built".
 *
 *   node tools/ensure-assets.ts            (what `yarn dev` runs first)
 *   SKIP_ASSETS=1 yarn dev                 (skip the check)
 */
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { join, relative, resolve } from "node:path";

const app = resolve(import.meta.dirname, "..");
const root = resolve(app, "..");

const SKIP_DIRS = new Set(["bin", "obj", "node_modules", ".git"]);

function files(path: string, skip: (rel: string) => boolean = () => false): string[] {
  if (!existsSync(path)) return [];
  if (statSync(path).isFile()) return [path];
  return readdirSync(path, { withFileTypes: true })
    .filter((e) => !SKIP_DIRS.has(e.name) && !skip(relative(root, join(path, e.name))))
    .flatMap((e) => files(join(path, e.name), skip));
}

/** Hash of every input file's path + content (sorted, so order on disk does not matter). */
function hashOf(paths: string[], extra = ""): string {
  const h = createHash("sha1").update(extra);
  for (const f of paths.sort())
    h.update(relative(root, f)).update("\0").update(readFileSync(f)).update("\0");
  return h.digest("hex").slice(0, 16);
}

/** True when the build in `dir` is current. A build with no hash file yet is adopted (its hash is written). */
function current(dir: string, built: boolean, hash: string): boolean {
  if (!built) return false;
  const p = join(dir, ".source-hash");
  if (!existsSync(p)) {
    writeFileSync(p, hash + "\n");
    return true;
  }
  return readFileSync(p, "utf8").trim() === hash;
}

function hasDotnet(): boolean {
  const candidates = [process.env.DOTNET, "dotnet", join(homedir(), ".dotnet", "dotnet")].filter(
    Boolean,
  );
  return candidates.some((bin) => spawnSync(bin!, ["--version"], { stdio: "ignore" }).status === 0);
}

function run(script: string): boolean {
  return (
    spawnSync("bash", [join(app, "scripts", script)], { stdio: "inherit", cwd: app }).status === 0
  );
}

const warn = (msg: string) => console.warn(`\x1b[33m[assets] ${msg}\x1b[0m`);

if (process.env.SKIP_ASSETS) process.exit(0);

// ------------------------------------------------------------------ engine
const engineDir = join(app, "public", "engine");
const engineManifest = join(engineDir, "current.json");
const engineInputs = [
  ...files(join(root, "sim"), (rel) => /\.Tests$|SimRunner$/.test(rel)),
  ...files(join(root, "unity", "SoccerVision", "Assets", "SoccerVision", "Simulation"), (rel) =>
    rel.endsWith(".meta"),
  ),
  join(root, "tools", "demo-page", "engine-worker.js"),
  join(app, "scripts", "build-engine.sh"),
];
const engineHash = hashOf(engineInputs);
let engineOk = existsSync(engineManifest);

if (!current(engineDir, engineOk, engineHash)) {
  if (!hasDotnet()) {
    warn(
      engineOk
        ? "engine sources changed but the .NET 8 SDK is not installed: using the old engine build."
        : "engine not built and the .NET 8 SDK is not installed: match screens will show 'Engine not built'. Install .NET 8, then `yarn engine:build`.",
    );
  } else {
    console.log(
      `[assets] engine ${engineOk ? "sources changed" : "missing"}: building (about a minute)...`,
    );
    if (run("build-engine.sh")) {
      writeFileSync(join(engineDir, ".source-hash"), engineHash + "\n");
      engineOk = true;
    } else warn("engine build failed (see above). Starting the dev server anyway.");
  }
}

// ------------------------------------------------------------------ classic page
const classicDir = join(app, "public", "classic");
if (engineOk) {
  const classicInputs = [
    ...files(join(root, "tools", "demo-page")),
    ...files(join(app, "src", "engine", "audio")),
    ...files(join(app, "src", "engine", "replay")),
    ...files(join(root, "docs", "sounds")),
    ...files(join(root, "docs", "faces")),
    join(app, "scripts", "build-classic.sh"),
  ];
  // The page embeds the engine's folder, so a new engine build means a new page too.
  const classicHash = hashOf(classicInputs, readFileSync(engineManifest, "utf8"));
  if (!current(classicDir, existsSync(join(classicDir, "index.html")), classicHash)) {
    console.log("[assets] classic page missing or out of date: building...");
    if (run("build-classic.sh"))
      writeFileSync(join(classicDir, ".source-hash"), classicHash + "\n");
    else warn("classic page build failed (see above). Starting the dev server anyway.");
  }
} else if (!existsSync(join(classicDir, "index.html"))) {
  warn(
    "classic page not built (needs the engine first). `/` redirects to it, so use /home locally.",
  );
}
