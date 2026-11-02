  // ---- drawing ---------------------------------------------------------------------------------------------------
  function esc(s) { return String(s == null ? "" : s).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); }
  function levelColour(v) { return v >= 75 ? "#3cc84b" : v >= 60 ? "#9bd23c" : v >= 45 ? "#e7cf2e" : v >= 30 ? "#f39a1e" : "#e2372f"; }
  function jerseySvg(fill, ink, number, w) {
    return '<svg viewBox="0 0 60 44" aria-hidden="true"><path d="M18 6 L24 3 Q30 8 36 3 L42 6 L52 13 L47 21 L42 18 L42 42 L18 42 L18 18 L13 21 L8 13 Z" fill="' + fill + '" stroke="rgba(0,0,0,.35)" stroke-width="1"/>' +
      '<text x="30" y="33" text-anchor="middle" font-family="Barlow, sans-serif" font-weight="800" font-size="' + (w || 15) + '" fill="' + ink + '">' + esc(number) + '</text></svg>';
  }
  // The stamina heart: it empties from the top as the energy goes (and its colour goes from green to red with it).
  var heartN = 0, HEART = "M12 21 C5 15 1 11.5 1 7 C1 3.5 3.8 1 7 1 C9.2 1 11 2.2 12 4 C13 2.2 14.8 1 17 1 C20.2 1 23 3.5 23 7 C23 11.5 19 15 12 21 Z";
  function heartSvg(v) {
    v = Math.max(0, Math.min(100, v)); var id = "hc" + (++heartN), top = (1 + 20 * (1 - v / 100)).toFixed(2);
    return '<svg class="heart" viewBox="0 0 24 22" aria-label="Energy ' + Math.round(v) + '%"><defs><clipPath id="' + id + '"><rect x="0" y="' + top + '" width="24" height="22"/></clipPath></defs>' +
      '<path d="' + HEART + '" fill="#47506a"/><path d="' + HEART + '" fill="' + levelColour(v) + '" clip-path="url(#' + id + ')"/>' +
      '<path d="M4 11 H8 L10 7 L13 15 L15 10 H20" fill="none" stroke="rgba(10,14,24,.55)" stroke-width="1.3" stroke-linejoin="round"/></svg>';
  }
  function cardHtml(p, slotAbbr, state) {
    var kit = R.home.kit, gk = slotAbbr === "GK", en = state && state.energy != null ? state.energy : 100;
    var flags = "";
    if (state && state.booked) flags += '<i class="y"></i>';
    if (state && state.off) flags += '<i class="r"></i>';
    if (state && state.inj) flags += '<i class="inj"></i>';
    return '<span class="jersey">' + jerseySvg(gk ? kit.gk : kit.primary, gk ? kit.gkNumber : kit.number, p.number) + '</span>' +
      '<span class="flag">' + flags + '</span><span class="nm">' + esc(p.shortName) + '</span><span class="ps"><span>' + esc(slotAbbr) + '</span><span>' + Math.round(en) + '%</span></span>' +
      '<span class="en"><b style="width:' + en + '%;background:' + levelColour(en) + '"></b></span>' + (state && state.rated ? honoursHtml(p.number) : "");
  }
  function stateOf(n) {
    var i = indexOf(n), s = lastS && i >= 0 ? lastS.ps[i] : null;
    return { energy: (KICKED || MODE !== "live") && i >= 0 ? energyOf(n) : 100, booked: s && s.bk, off: s && s.off, inj: s && s.inj, rated: KICKED || MODE !== "live" };
  }
  function drawCards() {
    if (!R) return;
    var f = myFormation(), on = displayLineup(), inEl = $("cardsIn"), outEl = $("cardsOut"), html = "";
    on.forEach(function (n, slot) {
      var p = byNum(n); if (!p) return;
      var pend = isPendingIn(n), abbr = f ? f.slots[slot].abbr : p.role;
      html += '<button type="button" class="pcard2' + (pend ? " pending" : "") + '" data-num="' + n + '" data-slot="' + slot + '"' + (pend ? ' data-locked="1"' : "") + ' aria-pressed="' + (!!SEL && SEL.num === n) + '" data-tip="' + esc(p.name + " · " + abbr) + '|' + esc(pend ? "Comes on at the next stoppage" : ROLES[ord(slot)[0]][0] + " · click another player to swap, drag to move, click twice for the profile") + '">' + cardHtml(p, abbr, pend ? null : stateOf(n)) + '</button>';
    });
    inEl.innerHTML = html; html = "";
    benchShown().forEach(function (b) {
      var p = b.p;
      html += '<button type="button" class="pcard2' + (b.gone ? " gone" : "") + (b.leaving ? " pending" : "") + '" data-num="' + p.number + '" data-bench="1"' + (b.gone ? ' data-gone="1"' : "") + (b.leaving ? ' data-locked="1"' : "") + ' aria-pressed="' + (!!SEL && SEL.num === p.number) + '" data-tip="' + esc(p.name + " · " + p.role) + '|' + esc(b.gone ? "Substituted - cannot return" : b.leaving ? "Goes off at the next stoppage" : "Click a player on the pitch to bring him on, or drag him onto one") + '">' + cardHtml(p, p.role, { energy: 100, rated: b.gone }) + '</button>';
    });
    outEl.innerHTML = html;
    var max = R.rules ? R.rules.maxSubs : 5, win = R.rules ? R.rules.windows : 3;
    var staged = PENDING.filter(function (q) { return q.staged; }).length, sent = PENDING.length - staged;
    $("subInfo").innerHTML = "Substitutions <b>" + changesUsed() + "/" + max + "</b> · Opportunities <b>" + windowsUsed() + "/" + win + "</b>" + (sent ? " · <b>" + sent + " waiting</b> for the next stoppage" : "") + (staged ? " · <b>" + staged + " planned</b>" : "");
    $("subConfirm").hidden = !staged;
    $("subOk").textContent = staged > 1 ? "Confirm " + staged + " changes" : "Confirm change";
    $("subHint").textContent = prematch() ? "Before kick-off: pick a player, then a bench player to change your starting eleven - or another player to swap places. You can also drag."
      : canChange() ? "Pick a player on the pitch, then a bench player (or drag one onto the other); plan all your changes, then Confirm - they are made together at the next stoppage (one opportunity). Half-time changes are free."
      : MODE === "live" ? "The match is over." : "Substitutions are made in a live match. Here a change of eleven replays this match with it.";
  }