  // ---- the state of the pitch: mud, puddles and snow -------------------------------------------------
  // The engine's own map (1 m grid of mud and standing water, built from Perlin noise where a pitch wears) decides where
  // the patches are, so the ball really stops in the puddles you see. Finer value noise ("grunge") breaks up their edges.
  function hash2(i, j) { var h = Math.imul(i, 374761393) ^ Math.imul(j, 668265263); h = Math.imul(h ^ (h >>> 13), 1274126177); return ((h ^ (h >>> 16)) >>> 0) / 4294967295; }
  function vnoise(x, y) {
    var i = Math.floor(x), j = Math.floor(y), u = x - i, v = y - j; u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
    var a = hash2(i, j), b = hash2(i + 1, j), c = hash2(i, j + 1), d = hash2(i + 1, j + 1);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
  }
  function grunge(x, y) { return .55 * vnoise(x * .7, y * .7) + .3 * vnoise(x * 2.3 + 7, y * 2.3 - 3) + .15 * vnoise(x * 6.1 - 11, y * 6.1 + 5); }
  var SNOW = { x: -60, y: 48, w: 120, h: 92 }; // the snow covers the whole view
  var SPX = 6, surfCv = null, snowCv = null, trodCv = null, puddles = [];
  // Textures from the shared module (web/src/replay/textures.ts): a new texture seed every time the page renders them.
  var TEXSEED = Math.floor(Math.random() * 1e9);
  function buildSurface() {
    surfCv = null; puddles = [];
    var sf = R.surface;
    if (sf && (sf.mud || sf.water)) {
      var res = SVTex.surfaceCanvas(sf, SET.weather === "Rain", SET.night, TEXSEED, 8);
      surfCv = res.canvas; puddles = res.puddles;
    }
    snowCv = null; trodCv = null;
    if (SET.weather === "Snow") {
      snowCv = SVTex.snowCanvas(TEXSEED, SPX, SET.snowCover === "covered");
      // trodCv is the snow as it lies now: a copy of the fresh layer the players tread holes into
      trodCv = document.createElement("canvas"); trodCv.width = snowCv.width; trodCv.height = snowCv.height;
      trodCv.getContext("2d").drawImage(snowCv, 0, 0);
    }
  }
  // Where players run and the ball rolls, the snow is trodden down and the grass shows through.
  function treadSnow(S, dt) {
    if (!trodCv || dt <= 0) return;
    var g = trodCv.getContext("2d");
    g.globalCompositeOperation = "destination-out";
    g.fillStyle = "rgba(0,0,0," + Math.min(.12, dt * .35) + ")";
    S.ps.forEach(function (p) { if (p.off) return; g.beginPath(); g.arc((p.x - SNOW.x) * SPX, (SNOW.y - p.y) * SPX, .45 * SPX, 0, Math.PI * 2); g.fill(); });
    if (S.bz < .3) { g.fillStyle = "rgba(0,0,0," + Math.min(.3, dt * 1.2) + ")"; g.beginPath(); g.arc((S.bx - SNOW.x) * SPX, (SNOW.y - S.by) * SPX, .16 * SPX, 0, Math.PI * 2); g.fill(); }
  }

  // Everything that does not move - turf, mud, puddles, the painted lines and the light - is drawn once into an image
  // and only copied each frame.
  var bgCv = null, linesCv = null, snowBase = null, snowAt = -1, bgKey = "";
  function pitch() {
    var snow = SET.weather === "Snow" && trodCv;
    var key = cv.width + "x" + cv.height + "|" + SET.weather + SET.grass + SET.snowLines + SET.snowCover + SET.night + SET.stadium + "|" + (surfCv ? surfCv.width + ":" + TEXSEED + ":" + R.seed + JSON.stringify(R.conditions || {}) : "-");
    if (key !== bgKey || !bgCv) {
      bgKey = key;
      var real = ctx;
      bgCv = document.createElement("canvas"); bgCv.width = cv.width; bgCv.height = cv.height;
      ctx = bgCv.getContext("2d"); grass(); if (surfCv) ctx.drawImage(surfCv, X(-54), Y(36), 108 * view.s, 72 * view.s);
      if (snow) {
        bakeLight(ctx);
        linesCv = document.createElement("canvas"); linesCv.width = cv.width; linesCv.height = cv.height;
        ctx = linesCv.getContext("2d"); lines();
        snowBase = bgCv; bgCv = document.createElement("canvas"); bgCv.width = cv.width; bgCv.height = cv.height; snowAt = -1;
      } else { lines(); bakeLight(ctx); linesCv = null; snowBase = null; }
      ctx = real;
    }
    // In the snow the picture (turf, the snow as trodden so far, the lines) is put together twice a second, not every frame.
    if (snow && (snowAt < 0 || Math.abs(performance.now() - snowAt) > 500)) {
      snowAt = performance.now();
      var g = bgCv.getContext("2d"), s = view.s;
      g.drawImage(snowBase, 0, 0); g.drawImage(trodCv, X(SNOW.x), Y(SNOW.y), SNOW.w * s, SNOW.h * s); g.drawImage(linesCv, 0, 0);
    }
    ctx.drawImage(bgCv, 0, 0);
  }
  // The light that does not move - the floodlit dark of a night match, the dull light of a grey sky - is baked into the
  // background image instead of being laid over the whole screen every frame (players stand in the floodlights anyway).
  function bakeLight(g) {
    if (SET.night) { if (!darkCv || darkCv.width !== cv.width || darkCv.height !== cv.height) buildDark(); g.drawImage(darkCv, 0, 0); return; }
    var wx = SET.weather;
    if (wx === "Sunny") { if (!standCv || standCv.key !== SET.stadium + cv.width + "x" + cv.height) buildStandShadow(); g.drawImage(standCv, 0, 0); return; }
    g.fillStyle = wx === "Rain" ? "rgba(18,28,44,.28)" : wx === "Snow" ? "rgba(40,52,74,.12)" : "rgba(22,30,42,.16)";
    g.fillRect(0, 0, cv.width, cv.height);
  }
  function fillC(text, x, y) {
    ctx.textAlign = "left"; ctx.textBaseline = "alphabetic";
    var m = ctx.measureText(text);
    ctx.fillText(text, x - (m.actualBoundingBoxRight - m.actualBoundingBoxLeft) / 2, y + (m.actualBoundingBoxAscent - m.actualBoundingBoxDescent) / 2);
  }
  // Goal net from above: a mesh of strings the ball presses out; hard shots make it bulge and ripple, and the
  // frame flashes when the ball rattles the woodwork (same model as Unity's GoalNetView).
  function netFx(time, side) {
    var rip = null, fl = null;
    for (var i = 0; i < E.length; i++) {
      var e = E[i]; if (e.t > time) break;
      if (time - e.t > 2 || e.x === undefined || Math.sign(e.x) !== side) continue;
      var age = time - e.t;
      if (e.type === "NetHit") { var pk = Math.min(.9, (e.v || 10) * .045) * Math.exp(-3 * age); if (!rip || pk > rip.pk) rip = { x: e.x, y: e.y, amp: pk * Math.cos(12 * age), pk: pk }; }
      if (e.type === "HitWoodwork") fl = { x: e.x, y: e.y, age: age };
    }
    return { rip: rip, fl: fl };
  }
  function drawNet(side, bx, by, fx) {
    var s = view.s, gl = 52.5 * side, at = { x: 0, y: 0 }, amp = 0;
    if (bx * side > 52.5 && Math.abs(by) < 4.16) { amp = Math.min(1, (bx * side - 52.5) / 2) * .35; at = { x: bx, y: by }; }
    if (fx.rip && (Math.abs(fx.rip.amp) > amp || amp === 0)) { amp = fx.rip.amp; at = fx.rip; }
    var atD = at.x * side - 52.5;
    function P(y, d) {
      var w = amp === 0 ? 0 : Math.exp(-((y - at.y) * (y - at.y) + (d - atD) * (d - atD)) / 1.445);
      var push = amp * w * (.35 + .65 * Math.min(1, d / 2));
      return [X(gl + (d + push) * side), Y(y + (y - at.y) * amp * w * .25)];
    }
    var ys = [], ds = [], i, k;
    for (i = 0; i < 25; i++) ys.push(-3.66 + i * 7.32 / 24);
    for (k = 0; k < 8; k++) ds.push(k * 2 / 7);
    ctx.save(); ctx.strokeStyle = rgba(SET.net, .62);
    ys.forEach(function (y, i) { ctx.lineWidth = i === 0 || i === 24 ? Math.max(1.5, .2 * s) : 1; ctx.beginPath(); ds.forEach(function (d, k) { var p = P(y, d); if (k) ctx.lineTo(p[0], p[1]); else ctx.moveTo(p[0], p[1]); }); ctx.stroke(); });
    ds.forEach(function (d, k) { ctx.lineWidth = k === 7 ? Math.max(1.5, .2 * s) : 1; ctx.beginPath(); ys.forEach(function (y, i) { var p = P(y, d); if (i) ctx.lineTo(p[0], p[1]); else ctx.moveTo(p[0], p[1]); }); ctx.stroke(); });
    if (fx.fl && fx.fl.age < .6) {
      var rr = (1 + fx.fl.age * 2) * s, g = ctx.createRadialGradient(X(fx.fl.x), Y(fx.fl.y), 0, X(fx.fl.x), Y(fx.fl.y), rr);
      g.addColorStop(0, "rgba(255,245,170," + (.9 * (1 - fx.fl.age / .6)) + ")"); g.addColorStop(1, "rgba(255,245,170,0)");
      ctx.fillStyle = g; ctx.beginPath(); ctx.arc(X(fx.fl.x), Y(fx.fl.y), rr, 0, Math.PI * 2); ctx.fill();
    }
    ctx.restore();
  }
  // The goal frame (crossbar over the line, the posts at its ends) stays white whatever colour the net is.
  function goalFrame(side) {
    var s = view.s, gl = 52.5 * side;
    ctx.save(); ctx.strokeStyle = "#fbfbfb"; ctx.lineWidth = Math.max(2, .24 * s); ctx.lineCap = "round";
    ctx.beginPath(); ctx.moveTo(X(gl), Y(-3.72)); ctx.lineTo(X(gl), Y(3.72)); ctx.stroke();
    ctx.fillStyle = "#ffffff"; [-3.72, 3.72].forEach(function (py) { ctx.beginPath(); ctx.arc(X(gl), Y(py), Math.max(1.6, .16 * s), 0, Math.PI * 2); ctx.fill(); });
    ctx.restore();
  }