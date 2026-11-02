  // ---- ratings (1-10, live), goals, assists, man of the match -------------------------------------------------------
  var RCACHE = {};
  function numberAt(i, time) { var cur = teams[i < 11 ? 0 : 1].players[i % 11], list = SUBS[i]; if (list) for (var k = 0; k < list.length && list[k].t <= time; k++) cur = list[k]; return cur.number; }
  function finalRecord(n) { var rs = R && R.final && R.final.home && R.final.home.records; if (!rs || t < tEnd - .5) return null; for (var k = 0; k < rs.length; k++) if (rs[k].number === n) return rs[k]; return null; }
  function ratingOf(n) {
    var fr = finalRecord(n); if (fr) return fr.rating;
    var i = indexOf(n);
    if (i >= 0) {
      var ST = R.stats || [], row = null;
      for (var q = ST.length - 1; q >= 0; q--) if (ST[q][0] / 100 <= t) { row = ST[q]; break; }
      var v = row && row.length > 37 + i ? row[37 + i] : 0;
      if (v > 0) { RCACHE[n] = v / 10; return v / 10; }
    }
    return RCACHE[n] != null ? RCACHE[n] : null;
  }
  var tallyMemo = { key: "", v: null };
  function tallies() {
    var key = Math.floor(t * 4) + ":" + (E ? E.length : 0);
    if (tallyMemo.key === key) return tallyMemo.v;
    tallyMemo = { key: key, v: talliesNow() };
    return tallyMemo.v;
  }
  function talliesNow() {
    var g = {}, a = {};
    (E || []).forEach(function (ev) {
      if (ev.t > t || ev.type !== "Goal" || ev.v > .5 || ev.team !== 0) return;
      if (ev.p >= 0 && ev.p < 11) { var n = numberAt(ev.p, ev.t); g[n] = (g[n] || 0) + 1; }
      if (ev.o >= 0 && ev.o < 11) { var m = numberAt(ev.o, ev.t); a[m] = (a[m] || 0) + 1; }
    });
    return { g: g, a: a };
  }
  function ratingColour(r) { return r >= 7.5 ? "#2fbf5b" : r >= 6.8 ? "#8fd14f" : r >= 6.2 ? "#f2c230" : r >= 5.6 ? "#f28c28" : "#e2372f"; }
  // the rating badge, a ball per goal, "A" per assist and a star for the man of the match
  function honoursHtml(n) {
    var r = ratingOf(n), T = tallies(), fr = finalRecord(n), h = "";
    for (var k = 0; k < (T.g[n] || 0); k++) h += '<i class="hg" title="Goal">⚽</i>';
    for (k = 0; k < (T.a[n] || 0); k++) h += '<i class="ha" title="Assist">👟</i>';
    if (fr && fr.motm) h += '<i class="hm" title="Man of the match">★</i>';
    return '<span class="rt"' + (r == null ? ' hidden' : ' style="background:' + ratingColour(r) + '"') + '>' + (r == null ? "" : r.toFixed(1)) + '</span><span class="hon">' + h + '</span>';
  }
  function changesUsed() { return homeChanges(MODE === "live" ? null : t).length; }
  // opportunities: changes at one stoppage count once; at half-time (and the second-half kick-off) none
  function windowsUsed() {
    var n = 0, last = -1e9;
    homeChanges(MODE === "live" ? null : t).forEach(function (c) {
      if (c.t >= secondHalfT - 200 && c.t <= secondHalfT + 3) return; // (the break and the kick-off after it)
      if (c.t - last > 25) n++;
      last = c.t;
    });
    return n;
  }
  function canChange() { return MODE === "live" && KICKED && !finished; }
  function prematch() { return MODE === "live" && !KICKED; }
  function editableLineup() { return prematch(); }
