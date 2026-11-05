  // ---- player card on hover ----------------------------------------------------------------------------
  // Category averages (mental, defence, playmaking, attacking, athletic) on a pentagon, as on the player page of the
  // web game; height, feet, age and fitness; a few key skills.
  var lastS = null;
  window.__svView = function () { return view; };
  window.__svState = function () { return lastS; }; // (a read-only peek at the frame on screen, for the browser tests)
  function playerCard(i) {
    var tm = teams[i < 11 ? 0 : 1], cur = infoAt(i), base = tm.players[i % 11], p = base;
    if (cur !== base) { (tm.bench || []).forEach(function (b) { if (b.number === cur.number) p = b; }); if (p === base) p = { number: cur.number, name: cur.shortName, role: "SUB" }; }
    var gk = i % 11 === 0 && p === base, c = p.cats || [0, 0, 0, 0, 0, 0];
    var axes = [[gk ? "GK" : "ATT", gk ? c[5] : c[3]], ["ATH", c[4]], ["PLA", c[2]], ["DEF", c[1]], ["MEN", c[0]]], cx = 56, cy = 54, R = 34;
    var pt = function (k, v) { var a = -Math.PI / 2 + k * 2 * Math.PI / 5; return [cx + Math.cos(a) * R * v, cy + Math.sin(a) * R * v]; };
    var poly = function (v) { return axes.map(function (_, k) { return pt(k, typeof v === "number" ? v : v[k]).map(function (n) { return n.toFixed(1); }).join(","); }).join(" "); };
    var svg = '<svg viewBox="0 0 112 104" aria-hidden="true">' + [1, .66, .33].map(function (f) { return '<polygon class="web" points="' + poly(f) + '"/>'; }).join("") +
      '<polygon class="val" points="' + poly(axes.map(function (a) { return Math.max(.05, Math.min(1, (a[1] - 10) / 85)); })) + '"/>' +
      axes.map(function (a, k) { var q = pt(k, 1.3); return '<text x="' + q[0].toFixed(1) + '" y="' + (q[1] + 3).toFixed(1) + '" text-anchor="middle">' + a[0] + " " + a[1] + "</text>"; }).join("") + "</svg>";
    var kv = function (k, v) { return "<div><span>" + k + "</span><b>" + v + "</b></div>"; };
    var el = $("pcard");
    el.style.setProperty("--pc", gk ? tm.kit.gk : tm.kit.primary);
    el.innerHTML = '<div class="pc-top"><b></b><span></span></div><div class="pc-meta"></div><div class="pc-body">' + svg + '<div class="pc-kv">' +
      (gk ? kv("Goalkeeping", c[5]) : kv("Attack", c[3])) + kv("Athletic", c[4]) + kv("Defence", c[1]) + kv("Mental", c[0]) + kv("Playmaking", c[2]) + "</div></div>";
    el.querySelector(".pc-top b").textContent = p.number + " " + (p.name || p.shortName);
    el.querySelector(".pc-top span").textContent = p.role || "";
    var meta = el.querySelector(".pc-meta");
    meta.innerHTML = "";
    if (p.height) {
      [p.height + " cm · " + p.age + " years",
       (p.foot === "Both" ? "Two-footed" : p.foot + "-footed") + " · weak foot " + "★".repeat(Math.max(1, Math.min(5, +p.weakFoot || 1))) + "☆".repeat(5 - Math.max(1, Math.min(5, +p.weakFoot || 1)))].forEach(function (line) { var d = document.createElement("div"); d.textContent = line; meta.appendChild(d); });
      var fit = document.createElement("div"); fit.appendChild(document.createTextNode("Fitness: ")); var fb = document.createElement("b"); fb.className = "fit-" + p.fitness; fb.textContent = p.fitness; fit.appendChild(fb); meta.appendChild(fit);
    }
    var en = document.createElement("div"); en.className = "pc-energy"; en.innerHTML = '<span>Stamina left</span><i><b></b></i><em></em>'; meta.appendChild(en);
    var rt = document.createElement("div"); rt.className = "pc-rating"; rt.innerHTML = '<span>Rating</span> <b></b>'; meta.appendChild(rt);
    el.dataset.slot = String(i);
    updateCardEnergy();
  }
  // How much energy the player has left now (the replay records it every second).
  function updateCardEnergy() {
    var el = $("pcard"), i = +el.dataset.slot, ST = R && R.stats || [], row = null;
    if (el.hidden && el.dataset.i) return;
    for (var q = ST.length - 1; q >= 0; q--) if (ST[q][0] / 100 <= t) { row = ST[q]; break; }
    var v = row && row.length > 15 + i ? row[15 + i] : 100, bar = el.querySelector(".pc-energy b"), num = el.querySelector(".pc-energy em");
    if (!bar) return;
    bar.style.width = v + "%"; bar.style.background = v > 70 ? "#4fd16f" : v > 50 ? "#f2c230" : "#ff6a4a"; num.textContent = v + "%";
    var rb = el.querySelector(".pc-rating b"), rv = row && row.length > 37 + i ? row[37 + i] / 10 : 0;
    if (rb) { rb.textContent = rv > 0 ? rv.toFixed(1) : "-"; rb.style.color = rv > 0 ? ratingColour(rv) : ""; }
  }
  cv.addEventListener("mousemove", function (ev) { if (!TOUCH) cvHover(ev); }); // (no hover card from a tap)
  function cvHover(ev) {
    var el = $("pcard");
    if (!lastS || !R) { el.hidden = true; return; }
    var rect = cv.getBoundingClientRect(), k = cv.width / rect.width, mx = (ev.clientX - rect.left) * k, my = (ev.clientY - rect.top) * k;
    var best = -1, bd = view.s * 1.4;
    lastS.ps.forEach(function (p, i) {
      if (p.off) return;
      if (MODE === "live" && i >= 11) return; // (in a live match the opponents' data is not yours to see)
      var d = Math.hypot(X(p.x) - mx, Y(p.y) - my); if (d < bd) { bd = d; best = i; }
    });
    if (best < 0) { el.hidden = true; el.dataset.i = ""; cv.style.cursor = ""; return; }
    cv.style.cursor = "pointer";
    if (el.dataset.i !== String(best)) { playerCard(best); el.dataset.i = String(best); }
    el.hidden = false;
    var sx = ev.clientX - rect.left, sy = ev.clientY - rect.top, w = el.offsetWidth, h = el.offsetHeight;
    el.style.left = Math.min(rect.width - w - 6, sx + 16) + "px"; el.style.top = Math.max(6, Math.min(rect.height - h - 6, sy - h / 2)) + "px";
  }

  cv.addEventListener("click", function (ev) {
    if (TOUCH) cvHover(ev); // (a tap: find the player under the finger first)
    var el = $("pcard"); if (el.hidden || !el.dataset.i) return;
    var i = +el.dataset.i, tm = teams[i < 11 ? 0 : 1], cur = infoAt(i), p = tm.players[i % 11];
    if (cur !== p) (tm.bench || []).forEach(function (b) { if (b.number === cur.number) p = b; });
    openProfile(p, i < 11 ? 0 : 1);
  });
  cv.addEventListener("mouseleave", function () { $("pcard").hidden = true; $("pcard").dataset.i = ""; });
  resize();