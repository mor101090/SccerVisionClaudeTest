(function () {
  "use strict";
  var STAGED_SEED = __STAGED_SEED__;
  // The engine lives in a folder named after its build, so a browser or CDN never mixes a new page with old engine files.
  var ENGINE_DIR = "__ENGINE_DIR__";
  var R, F, E, teams, t0, tEnd, secondHalfT, t, shown = -1;
  var PB = 9, PS = 5, OB = PB + 22 * PS;
  var playing = true, speed = 1, current = -1, addedMinutes = [0, 0, 0, 0], et1T = Infinity, et2T = Infinity, SO_KICKS = [];

  var $ = function (id) { return document.getElementById(id); };
  // a touch screen (no hover): tooltips and hover cards are off, so every tap is a click at once
  var TOUCH = !!(window.matchMedia && matchMedia("(hover: none)").matches);
  var seek = $("seek");

  function ft(f) { return f[0] / 100; }
  function find(time) {
    var lo = 0, hi = F.length - 1;
    if (time <= ft(F[0])) return 0;
    if (time >= ft(F[hi])) return hi;
    while (hi - lo > 1) { var mid = (lo + hi) >> 1; if (ft(F[mid]) <= time) lo = mid; else hi = mid; }
    return lo;
  }
  function lerp(a, b, k) { return a + (b - a) * k; }
  function lerpA(a, b, k) { var d = b - a; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; return a + d * k; }
  function sample(time) {
    var i = find(time), a = F[i], b = F[Math.min(i + 1, F.length - 1)];
    var gap = ft(b) - ft(a), half = gap > 5;
    var k = gap > 1e-6 && !half ? Math.max(0, Math.min(1, (time - ft(a)) / gap)) : 0;
    var ps = [], GB = OB + 9 + 3 + 4; // (the gesture codes follow the substitution's second man)
    for (var p = 0; p < 22; p++) {
      var o = PB + p * PS;
      var ka = KICK_OFF[(a[o + 4] >> 7) & 3], kb = KICK_OFF[(b[o + 4] >> 7) & 3];
      ps.push({ kp: lerp(ka, kb, k), kl: (a[o + 4] & 512) !== 0, kh: (a[o + 4] & 2048) !== 0, no: (a[o + 4] & 4096) !== 0, x: lerp(a[o], b[o], k) / 10, y: lerp(a[o + 1], b[o + 1], k) / 10, f: lerpA(a[o + 2] / 100, b[o + 2] / 100, k), j: lerp(a[o + 3], b[o + 3], k) / 100, off: (a[o + 4] & 1) !== 0, gk: (a[o + 4] & 2) !== 0, bk: (a[o + 4] & 4) !== 0, os: (a[o + 4] & 8) !== 0, sl: (a[o + 4] & 16) !== 0, gr: (a[o + 4] & 32) !== 0, inj: (a[o + 4] & 64) !== 0, dv: (a[o + 4] & 1024) !== 0, vx: (b[o] - a[o]) / 10, vy: (b[o + 1] - a[o + 1]) / 10, g: a.length > GB + p ? a[GB + p] : 0 });
    }
    var os = [];
    for (var q = 0; q < 3; q++) { var oo = OB + q * 3; os.push({ x: lerp(a[oo], b[oo], k) / 10, y: lerp(a[oo + 1], b[oo + 1], k) / 10, flag: a[oo + 2] === 1 || a[oo + 2] === 4, fdir: a[oo + 2] === 4 ? -1 : 1, card: a[oo + 2] === 2 ? 1 : a[oo + 2] === 3 ? 2 : 0 }); }
    var jump = Math.hypot(b[5] - a[5], b[6] - a[6]) > 150, bk = jump ? 0 : k;
    // the ball that went out, rolling on beyond the lines (height -1: none)
    var DB = OB + 9, dead = a.length > DB + 2 && a[DB + 2] >= 0 ? { x: lerp(a[DB], b[DB + 2] >= 0 ? b[DB] : a[DB], k) / 10, y: lerp(a[DB + 1], b[DB + 2] >= 0 ? b[DB + 1] : a[DB + 1], k) / 10, z: a[DB + 2] / 100 } : null;
    // the other man of a substitution (the substitute waiting for the high five, the player walking off)
    var FB = DB + 3, fig = a.length > FB + 3 && a[FB + 3] > 0 ? { x: lerp(a[FB], b[FB + 3] > 0 ? b[FB] : a[FB], k) / 10, y: lerp(a[FB + 1], b[FB + 3] > 0 ? b[FB + 1] : a[FB + 1], k) / 10, f: a[FB + 2] / 100, team: Math.floor(a[FB + 3] / 1000), number: a[FB + 3] % 1000 } : null;
    return { fig: fig, dead: dead, clock: lerp(a[1], b[1], half ? 0 : k), phase: a[2], hs: a[3], as: a[4], bx: lerp(a[5], b[5], bk) / 10, by: lerp(a[6], b[6], bk) / 10, bz: lerp(a[7], b[7], bk) / 100, owner: a[8], bw: a.length >= 160 ? [a[157] / 10, a[158] / 10, a[159] / 10] : null, ps: ps, os: os, half: half }; // (bw: the free ball's physical spin, rad per replay second; older replays have none)
  }
  function minuteText(clock, second) {
    var end = clock >= 5400 && t >= et1T ? (t >= et2T ? 7200 : 6300) : second ? 5400 : 2700;
    if (clock < end) return Math.floor(clock / 60) + 1 + "'";
    return end / 60 + "+" + (Math.floor((clock - end) / 60) + 1) + "'";
  }
  function mmss(s) { s = Math.max(0, Math.floor(s)); return Math.floor(s / 60) + ":" + String(s % 60).padStart(2, "0"); }
