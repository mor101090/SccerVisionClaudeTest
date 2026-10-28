// Runs the match engine (C# compiled to WebAssembly, see sim/SoccerVision.Wasm) off the page's thread and streams the
// match to the page as replay data: header, then batches of frames, events and stats.
//
// Messages in:  {cmd:"init", base}            load the engine (base = URL of the engine folder)
//               {cmd:"start", token, mode, seed, weather, maintenance, wind, windAngle, night, lead}
//                                              lead = 0: generate the whole match as fast as possible;
//                                              lead = N: stay at most N frames (N/10 s) ahead of the viewer (live)
//               {cmd:"progress", token, frames}  the viewer has played this many frames (live pacing)
//               {cmd:"stop", token}            drop the current match
// Messages out: {type:"ready"} | {type:"header", token, header} | {type:"chunk", token, len, frames, events, stats, finished}
//               | {type:"error", message}
let L = null, frameLength = 0, token = 0, running = false, again = false, generated = 0, played = 0, lead = 0;

self.onmessage = async (e) => {
  const m = e.data;
  try {
    if (m.cmd === "init") {
      const { dotnet } = await import(m.base + "_framework/dotnet.js");
      const { getAssemblyExports, getConfig } = await dotnet.withDiagnosticTracing(false).create();
      const exports = await getAssemblyExports(getConfig().mainAssemblyName);
      L = exports.LiveEngine;
      frameLength = L.FrameLength();
      postMessage({ type: "ready" });
    } else if (m.cmd === "start") {
      token = m.token; lead = m.lead; generated = 0; played = 0;
      const header = L.Start(m.seed, m.mode, m.weather, m.maintenance, m.wind, m.windAngle, m.night);
      postMessage({ type: "header", token, header });
      pump();
    } else if (m.cmd === "progress") {
      if (m.token === token) { played = m.frames; pump(); }
    } else if (m.cmd === "stop") {
      token = m.token;
    }
  } catch (err) {
    postMessage({ type: "error", message: String((err && err.message) || err) });
  }
};

async function pump() {
  if (running) { again = true; return; }
  running = true;
  try {
    do {
      again = false;
      const mine = token;
      while (mine === token && !L.Finished() && (lead === 0 || generated - played < lead)) {
        const frames = new Int32Array(L.Step(lead === 0 ? 30 : 5)); // a copy: the engine's buffer is reused
        const stats = new Int32Array(L.Stats());
        generated += frames.length / frameLength;
        postMessage({ type: "chunk", token: mine, len: frameLength, frames, events: L.Events(), stats, finished: L.Finished() }, [frames.buffer, stats.buffer]);
        await new Promise((r) => setTimeout(r, 0)); // let progress / stop messages in
      }
    } while (again);
  } catch (err) {
    postMessage({ type: "error", message: String((err && err.message) || err) });
  } finally {
    running = false;
  }
}
