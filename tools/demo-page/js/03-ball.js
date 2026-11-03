  // ---- 3D ball: a truncated icosahedron (12 black pentagons, 20 white hexagons) shaded and rotated as it rolls ----
  var PHI = (1 + Math.sqrt(5)) / 2, PENT = [], HEX = [];
  (function () {
    var v = [[0, 1, PHI], [0, -1, PHI], [0, 1, -PHI], [0, -1, -PHI], [1, PHI, 0], [-1, PHI, 0], [1, -PHI, 0], [-1, -PHI, 0], [PHI, 0, 1], [-PHI, 0, 1], [PHI, 0, -1], [-PHI, 0, -1]];
    v.forEach(function (a) { var l = Math.hypot(a[0], a[1], a[2]); PENT.push([a[0] / l, a[1] / l, a[2] / l]); });
    for (var i = 0; i < 12; i++) for (var j = i + 1; j < 12; j++) for (var k = j + 1; k < 12; k++) {
      var d = function (p, q) { return Math.hypot(v[p][0] - v[q][0], v[p][1] - v[q][1], v[p][2] - v[q][2]); };
      if (Math.abs(d(i, j) - 2) < 1e-6 && Math.abs(d(j, k) - 2) < 1e-6 && Math.abs(d(i, k) - 2) < 1e-6) {
        var c = [v[i][0] + v[j][0] + v[k][0], v[i][1] + v[j][1] + v[k][1], v[i][2] + v[j][2] + v[k][2]], l = Math.hypot(c[0], c[1], c[2]);
        HEX.push([c[0] / l, c[1] / l, c[2] / l]);
      }
    }
  })();
  // Face planes of the truncated icosahedron (edge 1): pentagons at 2.3274, hexagons at 2.2673 from the centre.
  var RP = 2.32744, RH = 2.26728;
  var ballRot = [1, 0, 0, 0, 1, 0, 0, 0, 1], ballSpin = { ax: [0, 1, 0], w: 0 }, ballPrev = null, ballCv = document.createElement("canvas"), ballCx = ballCv.getContext("2d");
  function rotateBall(ax, ang) {
    var c = Math.cos(ang), s1 = Math.sin(ang), t1 = 1 - c, x = ax[0], y = ax[1], z = ax[2];
    var r = [t1 * x * x + c, t1 * x * y - s1 * z, t1 * x * z + s1 * y, t1 * x * y + s1 * z, t1 * y * y + c, t1 * y * z - s1 * x, t1 * x * z - s1 * y, t1 * y * z + s1 * x, t1 * z * z + c], m = ballRot, o = [];
    for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) o[i * 3 + j] = r[i * 3] * m[j] + r[i * 3 + 1] * m[3 + j] + r[i * 3 + 2] * m[6 + j];
    ballRot = o;
  }
  function spinBall(S, h, dt, held) {
    if (held) { ballSpin.w = 0; ballPrev = { x: S.bx, y: S.by }; return; } // held by the keeper / carried overhead: no roll
    if (ballPrev && dt > 0) {
      var dx = S.bx - ballPrev.x, dy = S.by - ballPrev.y, dist = Math.hypot(dx, dy);
      if (dist > 0.0005 && dist < 3) {
        // Rolling without slipping (axis = up x direction of travel); in the air a gentler spin; the rate is capped so it stays readable.
        ballSpin.ax = [-dy / dist, dx / dist, 0];
        ballSpin.w = Math.min(dist / .3, 0.5) / dt * (h < .35 ? 1 : .35);
      } else ballSpin.w = 0;
      if (ballSpin.w > 0) rotateBall(ballSpin.ax, Math.min(.5, ballSpin.w * dt));
    }
    ballPrev = { x: S.bx, y: S.by };
  }
  var yellowBall = false;
  function drawBall(cx, cy, rad) {
    var size = Math.ceil(rad * 2) + 2, c = size / 2;
    if (ballCv.width !== size) { ballCv.width = size; ballCv.height = size; }
    var img = ballCx.createImageData(size, size), d = img.data, m = ballRot;
    var L = [-.45, .55, .7], ll = Math.hypot(L[0], L[1], L[2]); L = [L[0] / ll, L[1] / ll, L[2] / ll];
    var Hv = [L[0], L[1], L[2] + 1], hl = Math.hypot(Hv[0], Hv[1], Hv[2]); Hv = [Hv[0] / hl, Hv[1] / hl, Hv[2] / hl];
    for (var py = 0; py < size; py++) for (var px = 0; px < size; px++) {
      var nx = (px + .5 - c) / rad, ny = -(py + .5 - c) / rad, r2 = nx * nx + ny * ny;
      if (r2 > 1.0) continue;
      var nz = Math.sqrt(1 - r2);
      // into the ball's own frame (transpose of the rotation)
      var bx = m[0] * nx + m[3] * ny + m[6] * nz, by = m[1] * nx + m[4] * ny + m[7] * nz, bz = m[2] * nx + m[5] * ny + m[8] * nz;
      var best = -9, second = -9, pent = false, q, sc;
      for (q = 0; q < 12; q++) { sc = (PENT[q][0] * bx + PENT[q][1] * by + PENT[q][2] * bz) / RP; if (sc > best) { second = best; best = sc; pent = true; } else if (sc > second) second = sc; }
      for (q = 0; q < 20; q++) { sc = (HEX[q][0] * bx + HEX[q][1] * by + HEX[q][2] * bz) / RH; if (sc > best) { second = best; best = sc; pent = false; } else if (sc > second) second = sc; }
      // a white ball with black pentagons; in snow a bright orange one with pale panels (as the reference photo: no glow)
      var col, diff;
      if (yellowBall) {
        col = pent ? [236, 236, 230] : [255, 104, 26];
        if (best - second < .006) col = pent ? [190, 190, 186] : [214, 74, 12]; // stitched seams
        diff = .92 + .08 * Math.max(0, nx * L[0] + ny * L[1] + nz * L[2]);
      } else {
        col = pent ? [28, 28, 28] : [246, 246, 246];
        if (best - second < .006) col = pent ? [60, 60, 60] : [150, 150, 150]; // stitched seams
        diff = .38 + .62 * Math.max(0, nx * L[0] + ny * L[1] + nz * L[2]);
      }
      var spec = Math.pow(Math.max(0, nx * Hv[0] + ny * Hv[1] + nz * Hv[2]), 36) * (pent ? 120 : 90);
      var a = Math.min(1, (1 - Math.sqrt(r2)) * rad + .5), o = (py * size + px) * 4;
      d[o] = Math.min(255, col[0] * diff + spec); d[o + 1] = Math.min(255, col[1] * diff + spec); d[o + 2] = Math.min(255, col[2] * diff + spec); d[o + 3] = 255 * a;
    }
    ballCx.putImageData(img, 0, 0);
    ctx.drawImage(ballCv, cx - c, cy - c);
    ctx.beginPath(); ctx.arc(cx, cy, rad, 0, Math.PI * 2); ctx.lineWidth = Math.max(.6, rad * .08); ctx.strokeStyle = yellowBall ? "rgba(150,50,0,.7)" : "rgba(0,0,0,.45)"; ctx.stroke();
  }
  // Offside: a small chequered square above the player who was caught (no text, no pole).
  // Cloth that moves the way a real flag does in a light breeze: no flapping, a slow sway of the whole flag and a soft
  // ripple running from the pole to the free edge, its size growing towards the free edge.
  function cloth(x0, y0, w, h, t, seed, dir, fillFn) {
    var sway = Math.sin(t * .45 + seed) * .06 + Math.sin(t * .17 + seed * 2) * .04;
    var pts = [], n = 6;
    for (var k = 0; k <= n; k++) {
      var u = k / n, ripple = Math.sin(t * 1.7 + seed - u * 2.4) * .07 * u * h;
      pts.push([x0 + dir * u * w * (1 - .04 * u), y0 + ripple + sway * u * h + .1 * u * u * h]);
    }
    fillFn(pts, h);
  }
  // Corner flag seen from above (top view, like the rest of the pitch): the white top of the pole and the yellow cloth
  // seen edge-on, hanging out away from the pitch and swaying slowly in the breeze.
  // In a strong wind the cloth streams out downwind, stretched and rippling fast; in still air it hangs limp, shorter
  // seen from above, and only sways.
  function cornerFlag(x, y, s, t, i, ax, ay) {
    var wind = WIND.speed > .5, base = WIND.screenAngle, len = (wind ? 1.4 : 1.05) * s, wid = .42 * s, n = 6; // a breeze at least
    var swayAmp = wind ? .07 : .16, rate = wind ? 4.2 : 1.7, bendAmp = wind ? .1 : .12;
    var ang = base + Math.sin(t * (wind ? 1.3 : .45) + i * 1.7) * swayAmp + Math.sin(t * .17 + i * 3.4) * .1;
    var dx = Math.cos(ang), dy = Math.sin(ang), nx = -dy, ny = dx, top = [], bot = [];
    for (var k = 0; k <= n; k++) {
      var u = k / n, bend = Math.sin(t * rate + i * 1.7 - u * 2.4) * bendAmp * u * s, w = wid * (1 - .25 * u);
      var cx = x + dx * u * len + nx * bend, cy = y + dy * u * len + ny * bend;
      top.push([cx + nx * w / 2, cy + ny * w / 2]); bot.push([cx - nx * w / 2, cy - ny * w / 2]);
    }
    function strip(ox, oy) { ctx.beginPath(); top.forEach(function (q) { ctx.lineTo(q[0] + ox, q[1] + oy); }); for (var k2 = n; k2 >= 0; k2--) ctx.lineTo(bot[k2][0] + ox, bot[k2][1] + oy); ctx.closePath(); }
    var so = shadowOffset(); ctx.fillStyle = "rgba(0,0,0," + (.22 * so.a) + ")"; strip(so.dx * s, so.dy * s); ctx.fill(); // shadow on the grass
    ctx.fillStyle = "#ffd21f"; strip(0, 0); ctx.fill();
    ctx.strokeStyle = "rgba(150,100,0,.6)"; ctx.lineWidth = Math.max(.7, .04 * s); ctx.stroke();
    ctx.beginPath(); ctx.arc(x, y, Math.max(1.6, .2 * s), 0, Math.PI * 2); ctx.fillStyle = "#f6f6f4"; ctx.fill();
    ctx.lineWidth = Math.max(.6, .05 * s); ctx.strokeStyle = "rgba(0,0,0,.45)"; ctx.stroke();
  }
  // The assistant's flag, raised only when he signals (as before): black handle, checked orange / neon-yellow cloth.
  // The flag points the way the team awarded the free kick or throw-in attacks (left or right), as the officials signal it.
  function assistantFlag(x, y, s, t, fdir) {
    var fx = x + fdir * .3 * s;
    ctx.strokeStyle = "#111"; ctx.lineWidth = Math.max(1.5, .15 * s); ctx.beginPath(); ctx.moveTo(fx, y - .5 * s); ctx.lineTo(fx, y - 2.6 * s); ctx.stroke();
    var wind = WIND.speed > .5, dir = fdir;
    cloth(fx, y - 2.6 * s, 1.6 * s, 1.1 * s, t * (wind ? 3.2 : 1.6), 3.1, dir, function (pts, hh) {
      // four quadrants: neon yellow top-left / bottom-right, orange top-right / bottom-left
      for (var qx = 0; qx < 2; qx++) for (var qy = 0; qy < 2; qy++) {
        ctx.fillStyle = (qx + qy) % 2 ? "#ff5a1f" : "#e4f53c"; ctx.beginPath();
        var a0 = qx * 3, a1 = qx * 3 + 3;
        for (var k = a0; k <= a1; k++) ctx.lineTo(pts[k][0], pts[k][1] + qy * hh / 2);
        for (var k2 = a1; k2 >= a0; k2--) ctx.lineTo(pts[k2][0], pts[k2][1] + (qy + 1) * hh / 2);
        ctx.closePath(); ctx.fill();
      }
      ctx.strokeStyle = "#ff5a1f"; ctx.lineWidth = Math.max(.8, .07 * s); ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]);
      for (var k3 = 1; k3 < pts.length; k3++) ctx.lineTo(pts[k3][0], pts[k3][1]);
      for (var k4 = pts.length - 1; k4 >= 0; k4--) ctx.lineTo(pts[k4][0], pts[k4][1] + hh);
      ctx.closePath(); ctx.stroke();
    });
  }
  // The referee holds a card up above his head: yellow, or red.
  function refereeCard(x, y, s, card) {
    var w = .9 * s, h = 1.25 * s, cx = x + .45 * s, top = y - 3.4 * s;
    ctx.strokeStyle = "#121212"; ctx.lineWidth = Math.max(1.2, .14 * s); ctx.beginPath(); ctx.moveTo(x + .25 * s, y - .5 * s); ctx.lineTo(cx, top + h); ctx.stroke(); // raised arm
    ctx.fillStyle = card === 2 ? "#e0262b" : "#ffd21a"; ctx.fillRect(cx - w / 2, top, w, h);
    ctx.strokeStyle = "rgba(0,0,0,.55)"; ctx.lineWidth = Math.max(1, .06 * s); ctx.strokeRect(cx - w / 2, top, w, h);
  }
  function offsideMark(x, y, s) {
    var size = 1.3 * s, left = x - size / 2, top = y - 2.6 * s;
    for (var cx2 = 0; cx2 < 2; cx2++) for (var cy2 = 0; cy2 < 2; cy2++) { ctx.fillStyle = (cx2 + cy2) % 2 ? "#e53935" : "#ffd21a"; ctx.fillRect(left + cx2 * size / 2, top + cy2 * size / 2, size / 2, size / 2); }
    ctx.strokeStyle = "rgba(0,0,0,.6)"; ctx.lineWidth = Math.max(1, .1 * s); ctx.strokeRect(left, top, size, size);
  }
  // A player on the ground (slide tackle, then getting up): the body lies stretched along the slide, with a streak of
  // churned-up grass behind him while he is still sliding.
  function downPlayer(p, tm, info, x, y) {
    var r = view.s * .95, a = -p.f, c = Math.cos(a), sn = Math.sin(a);
    if (p.sl) {
      var g = ctx.createLinearGradient(x - c * r * 2.4, y - sn * r * 2.4, x, y);
      g.addColorStop(0, "rgba(225,240,205,0)"); g.addColorStop(1, "rgba(225,240,205,.3)");
      ctx.strokeStyle = g; ctx.lineWidth = r * .6; ctx.lineCap = "round";
      ctx.beginPath(); ctx.moveTo(x - c * r * 2.4, y - sn * r * 2.4); ctx.lineTo(x - c * r * .6, y - sn * r * .6); ctx.stroke();
    }
    ctx.save(); ctx.translate(x, y); ctx.rotate(a);
    ctx.fillStyle = "rgba(0,0,0,.45)"; ctx.beginPath(); ctx.ellipse(.15 * view.s, .2 * view.s, r * 1.2, r * .96, 0, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(0, 0, r * 1.22, r * .93, 0, 0, Math.PI * 2);
    ctx.fillStyle = p.gk ? tm.kit.gk : tm.kit.primary; ctx.fill(); ctx.lineWidth = Math.max(1, r * .12); ctx.strokeStyle = p.gk ? tm.kit.gkTrim || tm.kit.secondary : tm.kit.secondary; ctx.stroke();
    ctx.restore();
    ctx.fillStyle = p.gk ? tm.kit.gkNumber : tm.kit.number; ctx.font = "700 " + Math.max(8, r * 1.05) + "px Barlow, system-ui, sans-serif";
    fillC(String(info.number), x, y);
    if (p.bk) { ctx.fillStyle = "#ffd23f"; ctx.fillRect(x + r * .55, y - r * 1.05, r * .32, r * .45); }
  }
  // An injured player (playing on): a small white square with a red cross, opposite the card marker.
  function injuryMark(x, y, r) {
    var w = r * .56, cx = x - r * .8, cy = y - r * .85;
    ctx.fillStyle = "#fff"; ctx.fillRect(cx - w / 2, cy - w / 2, w, w);
    ctx.fillStyle = "#e0262b"; ctx.fillRect(cx - w * .09, cy - w * .34, w * .18, w * .68); ctx.fillRect(cx - w * .34, cy - w * .09, w * .68, w * .18);
  }
  // Who wears shirt slot i at the current time: substitutes take over the slot of the player they replace.
  var SUBS = {};
  function infoAt(i) {
    var base = teams[i < 11 ? 0 : 1].players[i % 11], list = SUBS[i];
    if (!list) return base;
    var cur = base;
    for (var k = 0; k < list.length && list[k].t <= t; k++) cur = list[k];
    return cur;
  }
  // Shadows follow the light: one long, sharp shadow from the low sun, a faint soft one under a grey sky, and four faint
  // ones at night, one from each floodlight tower.
  var TOWERS = [[-60, -42], [-60, 42], [60, -42], [60, 42]];
  function shadowOffset() {
    if (SET.night) return { dx: .1, dy: .12, a: .7 };
    if (SET.weather === "Sunny") return { dx: .6, dy: .45, a: 1.05 };
    return { dx: .1, dy: .14, a: .62 };
  }
  function castShadow(wx, wy, x, gy, rx, ry, alpha) {
    var s = view.s;
    if (SET.night) {
      TOWERS.forEach(function (L) {
        var dx = wx - L[0], dy = wy - L[1], l = Math.hypot(dx, dy) || 1, ux = dx / l, uy = -dy / l;
        ctx.fillStyle = "rgba(0,0,0," + (alpha * .32) + ")"; ctx.beginPath();
        ctx.ellipse(x + ux * .55 * s, gy + uy * .55 * s, rx * 1.25, ry * .8, Math.atan2(uy, ux), 0, Math.PI * 2); ctx.fill();
      });
      return;
    }
    var o = shadowOffset();
    ctx.fillStyle = "rgba(0,0,0," + Math.min(.8, alpha * o.a) + ")"; ctx.beginPath();
    if (SET.weather === "Sunny") ctx.ellipse(x + o.dx * s, gy + o.dy * s, rx * 1.3, ry * .88, Math.atan2(o.dy, o.dx), 0, Math.PI * 2);
    else ctx.ellipse(x + o.dx * s, gy + o.dy * s, rx * 1.02, ry * 1.02, 0, 0, Math.PI * 2);
    ctx.fill();
  }
  // A player: a plain circle in the shirt colour with a thin black edge and the number (the design's player marker).
  // the edge is the team's trim colour (black by default, as in the design); the boots have their own colour
  var BOOTS = "#e9ecef";
  // Two small boots peeking out under the circle, pointing where the player faces (static, as in the design).
  // The feet sit close under the front edge of the circle, a little back. A kick or a pass swings the kicking foot: back
  // (backswing), forward through the ball, then settles - nobody "runs" in place. side -1 is the left foot.
  // An official's feet point the way he moves (an assistant running along the touchline faces along it); standing still, the
  // referee watches the ball and an assistant faces the pitch.
  var offPrev = [null, null, null], offFace = [0, Math.PI / 2, -Math.PI / 2];
  function officialFacing(i, o, S) {
    var pv = offPrev[i], want = i === 0 ? Math.atan2(S.by - o.y, S.bx - o.x) : Math.atan2(-o.y, 0);
    if (pv) { var dx = o.x - pv[0], dy = o.y - pv[1]; if (Math.hypot(dx, dy) > .04) want = Math.atan2(dy, dx); }
    offPrev[i] = [o.x, o.y];
    var d = want - offFace[i]; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
    offFace[i] += d * .25;
    return offFace[i];
  }
  function playerFeet(x, y, r, facing, kick, kickLeft, boots, heel, open) {
    var a = -facing, c = Math.cos(a), sn = Math.sin(a);
    ctx.fillStyle = boots || BOOTS; ctx.strokeStyle = "rgba(0,0,0,.45)"; ctx.lineWidth = Math.max(.6, r * .06);
    [-1, 1].forEach(function (side) {
      var fwd = .8 + ((side < 0) === !!kickLeft ? (heel ? -(kick || 0) : (kick || 0)) : 0), sp = open ? .95 : .32, fx = x + c * r * fwd - sn * r * sp * side, fy = y + sn * r * fwd + c * r * sp * side; // (a nutmeg: his legs are open, the ball goes between them)
      ctx.beginPath(); ctx.ellipse(fx, fy, r * .3, r * .21, a, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    });
  }
  var KICK_OFF = [0, -.55, .55, .18];  // foot offset (x radius) in the phases of the swing: none, backswing, strike, follow-through
  function playerDisc(x, y, r, fill, edge) { var lw = Math.max(1.2, r * .12); ctx.beginPath(); ctx.arc(x, y, r - lw / 2, 0, Math.PI * 2); ctx.fillStyle = fill; ctx.fill(); ctx.lineWidth = lw; ctx.strokeStyle = edge; ctx.stroke(); }
  // What a player shows with his arms and his face, drawn on the disc: an arm out (the call to the line, "here!", a protest),
  // a sign in a small bubble (! ? ... + star heart), sound waves for the keeper's shout, a ring for the celebration.
  // code = kind * 100000 + fade (99 -> 0) * 1000 + angle in degrees (world, as the engine has it).
  var GEST = {
    1: { arm: 1.7, col: "#ffffff", sign: "" },
    2: { arm: 1.35, col: "#ffd23f", sign: "!", up: true },
    3: { waves: true, col: "#ffffff", sign: "" },
    4: { arm: 1.5, col: "#ff6b57", sign: "!" },
    5: { col: "#9fb4d8", sign: "\u2026", head: true },
    6: { arm: 1.2, arm2: true, col: "#ff9f43", sign: "?" },
    7: { arm: 1.1, col: "#6fdc8c", sign: "+" },
    8: { ring: true, col: "#ffd23f", sign: "\u2605", up: true, vee: true },
    9: { ring: true, col: "", sign: "\u2665" }
  };
  function gestureMark(code, tm, x, y, r, now) {
    var kind = Math.floor(code / 100000), d = GEST[kind]; if (!d) return;
    var fade = Math.floor(code / 1000) % 100 / 99, ang = (code % 1000) * Math.PI / 180, prog = 1 - fade;
    var pop = Math.min(1, prog * 7), al = Math.min(1, fade * 4) * pop, ca = Math.cos(ang), sa = -Math.sin(ang);
    var col = d.col || tm.kit.primary;
    ctx.save(); ctx.globalAlpha = al; ctx.lineCap = "round"; ctx.strokeStyle = col; ctx.fillStyle = col; ctx.lineWidth = Math.max(1.6, r * .26);
    if (d.ring) { // the pulse round the celebrating player
      var k = (now * 1.6) % 1; ctx.globalAlpha = al * (1 - k) * .8; ctx.lineWidth = Math.max(1.4, r * .22);
      ctx.beginPath(); ctx.arc(x, y, r * (1.25 + k * 1.1), 0, Math.PI * 2); ctx.stroke(); ctx.globalAlpha = al;
    }
    if (d.vee) { // both arms up
      [-.9, .9].forEach(function (s) { var a2 = -Math.PI / 2 + s; ctx.beginPath(); ctx.moveTo(x + Math.cos(a2) * r * .95, y + Math.sin(a2) * r * .95); ctx.lineTo(x + Math.cos(a2) * r * 1.75, y + Math.sin(a2) * r * 1.75); ctx.stroke(); });
    }
    if (d.arm) { // an arm out towards the angle (the second one for a double gesture)
      [0, d.arm2 ? .75 : null, d.arm2 ? -.75 : null].forEach(function (off, ix) {
        if (off === null || (ix === 0 && d.arm2)) return;
        var cs = Math.cos(Math.atan2(sa, ca) + off), sn = Math.sin(Math.atan2(sa, ca) + off), L = r * d.arm * (d.up ? .95 : 1);
        ctx.beginPath(); ctx.moveTo(x + cs * r * .9, y + sn * r * .9); ctx.lineTo(x + cs * (r * .9 + L), y + sn * (r * .9 + L)); ctx.stroke();
        ctx.beginPath(); ctx.arc(x + cs * (r * .9 + L), y + sn * (r * .9 + L), r * .26, 0, Math.PI * 2); ctx.fill();
      });
    }
    if (d.waves) { // the shout
      [1.5, 2.1].forEach(function (rad, ix) { ctx.globalAlpha = al * (ix ? .5 : .9); ctx.beginPath(); ctx.arc(x, y, r * rad, Math.atan2(sa, ca) - .5, Math.atan2(sa, ca) + .5); ctx.stroke(); });
      ctx.globalAlpha = al;
    }
    if (d.head) { // hands on the head
      ctx.beginPath(); ctx.arc(x - r * .55, y - r * .15, r * .24, 0, Math.PI * 2); ctx.arc(x + r * .55, y - r * .15, r * .24, 0, Math.PI * 2); ctx.fill();
    }
    if (d.sign) { // the sign in its bubble, up and to the right of the disc
      var bx = x + r * 1.25, by = y - r * 1.7, br = r * .85;
      ctx.globalAlpha = al * .94; ctx.fillStyle = "#12182a"; ctx.beginPath(); ctx.arc(bx, by, br, 0, Math.PI * 2); ctx.fill();
      ctx.lineWidth = Math.max(1.2, r * .16); ctx.strokeStyle = col; ctx.stroke();
      ctx.globalAlpha = al; ctx.fillStyle = col; ctx.font = "800 " + Math.max(8, r * 1.05) + "px Barlow, system-ui, sans-serif"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
      ctx.fillText(d.sign, bx, by + r * .04);
    }
    ctx.restore();
  }
  // The keeper's dive: his disc stretches along the line of the dive and squashes a little across it (a stretch with the arms
  // reaching, no gloves, no trail), and eases back to the round disc as he lands - it does not snap. k: 0 (standing) .. 1 (at full stretch).
  var diveK = {}, diveAng = {};
  function keeperDive(p, tm, x, y, gy, r, k, ang) {
    var col = tm.kit.gk, trim = tm.kit.gkTrim || tm.kit.secondary;
    ctx.save();
    ctx.globalAlpha = .35 * (.6 + .4 * k); ctx.fillStyle = "#000"; ctx.beginPath(); ctx.ellipse(x + r * .25, gy + r * .35, r * (1 + .5 * k), r * (1 - .3 * k), ang, 0, Math.PI * 2); ctx.fill(); // the shadow stays on the grass
    ctx.globalAlpha = 1; ctx.lineCap = "round"; ctx.strokeStyle = col; ctx.lineWidth = r * .36;
    if (k > .2) [-.25, .25].forEach(function (o) { ctx.beginPath(); ctx.moveTo(x + Math.cos(ang + o) * r * .8, y + Math.sin(ang + o) * r * .8); ctx.lineTo(x + Math.cos(ang + o * .4) * r * (.8 + 1.15 * k), y + Math.sin(ang + o * .4) * r * (.8 + 1.15 * k)); ctx.stroke(); }); // the arms, reaching
    ctx.fillStyle = col; ctx.strokeStyle = trim; ctx.lineWidth = Math.max(1, r * .12);
    ctx.beginPath(); ctx.ellipse(x, y, r * (1 + .3 * k), r * (1 - .14 * k), ang, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    ctx.restore();
  }
  function entities(S, now, dt) {
    var s = view.s, r = .95 * s;
    [[-52.5, -34], [-52.5, 34], [52.5, -34], [52.5, 34]].forEach(function (c, i) { cornerFlag(X(c[0]), Y(c[1]), s, now, i, X(c[0] + Math.sign(c[0])) - X(c[0]), Y(c[1] + Math.sign(c[1])) - Y(c[1])); });
    S.os.forEach(function (o, i) {
      var ox = X(o.x), oy = Y(o.y);
      if (i > 0 && o.flag) assistantFlag(ox, oy, s, now, o.fdir);
      castShadow(o.x, o.y, ox, oy, r, r * .9, .45);
      var rc = refColours();
      // the referee watches the ball, the assistants face the pitch from their touchline
      // (the same marker as a player: same size, same feet, same edge)
      playerFeet(ox, oy, r, officialFacing(i, o, S), 0, false, rc.boots);
      playerDisc(ox, oy, r, rc.shirt, rc.trim);
      ctx.fillStyle = rc.letter; ctx.font = "800 " + Math.max(8, 1.05 * s) + "px Barlow, system-ui, sans-serif"; fillC(i === 0 ? "R" : "L", ox, oy);
      if (i === 0 && o.card) refereeCard(ox, oy, s, o.card);
    });
    S.ps.forEach(function (p, i) {
      if (p.off && Math.abs(p.y) > 35.5) return;
      var tm = teams[i < 11 ? 0 : 1], info = infoAt(i), x = X(p.x), gy = Y(p.y), jh = Math.min(1, Math.max(0, p.j)), y = gy - jh * (p.gk ? 3 : 2.2) * s, g = 1 + jh * (p.gk ? .2 : .1), sh = 1 - jh * .6;
      if (p.sl || p.gr) { downPlayer(p, tm, info, x, gy); if (p.inj) injuryMark(x, gy, r); return; }
      if (p.gk) {
        // the stretch comes on quickly and goes off slowly (about a quarter of a second): no snap back to the disc
        var kk = diveK[i] || 0;
        if (p.dv) { kk = Math.min(1, kk + dt / .09); if (Math.hypot(p.vx, p.vy) > 0.02) diveAng[i] = Math.atan2(-p.vy, p.vx); else if (diveAng[i] === undefined) diveAng[i] = p.f + Math.PI / 2; }
        else kk = Math.max(0, kk - dt / .28);
        diveK[i] = kk;
        if (kk > .02) { keeperDive(p, tm, x, y, gy, r * g, kk, diveAng[i]); return; }
      }
      castShadow(p.x, p.y, x, gy, r * 1.05 * sh, r * .95 * sh, .5 * sh);
      playerFeet(x, y, r * g, p.f, p.kp, p.kl, p.gk ? tm.kit.gkBoots || tm.kit.boots : tm.kit.boots, p.kh, p.no);
      playerDisc(x, y, r * g, p.gk ? tm.kit.gk : tm.kit.primary, p.gk ? tm.kit.gkTrim || tm.kit.secondary : tm.kit.secondary);
      ctx.fillStyle = p.gk ? tm.kit.gkNumber : tm.kit.number; ctx.font = "700 " + Math.max(8, r * g * 1.05) + "px Barlow, system-ui, sans-serif";
      fillC(String(info.number), x, y);
      if (p.bk) { ctx.fillStyle = "#ffd23f"; ctx.fillRect(x + r * .55, y - r * 1.05, r * .32, r * .45); }
      if (p.inj) injuryMark(x, y, r);
      if (p.g && SET.gest !== "0") gestureMark(p.g, tm, x, y, r * g, now);
      if (inShade(p.x, p.y)) { ctx.fillStyle = "rgba(10,24,40,.3)"; ctx.beginPath(); ctx.arc(x, y, r * g * 1.1, 0, Math.PI * 2); ctx.fill(); }
    });
    if (S.fig) {
      var ft = teams[S.fig.team], fx = X(S.fig.x), fy = Y(S.fig.y);
      castShadow(S.fig.x, S.fig.y, fx, fy, r * 1.05, r * .95, .5);
      playerFeet(fx, fy, r, S.fig.f, 0, false, ft.kit.boots);
      playerDisc(fx, fy, r, ft.kit.primary, ft.kit.secondary);
      ctx.fillStyle = ft.kit.number; ctx.font = "700 " + Math.max(8, r * 1.05) + "px Barlow, system-ui, sans-serif"; fillC(String(S.fig.number), fx, fy);
    }
    S.ps.forEach(function (p) { if (p.os) offsideMark(X(p.x), Y(p.y), s); });
    var h = Math.max(0, S.bz - .11), bx = X(S.bx), by = Y(S.by);
    var bsr = .42 * s * Math.max(.45, 1 - h * .05); castShadow(S.bx, S.by, bx, by, bsr, bsr, Math.max(.18, .6 - h * .04));
    yellowBall = SET.weather === "Snow";
    spinBall(S, h, dt, S.phase !== 0 || (S.owner >= 0 && (S.ps[S.owner].gk || S.bz > .4))); // none while held or while play is stopped
    drawBall(bx, by - Math.min(20, h) * .55 * s, .38 * s * (1 + Math.min(20, h) * .07));
    if (S.dead) {
      // the old ball rolls on behind the line until it stops (the game goes on with a new one)
      var dh = Math.max(0, S.dead.z - .11), dx = X(S.dead.x), dy = Y(S.dead.y), dr = .42 * s * Math.max(.45, 1 - dh * .05);
      castShadow(S.dead.x, S.dead.y, dx, dy, dr, dr, Math.max(.18, .6 - dh * .04));
      drawBall(dx, dy - Math.min(20, dh) * .55 * s, .38 * s * (1 + Math.min(20, dh) * .07));
    }
    drawNet(-1, S.bx, S.by, netFx(t, -1)); drawNet(1, S.bx, S.by, netFx(t, 1));
    goalFrame(-1); goalFrame(1);
    if (S.owner >= 0 && S.phase === 0) {
      var o = S.ps[S.owner], tm2 = teams[S.owner < 11 ? 0 : 1], inf = infoAt(S.owner), label = inf.shortName;
      ctx.font = "600 " + Math.max(11, s) + "px Barlow, system-ui, sans-serif"; var lm = ctx.measureText(label), tw = lm.actualBoundingBoxRight + lm.actualBoundingBoxLeft, th = lm.actualBoundingBoxAscent + lm.actualBoundingBoxDescent, lx = X(o.x), ly = Y(o.y) + r * 2.2;
      ctx.fillStyle = "rgba(5,8,6,.75)"; ctx.fillRect(lx - tw / 2 - .5 * s, ly - th / 2 - .32 * s, tw + s, th + .64 * s); ctx.fillStyle = "#fff"; fillC(label, lx, ly);
    }
  }
