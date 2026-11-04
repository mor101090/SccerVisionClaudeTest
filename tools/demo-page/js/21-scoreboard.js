  // ---- scoreboard and commentary bar ------------------------------------------------------------------
  var possTeam = -1;
  // the league cup bar is one drawing: its texts follow the ordinary scoreboard's
  var lcLast = "";
  function syncLcup() {
    var sb = document.querySelector(".sb"); if (!sb.classList.contains("lcup")) return;
    var key = $("mn").textContent + "|" + $("hn").textContent + "|" + $("sc").textContent + "|" + $("an").textContent + "|" + redCards(0) + redCards(1) + "|" + (R && R.home.kit.primary) + (R && R.away.kit.primary);
    if (key === lcLast) return; lcLast = key;
    $("lcClock").textContent = $("mn").textContent; $("lcHome").textContent = $("hn").textContent; $("lcAway").textContent = $("an").textContent;
    $("lcScore").textContent = $("sc").textContent.replace("-", " - ");
    if (R) { $("lcHomeBolt").setAttribute("fill", R.home.kit.primary); $("lcAwayBolt").setAttribute("fill", R.away.kit.primary); }
    var reds = ""; for (var i = 0; i < redCards(0); i++) reds += '<rect x="' + (124 + i * 13) + '" y="-12" width="10" height="10" fill="#ed1c24"/>'; for (i = 0; i < redCards(1); i++) reds += '<rect x="' + (364 + i * 13) + '" y="-12" width="10" height="10" fill="#ed1c24"/>'; // (above the name plates, like the other two boards)
    $("lcReds").innerHTML = reds;
  }
  function clockText(c) { var m = Math.floor(c / 60), sec = Math.floor(c % 60); return String(m).padStart(2, "0") + ":" + String(sec).padStart(2, "0"); }
  function textOn(hex) { var n = parseInt(hex.slice(1), 16), r = n >> 16, g = (n >> 8) & 255, b = n & 255; return (r * .299 + g * .587 + b * .114) > 160 ? "#111" : "#fff"; }
  function updateBar(S) {
    if (S.owner >= 0) possTeam = S.owner < 11 ? 0 : 1;
    var lastLine = null, goal = null, card = null;
    for (var i = 0; i < E.length; i++) {
      var e = E[i]; if (e.t > t) break;
      if (!e.text || e.type === "Whistle" || e.type === "NetHit" || e.type === "Skill") continue;
      lastLine = e;
      if (e.type === "Goal") goal = e;
      if (e.type === "YellowCard" || e.type === "RedCard") card = e;
    }
    var bg = possTeam >= 0 ? teams[possTeam].kit.primary : "#3c4552", text = lastLine ? lastLine.text : "Kick-off", fg = null;
    if (goal && t - goal.t < 6) {
      // goal: a fast flash in the scoring team's two colours - shirt colour on trim, then trim on shirt colour (the
      // text always in the other one), about seven times a second, measured in real time so it is fast at any speed
      var gk = teams[goal.team === 1 ? 1 : 0].kit, on = Math.floor(performance.now() / 140) % 2 === 1;
      bg = on ? gk.secondary : gk.primary; fg = on ? gk.primary : gk.secondary; text = goal.text;
      if (hex6(fg) === hex6(bg)) fg = textOn(bg);
    } else if (card && t - card.t < 4) {
      bg = card.type === "RedCard" ? "#d50000" : "#ffd21a"; text = card.text;
    }
    var bar = $("cbar"), key = bg + "|" + fg;
    if (bar.dataset.bg !== key) { bar.style.background = bg; bar.style.color = fg || textOn(bg); bar.dataset.bg = key; }
    if ($("cbarText").textContent !== text) $("cbarText").textContent = text;
  }
  // (counted once per tenth of a second, not on every call of every frame)
  var redMemo = { key: "", v: [0, 0] };
  function redCards(team) {
    var key = Math.floor(t * 10) + ":" + E.length;
    if (redMemo.key !== key) { var v = [0, 0]; for (var i = 0; i < E.length && E[i].t <= t; i++) if (E[i].type === "RedCard" && E[i].team >= 0) v[E[i].team]++; redMemo = { key: key, v: v }; }
    return redMemo.v[team];
  }
  function setReds(el, n) { if (el.childElementCount !== n) { el.textContent = ""; for (var i = 0; i < n; i++) el.appendChild(document.createElement("i")); } }
  document.querySelectorAll("[data-speed]").forEach(function (b) {
    b.addEventListener("click", function () {
      speed = +b.getAttribute("data-speed");
      document.querySelectorAll("[data-speed]").forEach(function (o) { o.setAttribute("aria-pressed", String(o === b)); });
    });
  });
  seek.addEventListener("input", function () { jump(+seek.value); });
  $("skip").addEventListener("click", function () {
    if (!isFinite(secondHalfT)) return;
    // live: the second half has been generated already (the break makes no frames), so the live moment may move to it
    if (MODE === "live") LIVE_T = Math.max(LIVE_T, Math.min(tEnd, secondHalfT + .01));
    jump(secondHalfT + .01);
  });

  var last = performance.now();
  // DOM text is only written when it changes (every write makes the browser re-lay out the page).
  var shownText = {};
  function setText(id, v) { if (shownText[id] !== v) { shownText[id] = v; $(id).textContent = v; } }
  // Adaptive quality: when frames take too long (a weak device), first the resolution and the amount of rain / snow
  // drop, then the picture is drawn at 30 frames a second (an even, smooth pace) instead of stuttering below 60.
  var QUALITY = 0, frameAvg = 6, slowFor = 0, skip = false;
  // How long THIS page takes to draw a frame decides whether the device is slow - not the time between frames: a phone in low-power mode
  // (30 frames a second) or a busy browser stretches that without the page being slow, and the pitch used to be blurred for it.
  function adaptQuality(workMs) {
    frameAvg = frameAvg * .95 + workMs * .05;
    slowFor = frameAvg > (QUALITY >= 2 ? 26 : 14) ? slowFor + 1 : 0;
    if (slowFor > 90 && QUALITY < 3) { QUALITY++; slowFor = 0; frameAvg = 6; resize(); }
  }
  function frame(now) {
    requestAnimationFrame(frame);
    var t0 = performance.now(), ran = frameBody(now);
    if (ran) adaptQuality(performance.now() - t0);
  }
  function frameBody(now) { // (true if it drew a frame)
    if (QUALITY >= 3) { skip = !skip; if (skip) return false; } // 30 frames a second
    var dt = Math.max(0, Math.min(.25, (now - last) / 1000)); last = now;
    if (!R) return;
    if (!F || !F.length) { if (!cv.width) resize(); pitch(); return; }
    if (now - (frame.mgrAt || 0) > 1000) { frame.mgrAt = now; drawCards(); if (!document.querySelector('.mtab[data-pane="team"]').hidden) drawSheet(); }
    var adv = playing && t < tEnd ? Math.min(tEnd - t, dt * speed * SET.pace / 100) : 0;
    if (MODE === "live") {
      // the live moment carries on while you watch a replay of what happened; you catch it up when you reach it
      if (REWOUND) { LIVE_T = Math.min(tEnd, LIVE_T + dt * SET.pace / 100); adv = Math.min(adv, Math.max(0, LIVE_T - t)); t += adv; if (t >= LIVE_T - .05) setRewound(false); }
      else { t += adv; LIVE_T = t; }
    } else t += adv;
    if (!cv.width) resize();
    if (t > lastT) {
      for (var ei = 0; ei < E.length; ei++) { var ev = E[ei]; if (ev.t > t) break; if (ev.t > lastT && speed <= 4) { var sc = sample(ev.t); snd.play({ type: ev.type, whistle: ev.whistle, detail: ev.detail, value: ev.v, team: ev.team, homeScore: sc.hs, awayScore: sc.as, minute: parseInt(ev.minute, 10) || 0, info: ev.s }); } }
    }
    lastT = t; snd.tick(dt);
    var S = sample(t), second = t >= secondHalfT, ended = finished && t >= tEnd;
    lastS = S;
    if (!$("pcard").hidden && now - (frame.cardAt || 0) > 500) { frame.cardAt = now; updateCardEnergy(); }
    treadSnow(S, adv);
    pitch(); entities(S, now / 1000, adv); weatherFx(now / 1000, dt); lighting(now / 1000);
    setText("sc", S.hs + "-" + S.as);
    // extra time and penalties: the break before extra time and between its periods, the shoot-out
    var etBreak = S.half && t > secondHalfT, shootout = S.phase === 5, lastPens = null;
    if (SO_KICKS.length) for (var pi = SO_KICKS.length - 1; pi >= 0; pi--) if (SO_KICKS[pi].t <= t) { lastPens = SO_KICKS[pi].s; break; }
    if ($("pens").hidden !== !lastPens) $("pens").hidden = !lastPens;
    if (lastPens) setText("pens", "PENS " + lastPens);
    var mt = S.half ? (etBreak ? "ET" : "HT") : ended ? "FT" : shootout ? "PENS" : minuteText(S.clock, second);
    setText("mn", S.half ? (etBreak ? "ET" : "HT") : ended ? (R.final && R.final.aet ? "AET" : "FT") : shootout ? "PENS" : clockText(S.clock));
    setReds($("hr"), redCards(0)); setReds($("ar"), redCards(1)); syncLcup();
    updateBar(S);
    var inAdded = mt.indexOf("+") > 0;
    if ($("ad").hidden !== !inAdded) $("ad").hidden = !inAdded;
    if (inAdded) setText("ad", "+" + addedMinutes[t >= et2T ? 3 : t >= et1T ? 2 : second ? 1 : 0]);
    if ($("ht").hidden !== !S.half) $("ht").hidden = !S.half;
    if (S.half) {
      var nextT = !etBreak ? secondHalfT : t < et1T ? et1T : et2T;
      $("ht").querySelector(".big").textContent = etBreak ? "EXTRA TIME" : "HALF TIME";
      setText("htText", (etBreak ? (t < et1T ? "Extra time in " : "Second period in ") : "Second half in ") + (isFinite(nextT) ? mmss(nextT - t) : "a moment"));
    }
    if (MODE === "live" && now - lastProgress > 500) { lastProgress = now; worker.postMessage({ cmd: "progress", token: token, frames: find(LIVE_T) }); }
    if (MODE === "live") seek.max = Math.max(+seek.min + .1, LIVE_T); // (the slider reaches the live moment, not the future)
    if (Math.abs(+seek.value - t) > .4) seek.value = t;
    setText("rt", mmss(t - t0) + " / " + mmss(tEnd - t0));
    rebuildFeed();
    return true;
  }
