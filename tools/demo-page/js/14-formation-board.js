  // ---- the formation board: the shape, and what the chosen tactics do (arrows) ------------------------------------
  var DOTS_POS = {};
  function drawBoard() {
    if (!R) return;
    var f = myFormation(); if (!f) return;
    var E2 = effTac(), on = displayLineup(), html = "", ws = .82 + .36 * E2.width / 100, up = (E2.line - 50) * .14;
    var ys = f.slots.map(function (sl, i) { return 100 - (sl.f + 52.5) / 52.5 * 80 - (i === 0 ? 0 : up); });
    // the defensive line: a dashed line across the back four. A high backline stands on the halfway line with the whole
    // team compact in front of it (midfield 10-15 m ahead, forwards up to 30 m), as in the reference.
    var defYs = f.slots.map(function (sl, i) { return sl.line === 1 ? ys[i] : null; }).filter(function (v) { return v !== null; });
    var defY = defYs.reduce(function (a, b) { return a + b; }, 0) / defYs.length;
    if (TAC.defence === 4) {
      var dy = defY - 54;
      f.slots.forEach(function (sl, i) { if (i === 0) return; ys[i] -= sl.line === 1 ? dy : sl.line === 2 ? dy * .78 : dy * .35; });
      defY = 54;
    }
    var pos = f.slots.map(function (sl, i) {
      var x = 34 - sl.l * ws * .98;
      return { x: Math.max(7, Math.min(61, x)), y: Math.max(8, Math.min(100, ys[i])), line: sl.line, i: i };
    });
    html += '<line class="dl" x1="2" x2="66" y1="' + defY.toFixed(1) + '" y2="' + defY.toFixed(1) + '"/><text class="dlt" transform="translate(2.9 ' + (defY - 2.4).toFixed(1) + ') rotate(-90)">' + (TAC.defence === 4 ? "High line defence" : "Defensive line") + '</text>';
    html += tacArrows(pos, defY);
    var now = {};
    f.slots.forEach(function (sl, i) {
      var p = byNum(on[i]) || { number: "?", shortName: "" }, q = pos[i], pend = isPendingIn(on[i]);
      var nm = esc(p.shortName), w = Math.max(8, nm.length * 1.45 + 2.4);
      now[on[i]] = { x: q.x, y: q.y };
      html += '<g class="fd' + (i === 0 ? " gk" : "") + (pend ? " pend" : "") + (SEL && SEL.num === on[i] ? " sel" : "") + '" data-slot="' + i + '" data-num="' + on[i] + '" style="transform:translate(' + q.x.toFixed(2) + 'px,' + q.y.toFixed(2) + 'px)" data-tip="' + esc((p.name || nm) + " · " + sl.abbr) + '|' + esc(pend ? "Comes on at the next stoppage" : ROLES[ord(i)[0]][0]) + '">' +
        '<circle r="3.6"/><text class="n" text-anchor="middle" y="1.2">' + esc(p.number) + '</text><rect x="' + (-w / 2).toFixed(2) + '" y="4.2" width="' + w.toFixed(2) + '" height="3.6" rx=".8"/><text class="l" text-anchor="middle" y="6.9">' + nm + '</text></g>';
    });
    $("fDots").innerHTML = html;
    // players who changed places glide there (from where they stood a moment ago)
    document.querySelectorAll("#fDots .fd").forEach(function (g) {
      var o = DOTS_POS[g.dataset.num], n = now[g.dataset.num];
      if (o && n && (Math.abs(o.x - n.x) + Math.abs(o.y - n.y)) > .5 && g.animate)
        g.animate([{ transform: "translate(" + o.x.toFixed(2) + "px," + o.y.toFixed(2) + "px)" }, { transform: "translate(" + n.x.toFixed(2) + "px," + n.y.toFixed(2) + "px)" }], { duration: 480, easing: "cubic-bezier(.3,.7,.2,1)" });
    });
    DOTS_POS = now;
  }
  // What the chosen tactics do, drawn on the board as the manager's arrows: how the ball moves (passing, tempo), where it
  // goes (attack), pressing, the offside trap, the high line and the keeper, inverted full-backs, and the extra style.
  function tacArrows(pos, defY) {
    var D = [], M = [], A = [], gk = pos[0], html = "", dur = [3.2, 1.8, .9][TAC.tempo];
    pos.forEach(function (q, i) { if (i === 0) return; (q.line === 1 ? D : q.line === 2 ? M : A).push(q); });
    [D, M, A].forEach(function (L) { L.sort(function (a, b) { return a.x - b.x; }); });
    var at = function (L, k) { return L.length ? L[Math.max(0, Math.min(L.length - 1, k))] : gk; };
    var last = function (L) { return at(L, L.length - 1); };
    var flow = function (a, b) {
      var dx = b.x - a.x, dy = b.y - a.y, L = Math.hypot(dx, dy) || 1, ux = dx / L, uy = dy / L, r = 4.4;
      var x1 = a.x + ux * r, y1 = a.y + uy * r, x2 = b.x - (b.line != null ? ux * r : ux * .6), y2 = b.y - (b.line != null ? uy * r : uy * .6);
      var mx = (x1 + x2) / 2 - uy * L * .12, my = (y1 + y2) / 2 + ux * L * .12;
      return '<path class="arrow" d="M' + x1.toFixed(1) + " " + y1.toFixed(1) + " Q" + mx.toFixed(1) + " " + my.toFixed(1) + " " + x2.toFixed(1) + " " + y2.toFixed(1) + '" stroke-dasharray="2.4 1.3" style="animation: tacflow ' + dur + 's linear infinite"/>';
    };
    var P = function (x, y) { return { x: x, y: y }; };
    var segs = [];
    // passing: how the ball moves
    if (TAC.passing === 0) segs = [[at(D, 1), at(M, 1)], [at(M, 1), at(M, 2)], [at(M, 2), at(A, 0)], [at(A, 0), at(M, 1)]];
    else if (TAC.passing === 1) segs = [[at(D, 2), at(M, 2)], [at(M, 2), last(A)], [at(M, 0), at(A, 0)]];
    else segs = [[at(M, 0), P(at(M, 0).x - 2, 9)], [last(M), P(last(M).x + 2, 9)], [at(D, 1), at(A, 0)]]; // the wide men go long, in behind
    // the chosen attack: crosses from the wide men into the box, runs in behind, shots from distance
    if (TAC.attack === 1) segs = [[at(M, 0), P(27, 13)], [last(M), P(41, 13)], [at(D, 0), at(M, 0)], [last(D), last(M)]];
    else if (TAC.attack === 2) segs = [[at(M, 1), P(at(M, 1).x - 4, 8)], [at(M, 2), P(at(M, 2).x + 4, 8)], [at(D, 1), at(M, 1)]];
    else if (TAC.attack === 3) segs = [[at(M, 1), P(30, 21)], [at(M, 2), P(38, 21)], [at(A, 0), at(M, 1)]];
    // the extra style
    if (TAC.special === 1) segs = [[at(D, 1), at(D, 2)], [at(D, 2), at(M, 1)], [at(M, 1), at(M, 2)], [at(M, 2), at(D, 2)]];
    else if (TAC.special === 2) segs = [[at(D, 1), at(D, 2)], [at(D, 2), at(D, 1)], [gk, at(D, 1)]];
    else if (TAC.special === 3) segs = [[at(M, 1), P(at(M, 1).x - 3, 7)], [at(M, 2), P(at(M, 2).x + 3, 7)], [at(D, 2), at(M, 2)]];
    else if (TAC.special === 4) segs = [[at(M, 0), P(27, 10)], [last(M), P(41, 10)], [at(M, 1), P(33, 12)]];
    segs.forEach(function (sg) { html += flow(sg[0], sg[1]); });
    // pressing: arcs in front of the players who press (aggression, the high press, relentless pressure)
    var presses = TAC.style === 2 || TAC.defence === 1 ? A.concat(M.slice(1, M.length - 1)) : TAC.style === 1 ? A.slice(0, 1) : [];
    if (TAC.special === 4) presses = A.concat(M);
    presses.forEach(function (q) { html += '<path class="press" d="M' + (q.x - 4.6).toFixed(1) + " " + (q.y - 3.2).toFixed(1) + " A5.4 5.4 0 0 1 " + (q.x + 4.6).toFixed(1) + " " + (q.y - 3.2).toFixed(1) + '"/>'; });
    // the offside trap and the high backline: the whole line steps up together
    if (TAC.defence === 2 || TAC.defence === 4) D.forEach(function (q) { html += '<path class="step" d="M' + (q.x - 1.8).toFixed(1) + " " + (q.y - 5.2).toFixed(1) + " l1.8 -2.2 l1.8 2.2" + '"/>'; });
    if (TAC.defence === 4) html += '<path class="sweep" d="M' + gk.x.toFixed(1) + " " + (gk.y - 4.6).toFixed(1) + " L" + gk.x.toFixed(1) + " " + (defY + 4).toFixed(1) + '"/>'; // the keeper sweeps behind it
    if (TAC.defence === 5 && D.length >= 2) [D[0], last(D)].forEach(function (q) { html += flow(q, P(q.x < 34 ? q.x + 9 : q.x - 9, q.y - 9)); }); // the full-backs come inside
    if (TAC.defence === 3) [at(M, 0), last(M)].forEach(function (q) { html += flow(q, P(q.x < 34 ? q.x + 7 : q.x - 7, q.y + 4)); }); // the bunker: everybody tucks in
    return html;
  }
  function drawSheet() {
    if (!R) return;
    var f = myFormation(), on = displayLineup(), html = "";
    on.forEach(function (n, i) {
      var p = byNum(n); if (!p) return;
      var pend = isPendingIn(n), en = (KICKED || MODE !== "live") && !pend && indexOf(n) >= 0 ? energyOf(n) : 100;
      html += '<tr data-num="' + n + '" data-slot="' + i + '"' + (pend ? ' data-locked="1"' : "") + (i === PI_SLOT ? ' class="sel"' : "") + '><td class="pos">' + esc(f ? f.slots[i].abbr : p.role) + '</td><td>' + esc(p.name) + (pend ? ' <small class="muted">(on at the next stoppage)</small>' : "") + '</td><td>' + esc(ROLES[ord(i)[0]][0]) +
        '</td><td data-tip="Energy left|' + Math.round(en) + '%">' + heartSvg(en) + '</td><td class="fit-' + esc(p.fitness) + '">' + esc(p.fitness) + '</td><td>' + (KICKED || MODE !== "live" ? honoursHtml(n) : "") + '</td></tr>';
    });
    benchShown().forEach(function (b) {
      html += '<tr class="bench' + (b.gone ? " gone" : "") + '" data-num="' + b.p.number + '" data-bench="1"' + (b.gone ? ' data-gone="1"' : "") + (b.leaving ? ' data-locked="1"' : "") + '><td class="pos">' + esc(b.p.role) + '</td><td>' + esc(b.p.name) + (b.gone ? ' <small>(off)</small>' : b.leaving ? ' <small>(off at the next stoppage)</small>' : "") + '</td><td>' + esc(b.p.role) + '</td><td data-tip="Energy left|100%">' + heartSvg(100) + '</td><td class="fit-' + esc(b.p.fitness) + '">' + esc(b.p.fitness) + '</td><td>' + (b.gone ? honoursHtml(b.p.number) : "") + '</td></tr>';
    });
    $("sheet").innerHTML = html;
  }
  function drawInstructions() {
    if (!R) return;
    var f = myFormation(), on = lineupNow(), list = "";
    on.forEach(function (n, i) {
      var p = byNum(n) || {};
      list += '<li><button type="button" data-slot="' + i + '" aria-pressed="' + (i === PI_SLOT) + '"><b>' + esc(f.slots[i].abbr) + '</b><span>' + esc(p.number) + '</span><span>' + esc(p.shortName) + '<small>' + esc(ROLES[ord(i)[0]][0]) + '</small></span></button></li>';
    });
    $("piList").innerHTML = list;
    var sl = f.slots[PI_SLOT], p = byNum(on[PI_SLOT]) || {}, o = ord(PI_SLOT);
    var html = '<div class="pi-head"><b>' + esc(p.name) + '</b><span>' + esc(sl.abbr) + ' · ' + esc(KIND_NAMES[sl.kind]) + '</span><button type="button" data-profile="' + esc(p.number) + '">Player profile</button></div>';
    html += '<div><div class="tac-label"><span>Role</span><b>' + esc(ROLES[o[0]][0]) + '</b></div><div class="roles">' + KIND_ROLES[sl.kind].map(function (r) {
      return '<button type="button" data-role="' + r + '" aria-pressed="' + (o[0] === r) + '" data-tip="' + esc(ROLES[r][0]) + '|' + esc(ROLES[r][1]) + '">' + esc(ROLES[r][0]) + '<small>' + esc(ROLES[r][1].split(":")[0].split(" - ")[0]) + '</small></button>';
    }).join("") + '</div></div>';
    if (sl.kind !== 0) INSTR.forEach(function (ins) {
      html += '<div class="tac-row"><div class="tac-label"><span data-tip="' + esc(ins.name) + '|' + esc(ins.tip) + '">' + esc(ins.name) + '</span></div><div class="tac-seg" role="group" aria-label="' + esc(ins.name) + '">' + ins.opts.map(function (op) {
        return '<button type="button" data-ins="' + ins.key + '" data-v="' + op[0] + '" aria-pressed="' + (o[ins.key] === op[0]) + '" data-tip="' + esc(ins.name + ": " + op[1]) + '|' + esc(op[2]) + '">' + esc(op[1]) + '</button>';
      }).join("") + '</div></div>';
    });
    $("piDetail").innerHTML = html;
  }
  function spPlayers() { var gk = startLineup()[0]; return squad().filter(function (p) { return p.number !== gk; }); }
  // The old picker: every player with his rating; a click puts him next in the queue (numbered), a click on a numbered one takes him out.
  function spList(key, title, attr, attrName) {
    var L = PLAN.sp[key], on = displayLineup();
    var ps = spPlayers().sort(function (a, b) { return b.attrs[attr] - a.attrs[attr]; });
    var html = '<div class="takers" data-sp-list="' + key + '"><h4>' + esc(title) + '</h4>' +
      '<button type="button" data-sp-auto="1" aria-pressed="' + (L.length === 0) + '"><span></span><span>Auto</span><span></span><span class="v"></span></button>';
    ps.forEach(function (p) {
      var rank = L.indexOf(p.number) + 1, bench = on.indexOf(p.number) < 0;
      html += '<button type="button" data-sp-pick="' + p.number + '" aria-pressed="' + (rank > 0) + '"' + (bench ? ' style="opacity:.6"' : "") + ' data-tip="' + esc(p.name) + '|' + esc(attrName + " " + p.attrs[attr] + (rank ? " - number " + rank + " in the queue (click to remove)" : " - click to add him next in the queue")) + '">' +
        '<span>' + (rank || "") + '</span><span>' + esc(p.name) + (bench ? " (bench)" : "") + '</span><span>' + esc(p.role) + '</span><span class="v" style="color:' + levelColour(p.attrs[attr]) + '">' + p.attrs[attr] + '</span></button>';
    });
    return html + '</div>';
  }
  function spRoutine(key, title) {
    return '<div><h4>' + esc(title) + '</h4><select data-sp-routine="' + key + '">' + ROUTINES.map(function (r) { return '<option value="' + r[0] + '"' + (PLAN.sp[key] === r[0] ? " selected" : "") + ' title="' + esc(r[2]) + '">' + esc(r[1]) + '</option>'; }).join("") + '</select></div>';
  }
  function spZones(key, title) {
    var on = displayLineup().filter(function (n, i) { return i !== 0; });
    var html = '<div><h4>' + esc(title) + '</h4><div class="zones">';
    ZONES.forEach(function (z, i) {
      html += '<label>' + (i + 1) + '. ' + esc(z) + '<select data-sp-zone="' + key + ':' + i + '"><option value="0">Auto</option>' + on.map(byNum).filter(Boolean).sort(function (a, b) { return (b.attrs[19] || 0) - (a.attrs[19] || 0); }).map(function (p) {
        return '<option value="' + p.number + '"' + (PLAN.sp[key][i] === p.number ? " selected" : "") + '>' + esc(p.name) + (p.height ? " · " + p.height + "cm" : "") + '</option>'; }).join("") + '</select></label>';
    });
    return html + '</div></div>';
  }
  function boxSvg() {
    var pts = [[62, 36], [20, 36], [42, 62], [34, 30], [42, 100], [90, 82]];
    return '<svg class="box" viewBox="0 0 120 130" aria-label="Where each zone is in the box"><rect x="2" y="2" width="116" height="126" fill="none" stroke="#fff" stroke-opacity=".5"/><rect x="25" y="2" width="70" height="26" fill="none" stroke="#fff" stroke-opacity=".5"/><rect x="2" y="2" width="116" height="70" fill="none" stroke="#fff" stroke-opacity=".25"/>' +
      pts.map(function (q, i) { return '<circle cx="' + q[0] + '" cy="' + q[1] + '" r="7" fill="#f2c230"/><text x="' + q[0] + '" y="' + (q[1] + 4) + '" text-anchor="middle" font-size="10" font-weight="700" fill="#1b1f2b">' + (i + 1) + '</text>'; }).join("") + '</svg>';
  }