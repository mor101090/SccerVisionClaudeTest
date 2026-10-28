import { spawn } from "node:child_process";
import { readFile, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";

// Dev-only endpoint: GET /api/new-replay?seed=123 runs the C# engine (needs the .NET 8 SDK) and returns the
// recorded match as JSON, so the replay viewer can offer a "New match" button.
function newReplayApi(): Plugin {
  return {
    name: "soccervision-new-replay",
    configureServer(server) {
      server.middlewares.use("/api/new-replay", async (req, res) => {
        const url = new URL(req.url ?? "", "http://localhost");
        const seed = /^\d{1,10}$/.test(url.searchParams.get("seed") ?? "") ? url.searchParams.get("seed")! : String(Date.now() % 2147483647);
        const dir = await mkdtemp(join(tmpdir(), "sv-replay-"));
        const out = join(dir, "replay.json");
        const project = resolve(process.cwd(), "../sim/SoccerVision.SimRunner");
        // weather and pitch: ?weather=sunny|rain|snow&maintenance=0..1&wind=m/s&windangle=degrees
        const extra: string[] = [];
        const weather = url.searchParams.get("weather");
        if (weather && /^(sunny|cloudy|rain|snow)$/.test(weather)) extra.push("--weather", weather);
        if (url.searchParams.get("script") === "staged") extra.push("--script", "staged");
        for (const key of ["maintenance", "wind", "windangle"]) {
          const v = url.searchParams.get(key);
          if (v && /^\d{1,3}(\.\d{1,3})?$/.test(v)) extra.push("--" + key, v);
        }
        const child = spawn("dotnet", ["run", "-c", "Release", "--project", project, "--", "--replay", out, "--seed", seed, "--fps", "8", ...extra], { stdio: "ignore" });
        child.on("error", () => {
          res.statusCode = 500;
          res.end("dotnet SDK not found");
        });
        child.on("exit", async (code) => {
          try {
            if (code !== 0) throw new Error("engine failed");
            const body = await readFile(out);
            res.setHeader("Content-Type", "application/json");
            res.end(body);
          } catch {
            res.statusCode = 500;
            res.end("engine failed");
          } finally {
            rm(dir, { recursive: true, force: true }).catch(() => undefined);
          }
        });
      });
    },
  };
}

// The Unity WebGL build is copied to public/unity/SoccerVision (Unity menu: SoccerVision > Build WebGL for React).
export default defineConfig({
  plugins: [react(), newReplayApi()],
  server: { port: 5173 },
});
