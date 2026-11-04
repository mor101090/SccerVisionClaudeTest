  // ---- interaction: click two players, or drag one onto the other ---------------------------------------------------
  function descr(el) {
    if (!el) return null;
    if (el.classList.contains("pcard2") || el.tagName === "TR") return { num: +el.dataset.num, slot: el.dataset.slot !== undefined ? +el.dataset.slot : -1, bench: el.dataset.bench === "1", gone: el.dataset.gone === "1", locked: el.dataset.locked === "1" };
    if (el.classList.contains("fd")) { var s = +el.dataset.slot, n = displayLineup()[s]; return { num: n, slot: s, bench: false, gone: false, locked: isPendingIn(n) }; }
    return null;
  }
  // The one place where two players are put together: two on the pitch swap positions, one on the pitch and one from the
  // bench is a substitution (made at the next stoppage in a live match, at once in the line-up before kick-off).
  function dropOn(a, b) {
    if (!a || !b || a.num === b.num) return;
    if (a.locked || b.locked) { toast("That change is waiting for the next stoppage"); return; }
    if (a.gone || b.gone) { toast("A substituted player cannot come back"); return; }
    if (!a.bench && !b.bench) { swapSlots(a.slot, b.slot); return; }
    if (a.bench && b.bench) return;
    var onPitch = a.bench ? b : a, sub = a.bench ? a : b;
    if (canChange()) requestChange(onPitch.num, sub.num);
    else if (editableLineup()) benchSwap(onPitch.slot, sub.num);
    else if (MODE !== "live") viewOnlyNote();
    else toast("The match is over");
  }
  var lastCard = { num: -1, at: 0 }, dragEndAt = 0;
  var DRAGGING = false;
  function pickEl(el) {
    if (performance.now() - dragEndAt < 250) return; // (the click that ends a drag)
    var d = descr(el); if (!d) return;
    var now = performance.now();
    // a second click on the same card within a moment opens his page (the cards are redrawn between clicks, so by hand)
    if (lastCard.num === d.num && now - lastCard.at < 380) { lastCard.num = -1; SEL = null; drawManager(); openProfile(byNum(d.num), 0); return; }
    lastCard = { num: d.num, at: now };
    if (d.locked) { toast("That change is waiting for the next stoppage"); return; }
    if (!SEL) { if (d.gone) { toast("A substituted player cannot come back"); return; } SEL = d; drawManager(); return; }
    if (SEL.num === d.num) { SEL = null; drawManager(); return; }
    var a = SEL; SEL = null; dropOn(a, d); drawManager();
  }
  ["cardsIn", "cardsOut", "fDots"].forEach(function (id) {
    $(id).addEventListener("click", function (ev) { var el = ev.target.closest(".pcard2, .fd, #sheet tr"); if (el) pickEl(el); });
  });
  // drag and drop (mouse and pen; on a touch screen the two clicks do the same)
  (function () {
    var drag = null, ghost = null, over = null;
    function clearOver() { if (over) { over.classList.remove("drop"); over = null; } }
    function targetAt(x, y, except) { var els = document.elementsFromPoint(x, y); for (var i = 0; i < els.length; i++) { var t = els[i].closest && els[i].closest(".pcard2, .fd, #sheet tr"); if (t && t !== except) return t; } return null; }
    ["cardsIn", "cardsOut", "fDots", "sheet"].forEach(function (id) {
      $(id).addEventListener("pointerdown", function (ev) {
        if (ev.pointerType === "touch" || (ev.pointerType === "mouse" && ev.button !== 0)) return;
        var el = ev.target.closest(".pcard2, .fd, #sheet tr"); if (!el) return;
        var d = descr(el); if (!d || d.locked || d.gone) return;
        drag = { el: el, d: d, x0: ev.clientX, y0: ev.clientY, on: false };
      });
    });
    document.addEventListener("pointermove", function (ev) {
      if (!drag) return;
      if (!drag.on) {
        if (Math.hypot(ev.clientX - drag.x0, ev.clientY - drag.y0) < 7) return;
        drag.on = true; DRAGGING = true; SEL = null;
        ghost = drag.el.cloneNode(true); ghost.classList.add("ghost"); ghost.removeAttribute("id");
        if (drag.el.classList.contains("fd")) {
          // a board dot: the same circle (his shirt, his number, his name) comes along under the mouse, centred on it, at the size it has on the
          // board - so it is plain that he is being carried; the dot he left behind stays as a faint, dashed circle
          var pl = byNum(drag.d.num) || {}, m = $("fBoard").getScreenCTM(), dia = Math.max(30, 7.2 * (m ? m.a : 6));
          ghost = document.createElement("div"); ghost.className = "ghost fdghost" + (drag.el.classList.contains("gk") ? " gk" : "");
          ghost.style.width = ghost.style.height = dia + "px"; ghost.dataset.r = String(dia / 2);
          ghost.innerHTML = "<b>" + esc(pl.number) + "</b><i>" + esc(pl.shortName || "") + "</i>";
        }
        else ghost.style.width = drag.el.offsetWidth + "px";
        document.body.appendChild(ghost); drag.el.classList.add("dragging"); $("tip").hidden = true;
      }
      var r = +ghost.dataset.r || 0; // (a board circle is centred on the mouse; a card hangs a little off it)
      ghost.style.left = (ev.clientX + (r ? -r : 8)) + "px"; ghost.style.top = (ev.clientY + (r ? -r : 8)) + "px";
      var t = targetAt(ev.clientX, ev.clientY, drag.el);
      if (t !== over) { clearOver(); if (t) { var td = descr(t); if (td && !td.locked && !td.gone) { over = t; t.classList.add("drop"); } } }
    });
    function end(ev, drop) {
      if (!drag) return;
      var d = drag; drag = null;
      if (!d.on) return;
      DRAGGING = false;
      dragEndAt = performance.now();
      var t = drop ? targetAt(ev.clientX, ev.clientY, d.el) : null;
      if (ghost) { ghost.remove(); ghost = null; }
      d.el.classList.remove("dragging"); clearOver();
      if (t) { dropOn(d.d, descr(t)); drawManager(); }
      else if (drop && d.el.classList.contains("fd") && d.d.slot > 0) moveOnBoard(d.d.slot, ev);
    }
    document.addEventListener("pointerup", function (ev) { end(ev, true); });
    document.addEventListener("pointercancel", function (ev) { end(ev, false); });
  })();
  $("sheet").addEventListener("click", function (ev) { if (performance.now() - dragEndAt < 250) return; var tr = ev.target.closest("tr"); if (tr) openProfile(byNum(+tr.dataset.num), 0); });
  $("formation").addEventListener("change", function () { setFormation(this.value); });
  $("piList").addEventListener("click", function (ev) { var b = ev.target.closest("button"); if (b) { PI_SLOT = +b.dataset.slot; drawManager(); } });
  $("piDetail").addEventListener("click", function (ev) {
    var b = ev.target.closest("button"); if (!b) return;
    if (b.dataset.profile) { openProfile(byNum(+b.dataset.profile), 0); return; }
    var o = ord(PI_SLOT).slice();
    if (b.dataset.role != null) { if (o[0] === +b.dataset.role) return; o[0] = +b.dataset.role; }
    else if (b.dataset.ins != null) { if (o[+b.dataset.ins] === +b.dataset.v) return; o[+b.dataset.ins] = +b.dataset.v; }
    else return;
    if (!PLAN.orders) { PLAN.orders = []; for (var i = 0; i < 11; i++) PLAN.orders.push(ord(i).slice()); }
    PLAN.orders[PI_SLOT] = o;
    planChanged("Instruction sent to " + ((byNum(lineupNow()[PI_SLOT]) || {}).shortName || "the player"));
  });
  document.querySelectorAll(".sp").forEach(function (el) {
    el.addEventListener("click", function (ev) {
      var b = ev.target.closest("button"); if (!b) return;
      var list = b.closest("[data-sp-list]"); if (!list) return;
      var key = list.getAttribute("data-sp-list"), L = PLAN.sp[key];
      if (b.hasAttribute("data-sp-auto")) { if (!L.length) return; PLAN.sp[key] = []; planChanged("Takers: Auto"); return; }
      var n = +b.getAttribute("data-sp-pick"), i = L.indexOf(n);
      if (i >= 0) L.splice(i, 1); else L.push(n);
      planChanged("Taker queue: " + L.map(function (x) { return (byNum(x) || {}).shortName; }).join(", "));
    });
    el.addEventListener("change", function (ev) {
      var t = ev.target, add = t.getAttribute("data-sp-add"), rt = t.getAttribute("data-sp-routine"), zn = t.getAttribute("data-sp-zone");
      if (add) { if (t.value) { PLAN.sp[add].push(+t.value); planChanged("Taker added"); } }
      else if (rt) { PLAN.sp[rt] = +t.value; planChanged("Corner routine"); }
      else if (zn) {
        // one player, one zone: choosing him again moves whoever was here to the zone he leaves
        var z = zn.split(":"), Z = PLAN.sp[z[0]], i = +z[1], n = +t.value, j = n > 0 ? Z.indexOf(n) : -1;
        if (j >= 0 && j !== i) Z[j] = Z[i];
        Z[i] = n; planChanged("Corner positions");
      }
    });
  });
  document.querySelectorAll(".mtabs button").forEach(function (b) {
    b.addEventListener("click", function () {
      document.querySelectorAll(".mtabs button").forEach(function (o) { o.setAttribute("aria-selected", String(o === b)); });
      document.querySelectorAll(".mtab").forEach(function (p) { p.hidden = p.getAttribute("data-pane") !== b.dataset.tab; });
    });
  });
  $("kickoff").addEventListener("click", function () {
    if (!R || KICKED) return;
    KICKED = true; $("prematch").hidden = true;
    resetTacticsPanel(); drawManager();
    var mine = token;
    showLineups(function () {
      if (mine !== token) return;
      if (ONLINE === "host") NET.whenGuestReady(function () { if (mine === token) worker.postMessage({ cmd: "go", token: token }); }); // (both managers ready)
      else worker.postMessage({ cmd: "go", token: token });
    }); // (the line-ups, then the teams walk out)
  });