  // ---- the line-ups: the managers' starting elevens, substitutes and the manager, before the teams walk out -----------
  var luTimer = 0, luDone = null;
  function lineupCard(tm) {
    var f = formationOf(tm.formation) || formationOf("4-4-2"), kit = tm.kit, bySlot = [];
    tm.players.forEach(function (p, i) { bySlot[p.slot != null ? p.slot : i] = p; });
    var ink = textOn(kit.primary) === "#111" ? "#111111" : "#ffffff";
    var fMin = Math.min.apply(null, f.slots.map(function (q) { return q.f; })), fMax = Math.max.apply(null, f.slots.map(function (q) { return q.f; }));
    var svg = '<svg viewBox="0 0 100 130" aria-hidden="true"><rect class="ln" x="2" y="2" width="96" height="126"/><line class="ln" x1="2" x2="98" y1="65" y2="65"/><circle class="ln" cx="50" cy="65" r="12"/>' +
      '<rect class="ln" x="22" y="2" width="56" height="22"/><rect class="ln" x="36" y="2" width="28" height="9"/><rect class="ln" x="22" y="106" width="56" height="22"/><rect class="ln" x="36" y="119" width="28" height="9"/>';
    // the keeper first, then back to front: each shirt drops in on its spot
    var order = f.slots.map(function (sl, i) { return i; }).sort(function (a, b) { return f.slots[a].f - f.slots[b].f; });
    order.forEach(function (i, k) {
      var sl = f.slots[i], p = bySlot[i]; if (!p) return;
      var x = 50 - sl.l * 1.45, y = 118 - (sl.f - fMin) / Math.max(1, fMax - fMin) * 96, gk = i === 0;
      x = Math.max(11, Math.min(89, x));
      svg += '<g transform="translate(' + (x - 7.5).toFixed(1) + ' ' + (y - 11).toFixed(1) + ')"><g class="sh" style="animation-delay:' + (0.5 + k * 0.11).toFixed(2) + 's"><g transform="scale(.25)">' +
        '<path d="M18 6 L24 3 Q30 8 36 3 L42 6 L52 13 L47 21 L42 18 L42 42 L18 42 L18 18 L13 21 L8 13 Z" transform="scale(1)" fill="' + (gk ? kit.gk : kit.primary) + '" stroke="rgba(0,0,0,.4)" stroke-width="1.2"/>' +
        '<text x="30" y="33" text-anchor="middle" font-family="Barlow, sans-serif" font-weight="800" font-size="17" fill="' + (gk ? kit.gkNumber : kit.number) + '">' + p.number + '</text></g></g></g>' +
        '<text class="nm sh" style="animation-delay:' + (0.6 + k * 0.11).toFixed(2) + 's" x="' + x.toFixed(1) + '" y="' + (y + 7.2).toFixed(1) + '">' + esc(p.shortName) + '</text>';
    });
    svg += '</svg>';
    var d = 0.2, row = function (cls, num, name, extra) { d += 0.07; return '<div class="r ' + cls + '" style="animation-delay:' + d.toFixed(2) + 's"><i>' + num + '</i><span>' + esc(name) + (extra || "") + '</span></div>'; };
    var xi = order.map(function (i) { var p = bySlot[i]; return p ? row("", p.number, p.name, i === 0 ? " (GK)" : "") : ""; }).join("");
    var subs = (tm.bench || []).map(function (p) { return row("sub", p.number, p.name); }).join("");
    var bigShirt = '<svg viewBox="0 0 60 44" aria-hidden="true"><path d="M18 6 L24 3 Q30 8 36 3 L42 6 L52 13 L47 21 L42 18 L42 42 L18 42 L18 18 L13 21 L8 13 Z" fill="' + kit.primary + '" stroke="rgba(0,0,0,.35)" stroke-width=".8"/></svg>';
    return '<div class="lu-col" style="--tc:' + kit.primary + '"><div class="lu-name">' + esc(tm.name) + '</div><h4>Starting XI</h4>' + xi +
      '<h4>Coach</h4><div class="coach"><b>' + esc(tm.manager || "-") + '</b></div><h4>Substitutes</h4>' + subs + '</div>' +
      '<div class="lu-mid">' + svg + '</div>' +
      '<div class="lu-right"><div class="big">' + bigShirt + '</div><div class="fm">' + esc(f.name) + '</div><div class="cmp">' + esc(tm.shortName || "") + '</div></div>';
  }
  function showLineups(done) {
    if (!R) { done(); return; }
    var comp = R.competition === "Cup" ? "Cup match" : R.competition === "LeagueCup" ? "League Cup match" : "League match";
    $("luComp").textContent = comp + " - starting line-ups";
    var homeEl = $("luHome"), awayEl = $("luAway");
    homeEl.innerHTML = lineupCard(R.home); awayEl.innerHTML = lineupCard(R.away);
    homeEl.className = "lu-team"; awayEl.className = "lu-team";
    var el = $("lineups"), bar = $("luBar"), start = performance.now(), each = 7500, len = each * 2;
    el.hidden = false; setPlaying(false);
    clearInterval(luTimer);
    luDone = function () { clearInterval(luTimer); luDone = null; el.hidden = true; homeEl.className = awayEl.className = "lu-team"; setPlaying(true); done(); };
    var shown = -1;
    luTimer = setInterval(function () {
      var k = (performance.now() - start) / len; bar.style.width = Math.min(100, k * 100) + "%";
      var which = k < .5 ? 0 : 1;
      if (which !== shown) { shown = which; (which ? homeEl : awayEl).className = "lu-team out"; var nx = which ? awayEl : homeEl; nx.className = "lu-team"; void nx.offsetWidth; nx.className = "lu-team on"; }
      if (k >= 1 && luDone) luDone();
    }, 100);
  }
  $("luSkip").addEventListener("click", function () { if (luDone) luDone(); });
