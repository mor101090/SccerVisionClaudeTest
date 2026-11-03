  // ---- online: two friends, one match --------------------------------------------------------------------------------
  // The host's browser runs the engine; the friend's receives the match as it is played and sends his instructions and
  // changes back. The two connect directly (WebRTC). They find each other by a six-digit game code: the free PeerJS
  // broker (0.peerjs.com) only passes the handshake between the two browsers - no game data goes through it - and a STUN
  // server tells each browser its own internet address. If the connection drops (a laptop goes to sleep, the network
  // changes) the friend's page reconnects by itself and the host sends him what he missed. If the broker cannot be reached,
  // the old way still works: an invite link and a reply code, copied by hand. The friend sees his own team as the home team:
  // the data is turned round on his side.
  var ONLINE = null;
  var NET = (function () {
    var pc = null, dc = null, guestReady = false, readyWaiters = [], parts = {}, onOpen = null;
    // several STUN servers (some networks block one port or another). A TURN relay - for the networks where two browsers cannot
    // reach each other directly (mobile data, some work and university networks) - can be given in the address:
    // ?turn=turn:host:3478,user,password (both players need the same one). The data channel is encrypted end to end either way.
    var RTC = { iceServers: [{ urls: ["stun:stun.l.google.com:19302", "stun:stun1.l.google.com:19302"] }, { urls: "stun:stun.cloudflare.com:3478" }, { urls: "stun:global.stun.twilio.com:3478" }] };
    (function () {
      var m = /[?&]turn=([^&#]+)/.exec(location.search); if (!m) return;
      var p = decodeURIComponent(m[1]).split(","); RTC.iceServers.push({ urls: p[0], username: p[1] || "", credential: p[2] || "" });
    })();
    var connectTimer = 0;
    // the code way: PeerJS, a heartbeat, the friend coming back
    var peer = null, conn = null, hostId = null, gameCode = "", lastSeen = 0, beat = 0, lostShown = false, retry = 0, everOpen = false;
    var history = [], gotSeq = -1, mySeq = 0, matchSeed = null, kitsMine = null, kitsTheirs = null, onKits = null, wasKicked = false;
    var PEER_ID = function (code) { return "soccervision-game-" + code; };
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
      // It waits for the internet address the STUN server reports (a "srflx" candidate) - without it the code holds only the
      // address inside the home network, and a friend elsewhere can never connect (the old 4 s limit cut it off on slow networks).
      return new Promise(function (res) {
        if (pc.iceGatheringState === "complete") return res();
        var done = false, fin = function () { if (!done) { done = true; res(); } };
        pc.addEventListener("icegatheringstatechange", function () { if (pc.iceGatheringState === "complete") fin(); });
        pc.addEventListener("icecandidate", function (ev) { if (ev.candidate && / typ (srflx|relay)/.test(ev.candidate.candidate)) setTimeout(fin, 1500); });
        setTimeout(fin, 12000);
      });
    }
    function reachable(sdp) { return / typ (srflx|relay)/.test(sdp); }
    // what the connection is doing, in words - and what to try when it cannot get through
    function watch() {
      pc.addEventListener("iceconnectionstatechange", function () {
        var s = pc.iceConnectionState;
        if (s === "checking") status("Connecting... (looking for a way through the two networks)");
        else if (s === "connected" || s === "completed") status("Connected - starting...");
        else if (s === "failed") failed();
        else if (s === "disconnected" && dc && dc.readyState === "open") toast("The connection to your friend is unsteady...");
      });
    }
    function failed() {
      clearTimeout(connectTimer);
      status("Could not connect. Your two networks do not let the browsers reach each other directly (this happens on mobile data and on some work or school networks). "
        + "Try both on home Wi-Fi, keep both pages open, and make a new invite (Cancel, then invite again).");
    }
    function startTimer() {
      clearTimeout(connectTimer);
      connectTimer = setTimeout(function () { if (!dc || dc.readyState !== "open") failed(); }, 30000);
    }
    function status(t) { $("oStatus").textContent = t; }
    // a message larger than the channel takes goes in pieces
    function channelOpen() { return conn ? conn.open : !!(dc && dc.readyState === "open"); }
    var DBG = window.SV_NET = { sent: 0, recv: 0, errors: [] }; // (counters, for a look in the console when something is wrong)
    // (for tests: drop() closes the connection, mute(ms) makes this side go silent - like a laptop going to sleep)
    DBG.drop = function () { try { if (conn) conn.close(); else if (dc) dc.close(); } catch (e) {} };
    DBG.mute = function (ms) { DBG.muted = Date.now() + ms; };
    function raw(t) { if (DBG.muted > Date.now()) return; try { if (conn) conn.send(t); else dc.send(t); DBG.sent++; } catch (e) { DBG.errors.push(String(e && e.message || e)); } }
    function send(obj) {
      if (!channelOpen()) return;
      var t = JSON.stringify(obj);
      if (t.length < 15000) { raw(t); return; }
      var id = Math.random().toString(36).slice(2), n = Math.ceil(t.length / 15000);
      for (var i = 0; i < n; i++) raw(JSON.stringify({ t: "part", id: id, i: i, n: n, d: t.slice(i * 15000, (i + 1) * 15000) }));
    }
    function receive(text) {
      if (DBG.muted > Date.now()) return;
      lastSeen = Date.now(); DBG.recv++;
      var m = JSON.parse(text);
      DBG.last = m.t;
      if (m.t === "ping") return;
      (DBG.kinds = DBG.kinds || {})[m.t] = (DBG.kinds[m.t] || 0) + 1;
      if (m.t === "hello") { hello(m); return; }
      if (m.t === "part") {
        var q = parts[m.id] = parts[m.id] || []; q[m.i] = m.d;
        if (q.filter(function (x) { return x != null; }).length === m.n) { delete parts[m.id]; receive(q.join("")); }
        return;
      }
      if (ONLINE === "host") {
        if (m.t === "cmd" && m.cmd === "plan" && worker) worker.postMessage({ cmd: "plan", token: token, plan: m.plan, team: 1 });
        else if (m.t === "cmd" && m.cmd === "sub" && worker) worker.postMessage({ cmd: "sub", token: token, out: m.out, in: m.in, team: 1 });
        else if (m.t === "ready") guestIsReady(true);
      } else {
        if (m.seq != null && m.seq <= gotSeq) return;                 // (already had it: sent again after a reconnection)
        if (m.seq != null) gotSeq = m.seq;
        if (m.t === "header") { matchSeed = JSON.parse(m.header).seed; beginMatch(swapHeader(JSON.parse(m.header))); }
        else if (m.t === "chunk") appendChunk(swapChunk(m));
      }
    }
    function guestIsReady(say) {
      if (guestReady) return;
      guestReady = true; var w = readyWaiters; readyWaiters = []; w.forEach(function (f) { f(); }); if (say) toast("Your friend is ready");
    }
    // the kits, both ways: each side has its own, and both work out the same match kits from the two (see onlineKits)
    function sayHello() { send({ t: "hello", kits: kitsMine, have: gotSeq, kicked: ONLINE === "guest" && KICKED }); }
    function hello(m) {
      var first = !kitsTheirs;
      kitsTheirs = m.kits;
      if (ONLINE === "host") {
        if (first) { if (onKits) onKits(kitsTheirs); }
        else {
          // the friend is back: what he missed, then his team and his "ready" again
          toast("Your friend is back");
          for (var i = 0; i < history.length; i++) if (history[i].seq > m.have) send(history[i]);
        }
        if (m.kicked) guestIsReady(false);
        if (first) sayHello();
      } else {
        if (first) { if (onKits) onKits(kitsTheirs); }
        else if (KICKED) { send({ t: "cmd", cmd: "plan", plan: planString() }); send({ t: "ready" }); }
      }
    }
    function heartbeat() {
      clearInterval(beat);
      lastSeen = Date.now();
      beat = setInterval(function () {
        if (channelOpen()) raw(JSON.stringify({ t: "ping" }));
        var quiet = Date.now() - lastSeen; DBG.quiet = quiet; DBG.beats = (DBG.beats || 0) + 1;
        if (quiet > 9000 && !lostShown) {
          lostShown = true;
          netState(ONLINE === "guest" ? "Connection to your friend lost - reconnecting..." : "Connection to your friend lost - waiting for him to come back...");
          if (ONLINE === "guest" && conn) reconnect();
        } else if (quiet < 4000 && lostShown) { lostShown = false; netState(""); }
      }, 2000);
    }
    function netState(text) {
      var el = $("netState"); if (!el) return;
      el.textContent = text; el.hidden = !text;
    }
    function wire() {
      dc.onopen = function () { clearTimeout(connectTimer); status("Connected!"); heartbeat(); if (onOpen) onOpen(); };
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
    // ---- the code way (PeerJS) ----
    function loadPeer() {
      if (window.Peer) return Promise.resolve();
      var urls = ["https://unpkg.com/peerjs@1.5.4/dist/peerjs.min.js", "https://cdn.jsdelivr.net/npm/peerjs@1.5.4/dist/peerjs.min.js"];
      return urls.reduce(function (p, u) {
        return p.catch(function () { return new Promise(function (res, rej) { var sc = document.createElement("script"); sc.src = u; sc.onload = function () { window.Peer ? res() : rej(); }; sc.onerror = rej; document.head.appendChild(sc); }); });
      }, Promise.reject()).then(null, function () { throw new Error("the game-code service could not be loaded"); });
    }
    function newCode() { var a = new Uint32Array(1); crypto.getRandomValues(a); return String(100000 + a[0] % 900000); }
    function attach(c) {
      if (conn && conn !== c) { try { conn.close(); } catch (e) {} }
      conn = c;
      c.on("data", function (d) { receive(typeof d === "string" ? d : String(d)); });
      c.on("open", function () {
        retry = 0; lostShown = false; netState(""); clearTimeout(connectTimer);
        sayHello(); heartbeat();
        if (!everOpen) { everOpen = true; status("Connected - exchanging kits..."); }
      });
      c.on("close", function () { if (conn === c && ONLINE === "guest" && everOpen) reconnect(); });
      c.on("error", function () {});
    }
    function reconnect() {
      if (ONLINE !== "guest" || !peer) return;
      clearTimeout(reconnect.t);
      reconnect.t = setTimeout(function () {
        if (channelOpen() && Date.now() - lastSeen < 4000) return;
        retry++;
        if (peer.disconnected && !peer.destroyed) { try { peer.reconnect(); } catch (e) {} }
        attach(peer.connect(hostId, { reliable: true, serialization: "raw" }));
        reconnect();                                                     // (and again in a moment, until it holds)
      }, Math.min(8000, 1500 + retry * 1000));
    }
    function makePeer(id) {
      return new Peer(id, { debug: 0, config: RTC });
    }
    return {
      // the code way: the host gets a six-digit code, the friend types it in
      hostGame: function (kits, onCode, onFail) {
        ONLINE = "host"; kitsMine = kits;
        return loadPeer().then(function () {
          var tries = 0;
          (function open() {
            gameCode = newCode(); peer = makePeer(PEER_ID(gameCode));
            peer.on("open", function () { onCode(gameCode); });
            peer.on("connection", function (c) { attach(c); });
            peer.on("disconnected", function () { if (!peer.destroyed) setTimeout(function () { try { peer.reconnect(); } catch (e) {} }, 2000); }); // (from the broker only: the match goes on)
            peer.on("error", function (e) {
              if (e.type === "unavailable-id" && ++tries < 5) { peer.destroy(); open(); }
              else if (e.type === "network" || e.type === "server-error" || e.type === "socket-error") { if (!everOpen) onFail(e); }
            });
          })();
        });
      },
      joinGame: function (code, kits, onFail) {
        ONLINE = "guest"; kitsMine = kits; hostId = PEER_ID(code);
        return loadPeer().then(function () {
          peer = makePeer(undefined);
          peer.on("open", function () { status("Finding your friend's game..."); attach(peer.connect(hostId, { reliable: true, serialization: "raw" })); startTimer(); });
          peer.on("error", function (e) {
            if (e.type === "peer-unavailable") { if (!everOpen) { clearTimeout(connectTimer); onFail(new Error("There is no game with this code - check the six digits (and that your friend's page is still open).")); } else reconnect(); }
            else if (!everOpen && (e.type === "network" || e.type === "server-error" || e.type === "socket-error")) onFail(new Error("The game-code service cannot be reached."));
          });
          peer.on("disconnected", function () { if (!peer.destroyed) setTimeout(function () { try { peer.reconnect(); } catch (e) {} }, 2000); });
        });
      },
      onKits: function (f) { onKits = f; },
      // the host: the invite link
      invite: function (kit) {
        ONLINE = "host";
        pc = new RTCPeerConnection(RTC); dc = pc.createDataChannel("soccervision", { ordered: true }); wire(); watch();
        return pc.createOffer().then(function (o) { return pc.setLocalDescription(o); }).then(gathered).then(function () {
          if (!reachable(pc.localDescription.sdp)) toast("Your network did not report its internet address - a friend outside your home network may not be able to connect");
          return pack({ sdp: pc.localDescription.sdp, kit: kit });
        }).then(function (code) { return location.origin + location.pathname + "#join=" + code; });
      },
      accept: function (reply) {
        return unpack(reply).then(function (r) {
          if (!r || !r.sdp) throw new Error("not a reply code");
          if (pc.signalingState !== "have-local-offer") throw new Error("this invite was already used - make a new one");
          startTimer();
          return pc.setRemoteDescription({ type: "answer", sdp: r.sdp }).then(function () { return r.kit; });
        });
      },
      // the friend: the reply code
      join: function (offerCode) {
        ONLINE = "guest";
        return unpack(offerCode).then(function (o) {
          pc = new RTCPeerConnection(RTC);
          pc.ondatachannel = function (ev) { dc = ev.channel; wire(); };
          watch();
          return pc.setRemoteDescription({ type: "offer", sdp: o.sdp }).then(function () { return o.kit; });
        });
      },
      answer: function (kit) {
        return pc.createAnswer().then(function (a) { return pc.setLocalDescription(a); }).then(gathered).then(function () { return pack({ sdp: pc.localDescription.sdp, kit: kit }); });
      },
      onOpen: function (f) { onOpen = f; },
      forward: function (m) { // the host's match, as it is played, to the friend (and kept, for a friend who drops and comes back)
        DBG.forward = (DBG.forward || 0) + 1;
        if (ONLINE !== "host") return;
        var msg;
        if (m.type === "header") { history = []; msg = { t: "header", header: m.header, seq: mySeq++ }; }
        else msg = { t: "chunk", len: m.len, frames: Array.prototype.slice.call(m.frames), events: m.events, stats: Array.prototype.slice.call(m.stats), finished: m.finished, final: m.final, seq: mySeq++ };
        history.push(msg);
        send(msg);
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
