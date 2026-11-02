  // ---- corner set-ups: where everyone stands at an attacking and a defending corner -----------------------------------
  // Spots are in the team's own frame (forward towards the goal it attacks, lateral) for a corner taken on the side drawn
  // here; the engine mirrors them for a corner on the other side. Until the manager moves someone, the automatic set-up
  // (the best headers on the near post, far post, spot...; at the back zonal markers, man-markers and the keeper) is shown.
  var CSEL = { cpa: -1, cpd: -1 };
  function cornerAuto(key) {
    var on = displayLineup().map(byNum).filter(Boolean), gk = on[0], out = on.slice(1);
    var head = function (p) { return (p.attrs[19] || 0) + (p.attrs[18] || 0) + ((p.height || 180) - 180) * 1.2; };
    out.sort(function (a, b) { return head(b) - head(a); });
    var spots = [];
    if (key === "cpa") {
      var Z = [[46.8, 2.6], [47.4, -3.2], [42, .5], [48.6, -.3], [35.5, -2], [32, 9], [2, 12], [2, -12], [8, 18], [8, -18]];
      out.forEach(function (p, i) { spots.push({ n: p.number, x: Z[i][0], y: Z[i][1] }); });
    } else {
      var D = [[-51.6, 3.3], [-47.6, 1.5], [-47.6, -2.5], [-46.6, -2.6], [-47, 2.2], [-41.8, -.5], [-48.2, .6], [-36, -4], [-36, 5], [-6, 0]];
      if (gk) spots.push({ n: gk.number, x: -51.6, y: -.6 });
      out.forEach(function (p, i) { spots.push({ n: p.number, x: D[i][0], y: D[i][1] }); });
    }
    return spots;
  }
  // the routine's target spot cannot be moved (it is the routine): near post, far post, penalty spot, pulled back, short
  var ROUTINE_SPOT = { 1: [46.8, 2.6, "Near-post routine"], 2: [47.4, -3.2, "Far-post routine"], 3: [42, .5, "Penalty-spot routine"], 4: [35.5, -2, "Pulled-back routine"], 5: [43.5, 25.5, "Short routine"] };
  function cornerLayout(key) {
    var auto = cornerAuto(key), saved = PLAN.sp[key] || [];
    var L = auto.map(function (a) { var s = saved.filter(function (q) { return q.n === a.n; })[0]; return s ? { n: a.n, x: s.x, y: s.y } : a; });
    if (key === "cpa") {
      [PLAN.sp.crr, PLAN.sp.crl].forEach(function (rt) {
        var spot = ROUTINE_SPOT[rt]; if (!spot) return;
        var best = -1, bd = 1e9;
        L.forEach(function (q, i) { if (q.lock) return; var d = Math.hypot(q.x - spot[0], q.y - spot[1]); if (d < bd) { bd = d; best = i; } });
        if (best >= 0) { L[best].x = spot[0]; L[best].y = spot[1]; L[best].lock = spot[2]; }
      });
    }
    return L;
  }
  // screen: the goal at the top, the corner flag at the top right; 1 unit = 1 m
  function cToScreen(key, x, y) { return key === "cpa" ? [34 + y, 52.5 - x] : [34 + y, x + 52.5]; }
  function cFromScreen(key, sx, sy) { return key === "cpa" ? [52.5 - sy, sx - 34] : [sy - 52.5, sx - 34]; }
  function drawCornerSets() {
    if (!R) return;
    document.querySelectorAll(".cset").forEach(function (el) {
      var key = el.getAttribute("data-cset"), L = cornerLayout(key), kit = R.home.kit, gkNum = displayLineup()[0];
      var svg = '<svg viewBox="-1 -1 70 54" data-key="' + key + '">' +
        '<rect class="ln" x="0" y="0" width="68" height="52.5"/><rect class="ln" x="13.84" y="0" width="40.32" height="16.5"/><rect class="ln" x="24.84" y="0" width="18.32" height="5.5"/>' +
        '<path class="ln" d="M26.7 16.5 A9.15 9.15 0 0 0 41.3 16.5"/><circle class="ln" cx="34" cy="11" r=".25"/>' +
        '<rect x="30.34" y="-1" width="7.32" height="1" fill="#fff" opacity=".85"/>' +
        '<path d="M68 0 l0 -1 l1.4 .5 z" fill="#ffd21a"/><text x="66.4" y="2.2" class="nm">corner</text>';
      L.forEach(function (q, i) {
        var p = byNum(q.n) || {}, sc = cToScreen(key, q.x, q.y), gk = q.n === gkNum;
        svg += '<g data-i="' + i + '"' + (q.lock ? ' data-lock="' + esc(q.lock) + '"' : "") + '><circle class="pl' + (CSEL[key] === i ? " sel" : "") + (q.lock ? " lk" : "") + '" cx="' + sc[0].toFixed(2) + '" cy="' + sc[1].toFixed(2) + '" r="1.35" fill="' + (gk ? kit.gk : kit.primary) + '"/>' +
          '<text x="' + sc[0].toFixed(2) + '" y="' + (sc[1] + .5).toFixed(2) + '" style="fill:' + (gk ? kit.gkNumber : kit.number) + '">' + q.n + '</text>' +
          '<text class="nm" x="' + sc[0].toFixed(2) + '" y="' + (sc[1] + 2.7).toFixed(2) + '">' + esc(p.shortName || "") + '</text></g>';
      });
      svg += '</svg>';
      var sel = CSEL[key], side = '<div class="side"><p class="note">' + (key === "cpa"
        ? "Your corner, taken from the flag at the top right (mirrored for the other side). Drag a player to where he should stand; click him to put someone else there. The taker goes to the flag."
        : "Their corner, from the flag at the top right (mirrored for the other side). Drag your players - keeper, zonal markers, man-markers, who stays up - and click one to change who stands there.") + '</p>';
      if (sel >= 0 && L[sel]) {
        side += '<label>Player in the selected spot <select data-cset-swap="' + key + '">' + displayLineup().map(byNum).filter(Boolean).map(function (p) {
          return '<option value="' + p.number + '"' + (p.number === L[sel].n ? " selected" : "") + '>' + p.number + ' ' + esc(p.name) + ' · ' + (p.height || "") + 'cm · heading ' + (p.attrs[19] || "") + '</option>'; }).join("") + '</select></label>';
      }
      side += '<p class="note">' + ((PLAN.sp[key] || []).length ? "Your own set-up." : "The automatic set-up (the best headers in the danger zones).") + '</p><button type="button" data-cset-reset="' + key + '">Back to automatic</button></div>';
      el.innerHTML = svg + side;
    });
  }
  (function () {
    var drag = null;
    function svgPoint(svg, ev) { var r = svg.getBoundingClientRect(), vb = svg.viewBox.baseVal; return [vb.x + (ev.clientX - r.left) / r.width * vb.width, vb.y + (ev.clientY - r.top) / r.height * vb.height]; }
    function save(key, L) { PLAN.sp[key] = L.map(function (q) { return { n: q.n, x: Math.round(q.x * 10) / 10, y: Math.round(q.y * 10) / 10 }; }); }
    document.querySelectorAll(".cset").forEach(function (el) {
      var key = el.getAttribute("data-cset");
      el.addEventListener("pointerdown", function (ev) {
        var g = ev.target.closest("g[data-i]"); if (!g) return;
        var svg = el.querySelector("svg");
        if (g.hasAttribute("data-lock")) { CSEL[key] = +g.dataset.i; drawCornerSets(); toast(g.getAttribute("data-lock") + ": this spot is the routine - you can change who stands there, not where"); return; }
        drag = { key: key, i: +g.dataset.i, svg: svg, g: g, moved: false, x0: ev.clientX, y0: ev.clientY, L: cornerLayout(key) };
        try { svg.setPointerCapture(ev.pointerId); } catch (e) {}
        ev.preventDefault();
      });
      el.addEventListener("pointermove", function (ev) {
        if (!drag || drag.key !== key) return;
        if (!drag.moved && Math.hypot(ev.clientX - drag.x0, ev.clientY - drag.y0) < 4) return;
        drag.moved = true;
        var pt = svgPoint(drag.svg, ev), sx = Math.max(0, Math.min(68, pt[0])), sy = Math.max(0, Math.min(52.5, pt[1])), w = cFromScreen(key, sx, sy);
        drag.L[drag.i].x = w[0]; drag.L[drag.i].y = w[1];
        drag.g.querySelectorAll("circle").forEach(function (c) { c.setAttribute("cx", sx); c.setAttribute("cy", sy); });
        var tx = drag.g.querySelectorAll("text"); tx[0].setAttribute("x", sx); tx[0].setAttribute("y", sy + .5); tx[1].setAttribute("x", sx); tx[1].setAttribute("y", sy + 2.7);
      });
      el.addEventListener("pointerup", function () {
        if (!drag || drag.key !== key) return;
        var d = drag; drag = null;
        if (d.moved) { save(key, d.L); planChanged(key === "cpa" ? "Corner set-up (attack)" : "Corner set-up (defence)"); }
        else { CSEL[key] = CSEL[key] === d.i ? -1 : d.i; drawCornerSets(); }
      });
      el.addEventListener("change", function (ev) {
        var t = ev.target; if (!t.hasAttribute("data-cset-swap")) return;
        var L = cornerLayout(key), i = CSEL[key], n = +t.value; if (i < 0 || !L[i]) return;
        // the chosen player swaps spots with whoever is there
        var j = -1; L.forEach(function (q, k) { if (q.n === n) j = k; });
        if (j >= 0) { var tmp = L[j].n; L[j].n = L[i].n; L[i].n = tmp; } else L[i].n = n;
        save(key, L); planChanged("Corner set-up");
      });
      el.addEventListener("click", function (ev) {
        var b = ev.target.closest("[data-cset-reset]"); if (!b) return;
        PLAN.sp[key] = []; CSEL[key] = -1; planChanged("Corner set-up: automatic");
      });
    });
  })();
  document.querySelectorAll(".subtabs").forEach(function (bar) {
    bar.addEventListener("click", function (ev) {
      var b = ev.target.closest("button[data-sub]"); if (!b) return;
      bar.querySelectorAll("button").forEach(function (x) { x.setAttribute("aria-selected", String(x === b)); });
      bar.parentNode.querySelectorAll(".subpane").forEach(function (p) { p.hidden = p.getAttribute("data-subpane") !== b.dataset.sub; });
    });
  });
  function drawTakers() {
    if (!R) return;
    var c = document.querySelector('[data-sp="corners"]'), f = document.querySelector('[data-sp="fk"]'), p = document.querySelector('[data-sp="pen"]');
    var keep = document.activeElement && document.activeElement.closest && document.activeElement.closest(".sp");
    if (keep && document.activeElement.tagName === "SELECT") return;
    c.innerHTML = '<p class="note">Click a player to add him next in the queue; the first takes it, and if he is substituted, injured or sent off the next steps up. Auto = the best on the pitch.</p>' +
      '<div class="sp-cols">' + spList("ctr", "Right-side corners (taken from the right corner flag)", 9, "Crossing") + spList("ctl", "Left-side corners", 9, "Crossing") + '</div>' +
      '<div class="sp-cols">' + spRoutine("crr", "Right-side routine") + spRoutine("crl", "Left-side routine") + '</div>' +
      '<div class="sp-cols">' + spZones("czr", "Who stands where - right corners") + spZones("czl", "Who stands where - left corners") + '</div>' + boxSvg() +
      '<p class="note">Zones: 1 near post, 2 far post, 3 penalty spot, 4 six-yard centre, 5 edge of the box, 6 rebound. Put your tall high-jumper at the near post, a quick finisher at the far post. Left Auto = the best headers go first.</p>';
    f.innerHTML = '<p class="note">Direct and crossed free kicks. The first on the list who is still on the pitch takes it.</p><div class="sp-cols">' +
      spList("fkr", "Right-side free kicks", 20, "Free kicks") + spList("fkl", "Left-side free kicks", 20, "Free kicks") + '</div>';
    p.innerHTML = '<p class="note">Click a player to add him next in the queue; the first on the pitch takes the penalty.</p>' + spList("pen", "Penalty takers", 21, "Penalty");
  }
  function drawAll() {
    var sel = $("formation");
    if (sel.options.length !== (R.formations || []).length) sel.innerHTML = (R.formations || []).map(function (f) { return '<option>' + esc(f.name) + '</option>'; }).join("");
    sel.value = PLAN.formation;
    drawCards(); drawBoard(); drawSheet(); drawInstructions(); drawTakers(); drawCornerSets();
  }
  // Two players change places: the cards slide to their new spots (FLIP: draw, then animate from where each card was).
  function drawManager() {
    if (!R || DRAGGING) return;
    var before = {}, cards = document.querySelectorAll("#cardsIn .pcard2, #cardsOut .pcard2");
    cards.forEach(function (c) { before[c.dataset.num] = c.getBoundingClientRect(); });
    drawAll();
    var moved = false;
    document.querySelectorAll("#cardsIn .pcard2, #cardsOut .pcard2").forEach(function (c) {
      var r0 = before[c.dataset.num]; if (!r0 || !c.animate) return;
      var r1 = c.getBoundingClientRect(), dx = r0.left - r1.left, dy = r0.top - r1.top;
      if (Math.abs(dx) + Math.abs(dy) < 3) return;
      moved = true;
      c.animate([{ transform: "translate(" + dx + "px," + dy + "px)", zIndex: 4 }, { transform: "none", zIndex: 4 }], { duration: 440, easing: "cubic-bezier(.3,.7,.2,1)" });
    });
    if (moved) { var sq = document.querySelector(".squad"); sq.classList.add("anim"); clearTimeout(sq._t); sq._t = setTimeout(function () { sq.classList.remove("anim"); }, 480); }
  }
