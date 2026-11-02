  // ---- positions moved by hand on the board: the role follows the spot (as Formation.SlotAt in the engine) -----------
  // a player dropped on an empty spot of the board: his position moves there (and his role, if it no longer fits)
  function moveOnBoard(slot, ev) {
    var svg = $("fBoard"), m = svg.getScreenCTM(); if (!m) return;
    var pt = svg.createSVGPoint(); pt.x = ev.clientX; pt.y = ev.clientY; pt = pt.matrixTransform(m.inverse());
    if (pt.x < 0 || pt.x > 68 || pt.y < 0 || pt.y > 105) return;
    var E2 = effTac(), ws = .82 + .36 * E2.width / 100, up = (E2.line - 50) * .14;
    var fw = Math.max(-46, Math.min(6, (100 - pt.y - up) / 80 * 52.5 - 52.5)), lat = Math.max(-31, Math.min(31, (34 - pt.x) / (ws * .98)));
    var was = (myFormation().slots[slot] || {}).abbr, now = slotAt(fw, lat);
    if (!PLAN.pos) PLAN.pos = [];
    PLAN.pos[slot] = [Math.round(fw * 10) / 10, Math.round(lat * 10) / 10];
    var o = ord(slot).slice();
    if (KIND_ROLES[now.kind].indexOf(o[0]) < 0) { o[0] = 0; if (!PLAN.orders) { PLAN.orders = []; for (var i = 0; i < 11; i++) PLAN.orders.push(ord(i).slice()); } PLAN.orders[slot] = o; }
    planChanged(was !== now.abbr ? "Moved: " + was + " → " + now.abbr : "Position moved");
  }
  function slotAt(fw, lat) {
    var al = Math.abs(lat), right = lat < 0, side = function (r, l) { return right ? r : l; };
    if (fw < -27) return al > 16 ? { f: fw, l: lat, line: 1, kind: 2, abbr: side("RB", "LB") } : { f: fw, l: lat, line: 1, kind: 1, abbr: "CB" };
    if (fw < -19.5) return al > 16 ? { f: fw, l: lat, line: 1, kind: 2, abbr: side("RWB", "LWB") } : { f: fw, l: lat, line: 2, kind: 3, abbr: "DM" };
    if (fw < -11) return al > 16 ? { f: fw, l: lat, line: 2, kind: 6, abbr: side("RM", "LM") } : { f: fw, l: lat, line: 2, kind: 4, abbr: "CM" };
    if (fw < -3.5) return al > 14 ? { f: fw, l: lat, line: 2, kind: 6, abbr: side("RW", "LW") } : { f: fw, l: lat, line: 2, kind: 5, abbr: "AM" };
    return al > 12 ? { f: fw, l: lat, line: 2, kind: 6, abbr: side("RW", "LW") } : { f: fw, l: lat, line: 3, kind: 7, abbr: "ST" };
  }
  function myFormation() {
    var base = formationOf(PLAN.formation); if (!base || !PLAN.pos) return base;
    return { name: base.name, slots: base.slots.map(function (sl, i) { var q = PLAN.pos[i]; return i > 0 && q ? slotAt(q[0], q[1]) : sl; }) };
  }
  function posText() { var a = []; if (PLAN.pos) PLAN.pos.forEach(function (q, i) { if (q && i > 0) a.push(i + ":" + q[0].toFixed(1) + ":" + q[1].toFixed(1)); }); return a.join("/"); }
  var KIND_NAMES = ["Goalkeeper", "Centre-back", "Full-back", "Defensive midfielder", "Central midfielder", "Attacking midfielder", "Winger", "Striker"];
  // individual instructions: [label, tip] by the engine's Amount (0 less, 1 mixed, 2 more), shown more-first
  var INSTR = [
    { key: 1, name: "Long shots", tip: "How often he shoots from outside the box (18 m+).", opts: [[0, "Less", "Works it closer before shooting."], [1, "Mixed", "Shoots from distance when it is on."], [2, "Often", "Has a go from distance whenever he can."]] },
    { key: 2, name: "Dribbling", tip: "How often he runs with the ball and takes players on.", opts: [[0, "Less", "Moves the ball on quickly instead."], [1, "Mixed", "Dribbles when there is space."], [2, "Often", "Takes his man on and carries the ball forward."]] },
    { key: 3, name: "Aggression", tip: "How he goes into tackles.", opts: [[0, "Gentle", "Stays on his feet and avoids risky tackles - fewer fouls."], [1, "Mixed", "Picks his moments."], [2, "Aggressive", "Goes into every challenge and to ground more - wins it back, but more fouls and cards."]] },
  ];
  var TAKERS = [["Corners", 9, "Crossing", "takes the corners"], ["Free kicks", 20, "Free Kicks", "takes the free kicks at goal"], ["Penalties", 21, "Penalty", "takes the penalties"]];
  function spDefault() { return { ctr: [], ctl: [], fkr: [], fkl: [], pen: [], crr: 0, crl: 0, czr: [0, 0, 0, 0, 0, 0], czl: [0, 0, 0, 0, 0, 0], cpa: [], cpd: [] }; }
  function spotsText(l) { return (l || []).map(function (q) { return q.n + ":" + q.x.toFixed(1) + ":" + q.y.toFixed(1); }).join("/"); }
  var ROUTINES = [[0, "Varied", "Changes from corner to corner, like a real team."], [1, "Near post", "Whipped low to the near post for a flick-on."], [2, "Far post", "Floated to the far post for the tall players."], [3, "Penalty spot", "Driven to the penalty spot - the central runners arrive."], [4, "Pulled back", "Cut back to the edge of the box for a shot."], [5, "Short", "Played short to start a move."]];
  var ZONES = ["Near post", "Far post", "Penalty spot", "Six-yard centre", "Edge of the box", "Rebound / recycle"];
  var PLAN = (function () {
    var p = { formation: "4-4-2", slots: null, orders: null, sp: spDefault() };
    try { var st = JSON.parse(localStorage.getItem("sv-plan") || "{}"); if (st && typeof st.formation === "string") { p.formation = st.formation; p.slots = st.slots || null; p.orders = st.orders || null; p.sp = Object.assign(spDefault(), st.sp || {}); } } catch (e) {}
    return p;
  })();
  // Only in a live match is the home team yours (USER_PLAN, USER_TAC: saved). In a staged or a random match the panel shows
  // the team the home manager picked (from the match header) and cannot be changed.
  var USER_PLAN = PLAN; USER_TAC = TAC;
  function viewPlan() {
    return { formation: R.home.formation || "4-4-2", slots: startLineup(), sp: spDefault(),
             orders: (R.home.orders || []).map(function (o) { return String(o).split(".").map(Number); }) };
  }
  function viewTac() { var o = {}, h = R.home.tactics || {}; for (var k in TAC_DEFAULT) o[k] = k in h ? +h[k] : TAC_DEFAULT[k]; return o; }
  function viewOnlyNote() { toast(MODE === "random" ? "Random match: both managers pick their own team - you pick yours in a live match" : "Staged match: the squads play as they are - you pick your team in a live match"); }
  var KICKED = false, PENDING = [], SEL = null, PI_SLOT = 1, planTimer = 0;
  function savePlan() { if (PLAN !== USER_PLAN) return; try { localStorage.setItem("sv-plan", JSON.stringify(PLAN)); } catch (e) {} }
  function ord(i) { return (PLAN.orders && PLAN.orders[i]) || [0, 1, 1, 1]; }
  function planString() {
    var c = tacEngine(), o = [];
    for (var i = 0; i < 11; i++) o.push(ord(i).join("."));
    var parts = ["formation=" + PLAN.formation, "orders=" + o.join(","), "ctr=" + PLAN.sp.ctr.join("."), "ctl=" + PLAN.sp.ctl.join("."), "fkr=" + PLAN.sp.fkr.join("."), "fkl=" + PLAN.sp.fkl.join("."), "pen=" + PLAN.sp.pen.join("."), "crr=" + PLAN.sp.crr, "crl=" + PLAN.sp.crl, "czr=" + PLAN.sp.czr.join("."), "czl=" + PLAN.sp.czl.join("."), "cpa=" + spotsText(PLAN.sp.cpa), "cpd=" + spotsText(PLAN.sp.cpd), "pos=" + posText(),
                 "line=" + c.line, "width=" + c.width, "passing=" + c.passing, "tempo=" + c.tempo, "aggression=" + c.aggression,
                 "defence=" + c.defence, "attack=" + c.attack, "special=" + c.special];
    if (PLAN.slots) parts.push("slots=" + PLAN.slots.join(","));
    return parts.join(";");
  }
  function formationOf(name) { var f = null; (R && R.formations || []).forEach(function (x) { if (x.name === name) f = x; }); return f || (R && R.formations ? R.formations[0] : null); }
  function squad() { return R ? R.home.players.concat(R.home.bench || []) : []; }
  function byNum(n) { var p = null; squad().forEach(function (q) { if (q.number === n) p = q; }); return p; }
  // The eleven as the header starts the match (shirt number per slot).
  function startLineup() { var a = []; R.home.players.forEach(function (p, i) { a[p.slot != null ? p.slot : i] = p.number; }); return a; }
  // Substitutions so far (team 0): who came on for whom, and when.
  function homeChanges(upTo) {
    var out = [];
    for (var i = 0; i < E.length && (upTo == null || E[i].t <= upTo); i++) {
      var e = E[i];
      if (e.type !== "Substitution" || e.team !== 0 || !e.s) continue;
      var s = String(e.s).split("|");
      out.push({ t: e.t, on: +s[0], off: +s[2] });
    }
    return out;
  }
  // Who plays where now: the plan in a live match (it follows every change), the start plus the changes otherwise.
  function lineupNow() {
    if (MODE === "live" && KICKED && PLAN.slots) return PLAN.slots;
    var a = (PLAN.slots && !KICKED && MODE === "live") ? PLAN.slots.slice() : startLineup();
    homeChanges(t).forEach(function (c) { var k = a.indexOf(c.off); if (k >= 0) a[k] = c.on; });
    return a;
  }
  // the frame index (0-10) of a home player by shirt number now, for his energy, cards and injury
  function indexOf(n) { for (var i = 0; i < 11; i++) if (R && infoAt(i).number === n) return i; return -1; }
  function energyOf(n) {
    var i = indexOf(n); if (i < 0) return null;
    var ST = R.stats || [], row = null;
    for (var q = ST.length - 1; q >= 0; q--) if (ST[q][0] / 100 <= t) { row = ST[q]; break; }
    return row ? row[15 + i] : 100;
  }