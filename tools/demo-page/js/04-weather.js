  // ---- weather and light -----------------------------------------------------------------------------
  // Wind of the loaded recording: speed (m/s) and the direction it blows towards, on screen (y down).
  var WIND = { speed: 0, angle: 0, screenAngle: 0, breeze: 0 };
  function rgba(hex, a) { var n = parseInt(String(hex).slice(1), 16); return "rgba(" + (n >> 16) + "," + ((n >> 8) & 255) + "," + (n & 255) + "," + a + ")"; }
  var drops = [], flakes = [], rings = [], cloudSeed = Math.random() * 100;
  function windScreen(k) { return { x: Math.cos(WIND.screenAngle) * WIND.speed * k, y: Math.sin(WIND.screenAngle) * WIND.speed * k }; }
  function weatherFx(now, dt) {
    var w = cv.width, h = cv.height, s = view.s, dpr = Math.min(2, window.devicePixelRatio || 1);
    if (SET.weather === "Rain") {
      // seen from above, rain is a fast hail of short streaks blown along by the wind, splashing on the grass
      var want = Math.round(w * h / 2600 * (QUALITY ? .55 : 1));
      while (drops.length < want) drops.push({ x: Math.random() * w, y: Math.random() * h, l: .5 + Math.random(), v: .8 + Math.random() * .5 });
      drops.length = want;
      var ws = windScreen(.06), dx = ws.x + .12, dy = 1 + ws.y * .3, dl = Math.hypot(dx, dy); dx /= dl; dy /= dl;
      ctx.strokeStyle = SET.night ? "rgba(225,235,255,.42)" : "rgba(205,218,232,.34)"; ctx.lineWidth = Math.max(1, .7 * dpr); ctx.beginPath();
      drops.forEach(function (d) {
        var step = d.v * 900 * dpr * dt; d.x += dx * step; d.y += dy * step;
        if (d.y > h) { d.y -= h; d.x = Math.random() * w; } if (d.x > w) d.x -= w; if (d.x < 0) d.x += w;
        ctx.moveTo(d.x, d.y); ctx.lineTo(d.x - dx * d.l * 1.1 * s, d.y - dy * d.l * 1.1 * s);
      });
      ctx.stroke();
      for (var k = 0; k < 9; k++) rings.push({ x: Math.random() * w, y: Math.random() * h, t: 0, max: .35, life: .3 });
      for (var q = 0; q < 3 && puddles.length; q++) { var pp = puddles[Math.floor(Math.random() * puddles.length)]; rings.push({ x: X(pp[0] + Math.random() - .5), y: Y(pp[1] + Math.random() - .5), t: 0, max: .7, life: .9 }); }
    }
    if (rings.length) {
      ctx.lineWidth = Math.max(.7, .5 * dpr);
      rings = rings.filter(function (r) {
        r.t += dt; if (r.t > r.life) return false;
        var k = r.t / r.life; ctx.strokeStyle = "rgba(220,232,240," + (.45 * (1 - k)) + ")";
        ctx.beginPath(); ctx.ellipse(r.x, r.y, r.max * k * s, r.max * k * s * .8, 0, 0, Math.PI * 2); ctx.stroke(); return true;
      });
      if (rings.length > 400) rings.splice(0, rings.length - 400);
    }
    if (SET.weather === "Snow") {
      var want2 = Math.round(w * h / 3800 * (QUALITY ? .55 : 1));
      while (flakes.length < want2) flakes.push({ x: Math.random() * w, y: Math.random() * h, r: (.6 + Math.random() * 1.6) * dpr, ph: Math.random() * 6.3, v: .5 + Math.random() * .8 });
      flakes.length = want2;
      var wsn = windScreen(9 * dpr);
      ctx.fillStyle = "rgba(255,255,255,.88)"; ctx.beginPath();
      flakes.forEach(function (f) {
        f.x += (wsn.x * f.v + Math.sin(now * 1.3 + f.ph) * 14 * dpr) * dt; f.y += (wsn.y * f.v + 26 * dpr * f.v) * dt;
        if (f.y > h) { f.y -= h; f.x = Math.random() * w; } if (f.y < 0) f.y += h; if (f.x > w) f.x -= w; if (f.x < 0) f.x += w;
        ctx.moveTo(f.x + f.r, f.y); ctx.arc(f.x, f.y, f.r, 0, Math.PI * 2);
      });
      ctx.fill();
    }
  }
  // Cloud shadows drift across the pitch with the wind ("grunge" clouds from layered noise blobs).
  // (drawn into a small image an eighth of the size and scaled up: soft shadows need no detail, and it is much cheaper)
  var cloudCv = document.createElement("canvas"), cloudCx = cloudCv.getContext("2d");
  function cloudShadows(now, strength) {
    if (QUALITY >= 1) return; // a weak device keeps its frames for the match
    var k = 8, cw = Math.ceil(cv.width / k), ch = Math.ceil(cv.height / k);
    if (cloudCv.width !== cw || cloudCv.height !== ch) { cloudCv.width = cw; cloudCv.height = ch; }
    var g2 = cloudCx, s = view.s / k, drift = Math.max(1.5, WIND.breeze || WIND.speed) * now * .6;
    var ux = Math.cos(WIND.angle), uy = Math.sin(WIND.angle);
    g2.clearRect(0, 0, cw, ch);
    for (var i = 0; i < 6; i++) {
      var span = 260, ox = ((hash2(i, 7) * span + drift * ux) % span + span) % span - span / 2, oy = ((hash2(i, 9) * 160 + drift * uy) % 160 + 160) % 160 - 80;
      var cx = X(ox) / k, cy = Y(oy) / k, rad = (22 + hash2(i, 3) * 26) * s, g = g2.createRadialGradient(cx, cy, 0, cx, cy, rad);
      g.addColorStop(0, "rgba(10,18,28," + strength + ")"); g.addColorStop(.6, "rgba(10,18,28," + strength * .6 + ")"); g.addColorStop(1, "rgba(10,18,28,0)");
      g2.fillStyle = g; g2.fillRect(cx - rad, cy - rad, rad * 2, rad * 2);
    }
    ctx.drawImage(cloudCv, 0, 0, cv.width, cv.height);
  }
  // Night: everything outside the floodlights' reach falls into the dark; the four towers light the pitch.
  var darkCv = null;
  function buildDark() {
    darkCv = document.createElement("canvas"); darkCv.width = cv.width; darkCv.height = cv.height;
    var g = darkCv.getContext("2d"), s = view.s;
    g.fillStyle = "rgba(2,7,22,.62)"; g.fillRect(0, 0, cv.width, cv.height);
    g.globalCompositeOperation = "destination-out";
    TOWERS.forEach(function (L) {
      var cx = X(L[0] * .4), cy = Y(L[1] * .4), gr = g.createRadialGradient(cx, cy, 0, cx, cy, 72 * s);
      gr.addColorStop(0, "rgba(0,0,0,.55)"); gr.addColorStop(.55, "rgba(0,0,0,.45)"); gr.addColorStop(.85, "rgba(0,0,0,.14)"); gr.addColorStop(1, "rgba(0,0,0,0)");
      g.fillStyle = gr; g.fillRect(0, 0, cv.width, cv.height);
    });
    g.globalCompositeOperation = "source-over";
    TOWERS.forEach(function (L) { // the towers' glare at the corners
      var cx = X(L[0]), cy = Y(L[1]), gr = g.createRadialGradient(cx, cy, 0, cx, cy, 7 * s);
      gr.addColorStop(0, "rgba(255,250,225,.9)"); gr.addColorStop(.25, "rgba(255,245,200,.35)"); gr.addColorStop(1, "rgba(255,245,200,0)");
      g.fillStyle = gr; g.fillRect(cx - 7 * s, cy - 7 * s, 14 * s, 14 * s);
    });
  }
  // The low sun (from the top left, like the players' shadows) is blocked by the stands: part of the pitch lies in
  // their shade with a sharp edge. A main stand along the far touchline throws a slanting band; a bowl's curved roof
  // shades the top and the left. Drawn once per size into an image.
  var standCv = null;
  // Is this point of the pitch in the stands' shade? (same shapes as buildStandShadow)
  var ROOF = [[-70, 5.5], [-47, 8], [-46, 9.6], [-7, 13.8], [-6, 15.5], [33, 19.8], [34, 21.5], [70, 25.5]];
  function inShade(x, y) {
    if (SET.night || SET.weather !== "Sunny" || SET.stadium === "open") return false;
    if (SET.stadium === "bowl") { var ex = (x - 14) / 66, ey = (y + 9) / 47; return ex * ex + ey * ey > 1; }
    for (var i = 0; i < ROOF.length - 1; i++) if (x >= ROOF[i][0] && x <= ROOF[i + 1][0]) return y > ROOF[i][1] + (ROOF[i + 1][1] - ROOF[i][1]) * (x - ROOF[i][0]) / (ROOF[i + 1][0] - ROOF[i][0]);
    return false;
  }
  function buildStandShadow() {
    standCv = document.createElement("canvas"); standCv.width = cv.width; standCv.height = cv.height; standCv.key = SET.stadium + cv.width + "x" + cv.height;
    var g = standCv.getContext("2d"), s = view.s, P = function (x, y) { return [X(x), Y(y)]; };
    g.fillStyle = "rgba(255,214,140,.05)"; g.fillRect(0, 0, cv.width, cv.height);
    if (SET.stadium === "open") return;
    g.fillStyle = "rgba(10,24,40,.34)";
    g.filter = "blur(" + Math.max(1, .35 * s) + "px)";
    g.beginPath();
    if (SET.stadium === "bowl") {
      g.rect(-50, -50, cv.width + 100, cv.height + 100); // everything outside the roof's curved opening
      g.ellipse(X(14), Y(-9), 66 * s, 47 * s, 0, 0, Math.PI * 2);
      g.fill("evenodd");
    } else {
      // the main stand's roof line, with the steps of its trusses
      var pts = [[-70, 60], [70, 60]].concat(ROOF.slice().reverse());
      pts.forEach(function (q, i) { var p2 = P(q[0], q[1]); if (i) g.lineTo(p2[0], p2[1]); else g.moveTo(p2[0], p2[1]); });
      g.closePath(); g.fill();
    }
  }
  function lighting(now) {
    var w = cv.width, h = cv.height, wx = SET.weather;
    if (SET.night) return; // baked into the background (bakeLight)
    if (wx === "Sunny") {
      // (the warm sunlight and the stands' shade are baked into the background; players in the shade are darkened in entities)
      cloudShadows(now, .06);
      return;
    }
    cloudShadows(now, wx === "Cloudy" ? .1 : .08); // (the grey sky's dull light is baked into the background)
  }
  var WX_ICON = {
    Sunny: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="3.2" fill="#ffd34d"/><g stroke="#ffd34d" stroke-width="1.4" stroke-linecap="round"><path d="M8 1v2M8 13v2M1 8h2M13 8h2M3 3l1.4 1.4M11.6 11.6L13 13M3 13l1.4-1.4M11.6 4.4L13 3"/></g></svg>',
    Night: '<svg viewBox="0 0 16 16"><path d="M10.5 2.2a6 6 0 1 0 3.3 9.6A5 5 0 0 1 10.5 2.2z" fill="#e8ecff"/></svg>',
    Cloudy: '<svg viewBox="0 0 16 16"><path d="M4.5 12.5h7.2a3 3 0 0 0 .2-6 4 4 0 0 0-7.6 1.2 2.4 2.4 0 0 0 .2 4.8z" fill="#cfd8e3"/></svg>',
    Rain: '<svg viewBox="0 0 16 16"><path d="M4.5 9.5h7.2a3 3 0 0 0 .2-6 4 4 0 0 0-7.6 1.2 2.4 2.4 0 0 0 .2 4.8z" fill="#cfd8e3"/><g stroke="#8fc3ff" stroke-width="1.3" stroke-linecap="round"><path d="M5 11.5l-1 2.5M8 11.5l-1 2.5M11 11.5l-1 2.5"/></g></svg>',
    Snow: '<svg viewBox="0 0 16 16"><path d="M4.5 9h7.2a3 3 0 0 0 .2-6 4 4 0 0 0-7.6 1.2 2.4 2.4 0 0 0 .2 4.8z" fill="#cfd8e3"/><g fill="#fff"><circle cx="5" cy="12" r="1"/><circle cx="8" cy="14" r="1"/><circle cx="11" cy="12" r="1"/></g></svg>'
  };
  function updateWeatherChip() {
    var sky = SET.night && SET.weather === "Sunny" ? "Night" : SET.weather, label = (SET.night ? "Night" : "Day") + " · " + (SET.weather === "Sunny" && SET.night ? "Clear" : SET.weather);
    var html = WX_ICON[sky] + "<span>" + label + "</span>";
    if (WIND.breeze > .1) html += '<svg viewBox="0 0 16 16" style="transform:rotate(' + (WIND.screenAngle * 180 / Math.PI) + 'deg)"><path d="M1.5 8h11M9 4.5L12.8 8 9 11.5" fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg><span>' + Math.round(WIND.breeze * 3.6) + " km/h</span>";
    $("wx").innerHTML = html;
  }
