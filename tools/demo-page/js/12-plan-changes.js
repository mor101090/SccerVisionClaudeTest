  // ---- what changing the plan does: live - to the players now; before kick-off - a new team sheet; otherwise replayed
  function planChanged(what) {
    if (MODE !== "live") { viewOnlyNote(); if (R) { PLAN = viewPlan(); TAC = viewTac(); syncTacUi(); } drawManager(); return; }
    savePlan(); drawManager();
    clearTimeout(planTimer);
    if (MODE === "live" && KICKED) {
      planTimer = setTimeout(function () { if (worker && R) worker.postMessage({ cmd: "plan", token: token, plan: planString() }); toast(what || "Instruction sent to the players"); }, 350);
    } else if (MODE === "live") {
      planTimer = setTimeout(function () { startMatch("live", currentSeed); }, 250);
    } else {
      planTimer = setTimeout(function () { toast("Replaying the match with your changes"); restartMatch(); }, 450);
    }
  }
  function setFormation(name) {
    var from = formationOf(PLAN.formation), to = formationOf(name);
    if (!from || !to || name === PLAN.formation) return;
    // everybody to the nearest place in the new shape (as the engine does); roles that do not fit go back to Auto
    var map = remap(from, to), slots = lineupNow(), ns = [], no = [];
    for (var i = 0; i < 11; i++) {
      ns[map[i]] = slots[i];
      var o = ord(i).slice();
      if (KIND_ROLES[to.slots[map[i]].kind].indexOf(o[0]) < 0) o[0] = 0;
      no[map[i]] = o;
    }
    PLAN.formation = name; PLAN.slots = ns; PLAN.orders = no; PLAN.pos = null; // (a new shape: its own positions)
    planChanged("New formation: " + name);
  }
  function remap(from, to) {
    var map = [0], uo = [true], un = [true];
    for (var n = 1; n < 11; n++) {
      var bo = -1, bn = -1, bd = Infinity;
      for (var i = 1; i < 11; i++) { if (uo[i]) continue; for (var j = 1; j < 11; j++) { if (un[j]) continue;
        var a = from.slots[i], b = to.slots[j], df = a.f - b.f, dl = a.l - b.l, d = df * df + dl * dl + (a.line !== b.line ? 400 : 0);
        if (d < bd) { bd = d; bo = i; bn = j; } } }
      map[bo] = bn; uo[bo] = un[bn] = true;
    }
    return map;
  }
  function swapSlots(a, b) {
    var s = lineupNow().slice();
    if (canChange() && (a === 0 || b === 0)) { toast("The keeper stays in goal during the match"); return; }
    var x = s[a]; s[a] = s[b]; s[b] = x; PLAN.slots = s; // (the orders belong to the position, not the player)
    planChanged("Positions swapped");
  }
  function benchSwap(slot, benchNumber) {
    var s = lineupNow().slice(); s[slot] = benchNumber; PLAN.slots = s;
    planChanged("New starting eleven");
  }
  function requestChange(outN, inN) {
    if (!canChange()) return;
    var left = (R.rules ? R.rules.maxSubs : 5) - changesUsed() - PENDING.length;
    var p = indexOf(outN) >= 0 && lastS ? lastS.ps[indexOf(outN)] : null;
    if (p && p.off) { toast("A sent-off player cannot be replaced"); return; }
    if (left <= 0) { toast("All " + (R.rules ? R.rules.maxSubs : 5) + " substitutions are used"); return; }
    if (PENDING.some(function (q) { return q.out === outN || q.in === inN; })) { toast("That change is already waiting"); return; }
    // staged: nothing is sent until the manager confirms - all the changes go together, at one stoppage (one opportunity)
    PENDING.push({ out: outN, in: inN, staged: true });
    toast("Planned: " + (byNum(inN) || {}).shortName + " for " + (byNum(outN) || {}).shortName + " - confirm to send the changes");
    drawManager();
  }
  function confirmChanges() {
    var n = 0;
    PENDING.forEach(function (q) { if (!q.staged) return; q.staged = false; n++; worker.postMessage({ cmd: "sub", token: token, out: q.out, in: q.in }); });
    if (n) toast(n === 1 ? "The change is made at the next stoppage" : n + " changes are made together at the next stoppage");
    drawManager();
  }
  function cancelChanges() {
    var before = PENDING.length;
    PENDING = PENDING.filter(function (q) { return !q.staged; });
    if (PENDING.length !== before) toast("Changes cancelled");
    drawManager();
  }
  $("subOk").addEventListener("click", confirmChanges);
  $("subCancel").addEventListener("click", cancelChanges);
  function subEvent(e) {
    if (e.team !== 0) return;
    var s = String(e.s || "").split("|");
    if (e.type === "SubstitutionRequest") {
      if (e.v > 0.5) { PENDING = PENDING.filter(function (q) { return !(q.out === +s[0] && q.in === +s[1]); }); toast(e.text); }
    } else {
      var on = +s[0], off = +s[2];
      PENDING = PENDING.filter(function (q) { return q.in !== on; });
      if (MODE === "live" && PLAN.slots) { var k = PLAN.slots.indexOf(off); if (k >= 0) { PLAN.slots[k] = on; } }
    }
    drawManagerSoon();
  }
  var drawSoon = 0;
  function drawManagerSoon() { clearTimeout(drawSoon); drawSoon = setTimeout(drawManager, 120); }

  // What the screen shows: the eleven with the changes that are waiting for a stoppage already made (the bench player in
  // his place, marked), so the manager can go on - the pending players are locked until the whistle.
  function displayLineup() {
    var a = lineupNow().slice();
    PENDING.forEach(function (q) { var k = a.indexOf(q.out); if (k >= 0) a[k] = q.in; });
    return a;
  }
  function isPendingIn(n) { return PENDING.some(function (q) { return q.in === n; }); }
  function isPendingOut(n) { return PENDING.some(function (q) { return q.out === n; }); }
  function benchShown() {
    var on = displayLineup(), gone = homeChanges(MODE === "live" ? null : t).map(function (c) { return c.off; });
    return squad().filter(function (p) { return on.indexOf(p.number) < 0; }).map(function (p) { return { p: p, gone: gone.indexOf(p.number) >= 0, leaving: isPendingOut(p.number) }; });
  }
