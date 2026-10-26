audio=open(work+'/audio.iife.js').read()
rep('<script id="replays" type="application/json">','<script>'+audio+'</script>\n<script id="replays" type="application/json">')
rep("  function disc(x, y, r, fill, stroke)", """  function fillC(text, x, y) {
    ctx.textAlign = "left"; ctx.textBaseline = "alphabetic";
    var m = ctx.measureText(text);
    ctx.fillText(text, x - (m.actualBoundingBoxRight - m.actualBoundingBoxLeft) / 2, y + (m.actualBoundingBoxAscent - m.actualBoundingBoxDescent) / 2);
  }
  function disc(x, y, r, fill, stroke)""")
rep('ctx.textAlign = "center"; ctx.textBaseline = "middle"; ctx.fillText(i === 0 ? "R" : "L", X(o.x), Y(o.y) + .5);','fillC(i === 0 ? "R" : "L", X(o.x), Y(o.y));')
rep('ctx.textAlign = "center"; ctx.textBaseline = "middle"; ctx.fillText(String(info.number), x, y + .5);','fillC(String(info.number), x, y);')
rep('<button id="play" type="button">Pause</button>','<button id="play" type="button">Pause</button>\n    <button id="sound" type="button" aria-pressed="false">Sound off</button>')
rep('  var cache = {};', """  var snd = new SVAudio.MatchSound(), soundOn = false, lastT = 0;
  $("sound").addEventListener("click", function () {
    soundOn = !soundOn;
    if (soundOn) snd.enable(); else snd.disable();
    this.textContent = soundOn ? "Sound on" : "Sound off"; this.setAttribute("aria-pressed", String(soundOn));
  });
  var cache = {};""")
rep('t = t0; shown = -1;','t = t0; lastT = t0; shown = -1;')
rep('function jump(to) { t = Math.max(t0, Math.min(tEnd, to)); shown = -1; }','function jump(to) { t = Math.max(t0, Math.min(tEnd, to)); lastT = t; shown = -1; }')
rep('    var S = sample(t), second', """    if (t > lastT) {
      for (var ei = 0; ei < E.length; ei++) { var ev = E[ei]; if (ev.t > t) break; if (ev.t > lastT && speed <= 4) { var sc = sample(ev.t); snd.play({ type: ev.type, whistle: ev.whistle, detail: ev.detail, team: ev.team, homeScore: sc.hs, awayScore: sc.as }); } }
    }
    lastT = t; snd.tick(dt);
    var S = sample(t), second""")
rep('    if (!KEY[e.type]) return;', '    if (!KEY[e.type] && !(e.type === "Shot" && e.detail >= 3) && !(e.type === "FreeKick" && e.detail === 1)) return;')
# ---- turn 5: deforming nets, woodwork flash, name plate, raised flag, pace slider ----
rep('''      ctx.save(); ctx.strokeStyle = "rgba(230,236,230,.45)"; ctx.lineWidth = 1;
      for (var y = -3.66; y <= 3.67; y += .5) line(gl, y, gl + 2 * sd, y);
      for (var d = 0; d <= 2.01; d += .5) line(gl + d * sd, -3.66, gl + d * sd, 3.66);
      ctx.restore(); ctx.save(); ctx.lineWidth = Math.max(2, .22 * s);
      line(gl, -3.72, gl + 2 * sd, -3.72); line(gl, 3.72, gl + 2 * sd, 3.72); line(gl + 2 * sd, -3.72, gl + 2 * sd, 3.72); ctx.restore();''','''      ctx.save(); ctx.lineWidth = Math.max(2, .22 * s); line(gl, -3.72, gl, 3.72); ctx.restore();''')
rep('''  function disc(x, y, r, fill, stroke)''','''  // Goal net from above: a mesh of strings the ball presses out; hard shots make it bulge and ripple, and the
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
    ctx.save(); ctx.strokeStyle = "rgba(235,240,235,.6)";
    ys.forEach(function (y, i) { ctx.lineWidth = i === 0 || i === 24 ? Math.max(1.5, .2 * s) : 1; ctx.beginPath(); ds.forEach(function (d, k) { var p = P(y, d); if (k) ctx.lineTo(p[0], p[1]); else ctx.moveTo(p[0], p[1]); }); ctx.stroke(); });
    ds.forEach(function (d, k) { ctx.lineWidth = k === 7 ? Math.max(1.5, .2 * s) : 1; ctx.beginPath(); ys.forEach(function (y, i) { var p = P(y, d); if (i) ctx.lineTo(p[0], p[1]); else ctx.moveTo(p[0], p[1]); }); ctx.stroke(); });
    if (fx.fl && fx.fl.age < .6) {
      var rr = (1 + fx.fl.age * 2) * s, g = ctx.createRadialGradient(X(fx.fl.x), Y(fx.fl.y), 0, X(fx.fl.x), Y(fx.fl.y), rr);
      g.addColorStop(0, "rgba(255,245,170," + (.9 * (1 - fx.fl.age / .6)) + ")"); g.addColorStop(1, "rgba(255,245,170,0)");
      ctx.fillStyle = g; ctx.beginPath(); ctx.arc(X(fx.fl.x), Y(fx.fl.y), rr, 0, Math.PI * 2); ctx.fill();
    }
    ctx.restore();
  }
  function disc(x, y, r, fill, stroke)''')
rep('''    if (S.owner >= 0 && S.phase === 0) {''','''    drawNet(-1, S.bx, S.by, netFx(t, -1)); drawNet(1, S.bx, S.by, netFx(t, 1));
    if (S.owner >= 0 && S.phase === 0) {''')
rep('''label = "#" + inf.number + " – " + inf.shortName;''','''label = inf.shortName;''')
rep('''var w = ctx.measureText(label).width + 12, lx = X(o.x), ly = Y(o.y) + r * 2.2;
      ctx.fillStyle = "rgba(5,8,6,.75)"; ctx.fillRect(lx - w / 2, ly - .65 * s, w, 1.3 * s); ctx.fillStyle = "#fff"; ctx.fillText(label, lx, ly);''','''var lm = ctx.measureText(label), tw = lm.actualBoundingBoxRight + lm.actualBoundingBoxLeft, th = lm.actualBoundingBoxAscent + lm.actualBoundingBoxDescent, lx = X(o.x), ly = Y(o.y) + r * 2.2;
      ctx.fillStyle = "rgba(5,8,6,.75)"; ctx.fillRect(lx - tw / 2 - .5 * s, ly - th / 2 - .32 * s, tw + s, th + .64 * s); ctx.fillStyle = "#fff"; fillC(label, lx, ly);''')
rep('''      if (i > 0 && o.flag) { ctx.fillStyle = "#f2b31b"; ctx.beginPath(); ctx.moveTo(X(o.x) + .2 * s, Y(o.y) - 1.7 * s); ctx.lineTo(X(o.x) + 1.4 * s, Y(o.y) - 1.25 * s); ctx.lineTo(X(o.x) + .2 * s, Y(o.y) - .8 * s); ctx.fill(); }''','''      if (i > 0 && o.flag) {
        var wv = Math.sin(now * 6) * .15, fx0 = X(o.x) + .3 * s, fy0 = Y(o.y);
        ctx.strokeStyle = "#222"; ctx.lineWidth = Math.max(1.5, .15 * s); ctx.beginPath(); ctx.moveTo(fx0, fy0 - .5 * s); ctx.lineTo(fx0, fy0 - 2.6 * s); ctx.stroke();
        ctx.fillStyle = "#ffd21a"; ctx.beginPath(); ctx.moveTo(fx0, fy0 - 2.6 * s); ctx.lineTo(fx0 + (1.6 + wv) * s, fy0 - (2.2 + wv) * s); ctx.lineTo(fx0, fy0 - 1.6 * s); ctx.fill();
      }''')
rep('''    <input id="seek" type="range"''','''    <label class="pace">Pace <output id="paceOut">100%</output><input id="pace" type="range" min="50" max="100" step="5" value="100" aria-label="Match pace in percent"></label>
    <input id="seek" type="range"''')
rep('''.speeds { display: flex; gap: 4px; }''','''.speeds { display: flex; gap: 4px; }
.pace { display: flex; align-items: center; gap: 8px; font: 600 14px var(--body); color: var(--muted); }
.pace output { color: var(--text); font-variant-numeric: tabular-nums; min-width: 40px; }
.pace input { width: 140px; accent-color: var(--amber); }''')
rep('''if (playing && t < tEnd) t = Math.min(tEnd, t + dt * speed);''','''if (playing && t < tEnd) t = Math.min(tEnd, t + dt * speed * pace / 100);''')
rep('''  var cache = {};''','''  var pace = 100;
  try { pace = +localStorage.getItem("sv.pace") || 100; } catch (err) {}
  $("pace").value = pace; $("paceOut").textContent = pace + "%";
  $("pace").addEventListener("input", function () {
    pace = +this.value; $("paceOut").textContent = pace + "%";
    try { localStorage.setItem("sv.pace", String(pace)); } catch (err) {}
  });
  var cache = {};''')
rep('KEY = { Goal: 1, YellowCard: 1, RedCard: 1, PenaltyAwarded: 1 };','KEY = { Goal: 1, YellowCard: 1, RedCard: 1, PenaltyAwarded: 1, Offside: 1, HitWoodwork: 1 };')
# ---- turn 6: little boots that stick out of the circle and swing as players run ----
rep('''      [-1, 1].forEach(function (k) { ctx.beginPath(); ctx.arc(x + fx * r * .62 - fy * r * .42 * k, y + fy * r * .62 + fx * r * .42 * k, r * .3, 0, Math.PI * 2); ctx.fill(); });''','''      var lp = lastP[i], mv = lp ? Math.hypot(p.x - lp.x, p.y - lp.y) : 0; lastP[i] = { x: p.x, y: p.y };
      if (mv < 3) stepPh[i] = (stepPh[i] || 0) + mv * 2.6;
      var stride = mv > .002 ? Math.sin(stepPh[i] || 0) * .6 * r : 0;
      ctx.strokeStyle = "rgba(255,255,255,.35)"; ctx.lineWidth = Math.max(.5, r * .06);
      [-1, 1].forEach(function (k) { ctx.beginPath(); ctx.arc(x + fx * (r + stride * k) - fy * r * .4 * k, y + fy * (r + stride * k) + fx * r * .4 * k, r * .4, 0, Math.PI * 2); ctx.fill(); ctx.stroke(); });''')
rep('''  function entities(S, now) {''','''  var stepPh = [], lastP = [];
  function entities(S, now) {''')
