// Runs the match engine (C# compiled to WebAssembly, see sim/SoccerVision.Wasm) off the page's thread and streams the
// match to the page as replay data: header, then batches of frames, events and stats.
//
// Messages in:  {cmd:"init", base}            load the engine (base = URL of the engine folder)
//               {cmd:"start", token, mode, seed, weather, maintenance, wind, windAngle, night, lead, plan, hold}
//                                              lead = 0: generate the whole match as fast as possible;
//                                              lead = N: stay at most N frames (N/10 s) ahead of the viewer (live)
//                                              plan: the home manager's plan as text (see TeamPlan.cs)
//                                              hold: send the header only, play nothing until "go" (pre-match)
//               {cmd:"go", token}              kick off a held match
//               {cmd:"progress", token, frames}  the viewer has played this many frames (live pacing)
//               {cmd:"plan", token, plan}      the home manager's new plan during the match (live)
//               {cmd:"sub", token, out, in}    the home manager asks for a change (shirt numbers; live)
//               {cmd:"stop", token}            drop the current match
// Messages out: {type:"ready"} | {type:"header", token, header} | {type:"chunk", token, len, frames, events, stats, finished}
//               | {type:"error", message}
let L = null, frameLength = 0, token = 0, running = false, again = false, generated = 0, played = 0, lead = 0, held = false;

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
      token = m.token; lead = m.lead; generated = 0; played = 0; held = !!m.hold;
      const header = L.Start(m.seed, m.mode, m.weather, m.maintenance, m.wind, m.windAngle, m.night, m.plan || "");
      postMessage({ type: "header", token, header });
      if (!held) pump();
    } else if (m.cmd === "go") {
      if (m.token === token && held) { held = false; pump(); }
    } else if (m.cmd === "progress") {
      if (m.token === token) { played = m.frames; if (!held) pump(); }
    } else if (m.cmd === "plan") {
      if (m.token === token) L.SetPlan(m.plan || "");
    } else if (m.cmd === "sub") {
      if (m.token === token) L.Substitute(m.out | 0, m.in | 0);
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
