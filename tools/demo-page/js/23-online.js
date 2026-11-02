  // ---- online: two friends, one match --------------------------------------------------------------------------------
  // The host's browser runs the engine; the friend's receives the match as it is played and sends his instructions and
  // changes back. The two connect directly (WebRTC): a public STUN server only tells each browser its own internet
  // address - no game data passes through it. The handshake goes by hand: the invite link carries the host's offer, the
  // friend sends back a reply code. The friend sees his own team as the home team: the data is turned round on his side.
  var ONLINE = null;
  var NET = (function () {
    var pc = null, dc = null, guestReady = false, readyWaiters = [], parts = {}, onOpen = null;
    var RTC = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }, { urls: "stun:stun1.l.google.com:19302" }] };
    function b64(bytes) { var s = ""; for (var i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]); return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, ""); }
    function unb64(t) { t = t.replace(/-/g, "+").replace(/_/g, "/"); while (t.length % 4) t += "="; var s = atob(t), a = new Uint8Array(s.length); for (var i = 0; i < s.length; i++) a[i] = s.charCodeAt(i); return a; }
    function pack(obj) {
      var data = new TextEncoder().encode(JSON.stringify(obj));
      if (!window.CompressionStream) return Promise.resolve("j" + b64(data));
      return new Response(new Blob([data]).stream().pipeThrough(new CompressionStream("deflate-raw"))).arrayBuffer().then(function (buf) { return "z" + b64(new Uint8Array(buf)); });
    }
    function unpack(code) {
      code = String(code || "").trim(); var kind = code[0], bytes = unb64(code.slice(1));
      if (kind === "j") return Promise.resolve(JSON.parse(new TextDecoder().decode(bytes)));
      return new Response(new Blob([bytes]).stream().pipeThrough(new DecompressionStream("deflate-raw"))).text().then(JSON.parse);
    }
    function gathered() { // the offer / answer with all the addresses in it (no trickle: it goes by hand)
      return new Promise(function (res) {
        if (pc.iceGatheringState === "complete") return res();
        var done = false, fin = function () { if (!done) { done = true; res(); } };
        pc.addEventListener("icegatheringstatechange", function () { if (pc.iceGatheringState === "complete") fin(); });
        setTimeout(fin, 4000);
      });
    }
    function status(t) { $("oStatus").textContent = t; }
    // a message larger than the channel takes goes in pieces
    function send(obj) {
      if (!dc || dc.readyState !== "open") return;
      var t = JSON.stringify(obj);
      if (t.length < 15000) { dc.send(t); return; }
      var id = Math.random().toString(36).slice(2), n = Math.ceil(t.length / 15000);
      for (var i = 0; i < n; i++) dc.send(JSON.stringify({ t: "part", id: id, i: i, n: n, d: t.slice(i * 15000, (i + 1) * 15000) }));
    }
    function receive(raw) {
      var m = JSON.parse(raw);
      if (m.t === "part") {
        var q = parts[m.id] = parts[m.id] || []; q[m.i] = m.d;
        if (q.filter(function (x) { return x != null; }).length === m.n) { delete parts[m.id]; receive(q.join("")); }
        return;
      }
      if (ONLINE === "host") {
        if (m.t === "cmd" && m.cmd === "plan" && worker) worker.postMessage({ cmd: "plan", token: token, plan: m.plan, team: 1 });
        else if (m.t === "cmd" && m.cmd === "sub" && worker) worker.postMessage({ cmd: "sub", token: token, out: m.out, in: m.in, team: 1 });
        else if (m.t === "ready") { guestReady = true; var w = readyWaiters; readyWaiters = []; w.forEach(function (f) { f(); }); toast("Your friend is ready"); }
      } else {
        if (m.t === "header") beginMatch(swapHeader(JSON.parse(m.header)));
        else if (m.t === "chunk") appendChunk(swapChunk(m));
      }
    }
    function wire() {
      dc.onopen = function () { status("Connected!"); if (onOpen) onOpen(); };
      dc.onclose = function () { if (ONLINE) toast("The connection to your friend was lost"); };
      dc.onmessage = function (ev) { receive(ev.data); };
    }
    // ---- the friend's side: his team is the home team in his view ----
    function swapHeader(h) {
      var a = h.home; h.home = h.away; h.away = a;
      if (h.shootout) h.shootout = [h.shootout[1], h.shootout[0]];
      return h;
    }
    function swapIndex(i) { return i >= 0 && i < 22 ? (i + 11) % 22 : i; }
    function swapFrame(f) {
      var a = f.slice(), PB0 = 9, PS0 = 5, GB0 = PB0 + 22 * PS0 + 9 + 3 + 4, FB0 = GB0 - 4;
      a[3] = f[4]; a[4] = f[3]; a[8] = swapIndex(f[8]);
      for (var p = 0; p < 22; p++) for (var k = 0; k < PS0; k++) a[PB0 + swapIndex(p) * PS0 + k] = f[PB0 + p * PS0 + k];
      if (f.length > GB0 + 21) for (p = 0; p < 22; p++) a[GB0 + swapIndex(p)] = f[GB0 + p];
      if (f[FB0 + 3] > 0) a[FB0 + 3] = (1 - Math.floor(f[FB0 + 3] / 1000)) * 1000 + f[FB0 + 3] % 1000;
      return a;
    }
    function swapChunk(m) {
      var L2 = m.len, out = [], i;
      for (i = 0; i < m.frames.length; i += L2) Array.prototype.push.apply(out, swapFrame(m.frames.slice(i, i + L2)));
      var ev = JSON.parse(m.events).map(function (e) { if (e.team === 0 || e.team === 1) e.team = 1 - e.team; if (e.p != null) e.p = swapIndex(e.p); if (e.o != null) e.o = swapIndex(e.o); return e; });
      var SL = 15 + 22 + 22, st = m.stats.slice();
      for (i = 0; i + SL <= st.length; i += SL) {
        for (var k = 1; k < 15; k += 2) { st[i + k] = m.stats[i + k + 1]; st[i + k + 1] = m.stats[i + k]; }
        for (k = 0; k < 22; k++) { st[i + 15 + swapIndex(k)] = m.stats[i + 15 + k]; st[i + 37 + swapIndex(k)] = m.stats[i + 37 + k]; }
      }
      var fin = null; if (m.final) { try { fin = JSON.stringify(swapHeader(JSON.parse(m.final))); } catch (x) {} }
      return { len: L2, frames: out, events: JSON.stringify(ev), stats: st, finished: m.finished, final: fin };
    }
    return {
      // the host: the invite link
      invite: function (kit) {
        ONLINE = "host";
        pc = new RTCPeerConnection(RTC); dc = pc.createDataChannel("soccervision", { ordered: true }); wire();
        return pc.createOffer().then(function (o) { return pc.setLocalDescription(o); }).then(gathered).then(function () {
          return pack({ sdp: pc.localDescription.sdp, kit: kit });
        }).then(function (code) { return location.origin + location.pathname + "#join=" + code; });
      },
      accept: function (reply) {
        return unpack(reply).then(function (r) { return pc.setRemoteDescription({ type: "answer", sdp: r.sdp }).then(function () { return r.kit; }); });
      },
      // the friend: the reply code
      join: function (offerCode) {
        ONLINE = "guest";
        return unpack(offerCode).then(function (o) {
          pc = new RTCPeerConnection(RTC);
          pc.ondatachannel = function (ev) { dc = ev.channel; wire(); };
          return pc.setRemoteDescription({ type: "offer", sdp: o.sdp }).then(function () { return o.kit; });
        });
      },
      answer: function (kit) {
        return pc.createAnswer().then(function (a) { return pc.setLocalDescription(a); }).then(gathered).then(function () { return pack({ sdp: pc.localDescription.sdp, kit: kit }); });
      },
      onOpen: function (f) { onOpen = f; },
      forward: function (m) { // the host's match, as it is played, to the friend
        if (ONLINE !== "host") return;
        if (m.type === "header") send({ t: "header", header: m.header });
        else send({ t: "chunk", len: m.len, frames: Array.prototype.slice.call(m.frames), events: m.events, stats: Array.prototype.slice.call(m.stats), finished: m.finished, final: m.final });
      },
      whenGuestReady: function (f) { if (guestReady) f(); else { readyWaiters.push(f); toast("Waiting for your friend to start..."); } },
      // the friend's "engine": his instructions, changes and kick-off go to the host
      remoteWorker: { postMessage: function (m) {
        if (m.cmd === "plan") send({ t: "cmd", cmd: "plan", plan: m.plan });
        else if (m.cmd === "sub") send({ t: "cmd", cmd: "sub", out: m.out, in: m.in });
        else if (m.cmd === "go") { send({ t: "cmd", cmd: "plan", plan: planString() }); send({ t: "ready" }); } // (his eleven and instructions, then ready)
      } },
      status: status
    };
  })();
