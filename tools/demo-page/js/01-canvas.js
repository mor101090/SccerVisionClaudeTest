  // ---- drawing ---------------------------------------------------------------------------------------
  // World view: the pitch plus a wider band on top (scoreboard, technical areas) and below (commentary bar).
  var cv = $("pitch"), ctx = cv.getContext("2d"), W = 115, TOP = 46, BOTTOM = -41, H = TOP - BOTTOM, view = { s: 1, ox: 0, oy: 0 };
  function X(x) { return view.ox + x * view.s; }
  function Y(y) { return view.oy - y * view.s; }
  function resize() {
    // (3x screens: 2x is plenty and twice as fast; a weak device steps down further, see adaptQuality)
    var dpr = Math.min(QUALITY >= 2 ? 1 : QUALITY >= 1 ? 1.5 : 2, window.devicePixelRatio || 1), r = cv.getBoundingClientRect();
    cv.width = Math.round(r.width * dpr); cv.height = Math.round(r.height * dpr);
    view.s = Math.min(cv.width / W, cv.height / H); view.ox = cv.width / 2; view.oy = (cv.height - H * view.s) / 2 + TOP * view.s;
    grassNoise = null; darkCv = null; standCv = null; bgCv = null; drops = []; flakes = []; rings = [];
  }
  window.addEventListener("resize", resize);
  function line(x1, y1, x2, y2) { ctx.beginPath(); ctx.moveTo(X(x1), Y(y1)); ctx.lineTo(X(x2), Y(y2)); ctx.stroke(); }
  function arc(x, y, r, a0, a1) { ctx.beginPath(); ctx.arc(X(x), Y(y), r * view.s, -a1, -a0); ctx.stroke(); }
  function dot(x, y, r) { ctx.beginPath(); ctx.arc(X(x), Y(y), Math.max(1.5, r * view.s), 0, Math.PI * 2); ctx.fill(); }
  // Natural, slightly muted turf with a fine grain texture, mown in one of three patterns: bands along the length of the
  // pitch, bands across it, or rings around the centre spot.
  var grassNoise = null;
  function makeGrassNoise() {
    var n = document.createElement("canvas"); n.width = n.height = 192;
    var g = n.getContext("2d"), img = g.createImageData(192, 192), d = img.data;
    for (var i = 0; i < d.length; i += 4) {
      var v = Math.random(), light = v > .5;
      d[i] = d[i + 1] = d[i + 2] = light ? 255 : 0;
      d[i + 3] = Math.floor(Math.abs(v - .5) * 2 * 26);
    }
    g.putImageData(img, 0, 0);
    return ctx.createPattern(n, "repeat");
  }
  function grass() {
    var s = view.s, wet = SET.weather === "Rain";
    var light = wet ? "#4f8530" : "#5b8733", dark = wet ? "#46762b" : "#53792d";
    ctx.fillStyle = dark; ctx.fillRect(0, 0, cv.width, cv.height);
    ctx.fillStyle = light;
    if (SET.grass === "across") {
      var bwx = 105 / 18;
      for (var i = -2; i < 20; i++) if (i % 2 === 0) ctx.fillRect(X(-52.5 + i * bwx), 0, bwx * s + 1, cv.height);
    } else if (SET.grass === "circles") {
      for (var r = 80; r > 0; r -= 5) { ctx.fillStyle = (r / 5) % 2 ? light : dark; ctx.beginPath(); ctx.arc(X(0), Y(0), r * s, 0, Math.PI * 2); ctx.fill(); }
    } else {
      var bands = 12, bw = (TOP - BOTTOM) / bands;
      for (var j = 0; j < bands; j++) if (j % 2) ctx.fillRect(0, Y(TOP - j * bw), cv.width, bw * s + 1);
    }
    if (!grassNoise) grassNoise = makeGrassNoise();
    ctx.fillStyle = grassNoise; ctx.fillRect(0, 0, cv.width, cv.height);
    if (wet) { ctx.fillStyle = "rgba(170,210,230,.05)"; ctx.fillRect(0, 0, cv.width, cv.height); } // wet sheen
  }
  function lines() {
    // in the snow the lines are painted yellow or orange so they read on the white
    var s = view.s, paint = SET.weather === "Snow" ? (SET.snowLines === "orange" ? "#f26a1b" : SET.snowLines === "blue" ? "#1d5fd6" : "#f5c518") : "rgba(255,255,255,.9)";
    ctx.strokeStyle = paint; ctx.fillStyle = paint; ctx.lineWidth = Math.max(1, .12 * s);
    if (SET.weather === "Snow") { ctx.save(); ctx.shadowColor = "rgba(10,30,10,.35)"; ctx.shadowBlur = Math.max(1, .15 * s); ctx.lineWidth = Math.max(1.4, .16 * s); } // cleared lines read on the frost
    // Technical areas (coaches) above the top touchline, either side of the halfway line.
    ctx.strokeRect(X(-19), Y(39.6), 17.5 * s, 3.6 * s); ctx.strokeRect(X(1.5), Y(39.6), 17.5 * s, 3.6 * s);
    ctx.strokeRect(X(-52.5), Y(34), 105 * s, 68 * s); line(0, -34, 0, 34); arc(0, 0, 9.15, 0, Math.PI * 2); dot(0, 0, .2);
    [-1, 1].forEach(function (sd) {
      var gl = 52.5 * sd, pa = 36 * sd, ga = 47 * sd;
      line(gl, -20.16, pa, -20.16); line(gl, 20.16, pa, 20.16); line(pa, -20.16, pa, 20.16);
      line(gl, -9.16, ga, -9.16); line(gl, 9.16, ga, 9.16); line(ga, -9.16, ga, 9.16);
      dot(41.5 * sd, 0, .15);
      var h = Math.acos(5.5 / 9.15), c = sd > 0 ? Math.PI : 0; arc(41.5 * sd, 0, 9.15, c - h, c + h);
      [-1, 1].forEach(function (cc) { var st = sd > 0 ? (cc > 0 ? Math.PI : Math.PI / 2) : (cc > 0 ? 1.5 * Math.PI : 0); arc(gl, 34 * cc, 1, st, st + Math.PI / 2); });
    });
    if (SET.weather === "Snow") ctx.restore();
  }