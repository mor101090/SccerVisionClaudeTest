  // ---- what a tactic locks: a choice rules out what would contradict it (the bus cannot push the line up, a counter is
  // never slow and never built from short combinations, possession is not played long...) ------------------------------
  function tacRules(T) {
    var r = { line: [0, 100], width: [0, 100], why: { line: "", width: "" }, off: { passing: {}, tempo: {}, style: {}, defence: {}, attack: {}, special: {} } };
    function lim(k, lo, hi, why) { r[k] = [Math.max(r[k][0], lo), Math.min(r[k][1], hi)]; r.why[k] = why; }
    function ban(k, v, why) { if (!(v in r.off[k])) r.off[k][v] = why; }
    if (T.defence === 3) { lim("line", 0, 30, "Park the bus: the line stays deep"); ban("special", 4, "Park the bus: no relentless pressure"); ban("tempo", 2, "Park the bus: no fast tempo"); }
    // (a high press is just pressing high up the pitch: it locks nothing)
    if (T.defence === 2) lim("line", 50, 100, "Offside trap: the line has to hold high");
    if (T.defence === 4) lim("line", 65, 100, "High backline: the line lives up the pitch");
    if (T.attack === 1) lim("width", 50, 100, "Crosses into the box: the play goes wide");
    if (T.attack === 2) ban("passing", 0, "Runs in behind need balls played behind - not short combinations");
    if (T.special === 1) { ban("passing", 2, "Possession: the ball is kept, not played long"); ban("tempo", 2, "Possession: no fast tempo"); }
    if (T.special === 2) { ban("tempo", 1, "Time wasting: the tempo is slow"); ban("tempo", 2, "Time wasting: the tempo is slow"); }
    if (T.special === 3) { ban("tempo", 0, "Counter-attacks are fast - never slow"); ban("passing", 0, "Counter-attacks are direct - no short combinations"); lim("line", 0, 60, "Counter-attacks: space behind the opponents to break into"); }
    if (T.special === 4) { lim("line", 55, 100, "Relentless pressure: the whole team up"); ban("defence", 3, "Relentless pressure: no parking the bus"); ban("tempo", 0, "Relentless pressure: never slow"); }
    // and the other way: a passing or tempo already chosen locks the styles that need the opposite
    if (T.passing === 0) { ban("special", 3, "Short combinations: no counter-attacks (pick mixed or balls in behind)"); ban("attack", 2, "Short combinations: no runs in behind"); }
    if (T.passing === 2) ban("special", 1, "Balls in behind: no possession game");
    if (T.tempo === 0) { ban("special", 3, "Slow tempo: no counter-attacks"); ban("special", 4, "Slow tempo: no relentless pressure"); }
    if (T.tempo === 2) { ban("special", 1, "Fast tempo: no possession game"); ban("special", 2, "Fast tempo: no time wasting"); ban("defence", 3, "Fast tempo: no parking the bus"); }
    if (T.tempo === 1) ban("special", 2, "Time wasting needs a slow tempo");
    return r;
  }
  // after a choice: the line and width into their ranges (a deep block pulls the line back)
  function enforceTac() {
    var r = tacRules(TAC);
    TAC.line = Math.max(r.line[0], Math.min(r.line[1], TAC.line));
    TAC.width = Math.max(r.width[0], Math.min(r.width[1], TAC.width));
  }
  function syncTacUi() {
    var rules = tacRules(TAC);
    document.querySelectorAll(".tac-seg").forEach(function (g) {
      var k = g.getAttribute("data-tac");
      g.querySelectorAll("button").forEach(function (b) {
        var why = rules.off[k] && rules.off[k][+b.getAttribute("data-v")];
        b.classList.toggle("locked", !!why); b.setAttribute("aria-disabled", String(!!why));
        if (why) b.setAttribute("data-lock", why); else b.removeAttribute("data-lock");
      });
    });
    ["line", "width"].forEach(function (k) {
      var el = $(k === "line" ? "tacLine" : "tacWidth"), lo = rules[k][0], hi = rules[k][1];
      el.style.setProperty("--lo", lo + "%"); el.style.setProperty("--hi", hi + "%");
      el.classList.toggle("locked", lo > 0 || hi < 100); el.title = lo > 0 || hi < 100 ? rules.why[k] : "";
    });
    $("tacLine").value = TAC.line; $("tacWidth").value = TAC.width;
    $("tacLine").style.setProperty("--p", TAC.line + "%"); $("tacWidth").style.setProperty("--p", TAC.width + "%");
    $("tacLineVal").textContent = words(TAC.line, LINE_WORDS); $("tacWidthVal").textContent = words(TAC.width, WIDTH_WORDS);
    document.querySelectorAll(".tac-seg").forEach(function (g) { var k = g.getAttribute("data-tac"); g.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", String(+b.getAttribute("data-v") === TAC[k])); }); });
    drawBoard();
  }
  var toastTimer = 0;
  function toast(text) { var el = $("tacToast"); el.textContent = text; el.classList.add("show"); clearTimeout(toastTimer); toastTimer = setTimeout(function () { el.classList.remove("show"); }, 2600); }
  var tacTimer = 0;
  function tacChanged() {
    if (TAC === USER_TAC) try { localStorage.setItem("sv-tactics", JSON.stringify(TAC)); } catch (e) {}
    syncTacUi();
    planChanged(MODE === "live" && KICKED ? "Instruction sent to the players" : null);
  }
  ["tacLine", "tacWidth"].forEach(function (id) {
    var key = id === "tacLine" ? "line" : "width";
    $(id).addEventListener("input", function () {
      var rr = tacRules(TAC)[key], v = +this.value;
      if (v < rr[0] || v > rr[1]) { v = Math.max(rr[0], Math.min(rr[1], v)); this.value = v; toast(tacRules(TAC).why[key]); }
      TAC[key] = v; this.style.setProperty("--p", this.value + "%"); $(id + "Val").textContent = words(TAC[key], key === "line" ? LINE_WORDS : WIDTH_WORDS); drawBoard(); });
    $(id).addEventListener("change", tacChanged);
  });
  document.querySelectorAll(".tac-seg").forEach(function (g) {
    g.addEventListener("click", function (ev) {
      var b = ev.target.closest("button"); if (!b) return;
      if (b.hasAttribute("data-lock")) { toast(b.getAttribute("data-lock")); return; } // (locked by another choice)
      var k = g.getAttribute("data-tac"), v = +b.getAttribute("data-v"); if (TAC[k] === v) return;
      TAC[k] = v; enforceTac(); tacChanged();
    });
  });
  function tacticsEvent(e) {
    var how = String(e.text).split(": ").slice(1).join(": ");
    if (e.team === 1) {
      var now = $("cpuNow"); now.textContent = how.charAt(0).toUpperCase() + how.slice(1); now.classList.remove("flash"); void now.offsetWidth; now.classList.add("flash");
      var li = document.createElement("li"), m = document.createElement("span"); m.textContent = e.minute; li.appendChild(m); li.appendChild(document.createTextNode(how));
      $("cpuLog").insertBefore(li, $("cpuLog").firstChild);
    }
  }
  function resetTacticsPanel() {
    $("tacTeam").textContent = R.home.name; $("cpuTeam").textContent = R.away.name;
    document.documentElement.style.setProperty("--home", teams[0].kit.primary); document.documentElement.style.setProperty("--away", teams[1].kit.primary);
    $("cpuNow").textContent = "Middle line, normal width, mixed passing, normal tempo, balanced"; $("cpuLog").textContent = "";
    var live = MODE === "live";
    $("tacBadge").textContent = live ? (KICKED ? "LIVE" : "PRE-MATCH") : "THE MANAGER'S PICK";
    $("tacStatus").textContent = live ? (KICKED ? "Your instructions reach the players straight away; changes wait for a stoppage." : "Pick your eleven and instructions, then kick off.") : "The home manager's own team and instructions - pick yours in a live match.";
    $("cpuNote").textContent = live ? "Reads your tactics and the score, and answers every few minutes." : "The computer manages only in a live match.";
  }
  syncTacUi();

  // Replay the match with new settings: a tactic change keeps a random match's seed (the same match, your tactics); a
  // change of weather, wind or pitch in a random match draws a new one.
  function restartMatch(newConditions) {
    var seed = MODE === "staged" ? STAGED_SEED : MODE === "random" && !newConditions ? currentSeed : newSeed();
    startMatch(MODE, seed, R && MODE !== "live" && !(newConditions && MODE === "random") ? t - t0 : undefined);
  }
  function newSeed() { return 1 + Math.floor(Math.random() * 2000000000); }
  document.querySelectorAll(".modes button").forEach(function (b) {
    b.addEventListener("click", function () {
      var m = b.getAttribute("data-mode");
      startMatch(m, m === "staged" ? STAGED_SEED : newSeed());
    });
  });
  function beginMatch(hdr) {
    R = hdr; F = R.frames = []; E = R.events = []; R.stats = []; teams = [R.home, R.away];
    R.home._kit0 = R.home.kit; R.away._kit0 = R.away.kit;
    SUBS = {}; t0 = 0; tEnd = 0; secondHalfT = Infinity; addedMinutes = [0, 0, 0, 0]; et1T = Infinity; et2T = Infinity; SO_KICKS = []; finished = false;
    var comp = R.competition || "League", sbEl = document.querySelector(".sb");
    sbEl.classList.toggle("cup", comp === "Cup"); sbEl.classList.toggle("lcup", comp === "LeagueCup");
    $("sbComp").textContent = comp === "Cup" ? "Cup match" : comp === "LeagueCup" ? "League Cup match" : "League match";
    t = 0; lastT = 0; shown = -1; shownText = {}; moments.textContent = "";
    $("hn").textContent = R.home.shortName; $("an").textContent = R.away.shortName;
    applyColours(); applyWind(); buildSurface(); syncSettingsUi();
    possTeam = -1; ballPrev = null; bgCv = null;
    seek.min = 0; seek.max = 0;
    $("seedText").textContent = (MODE === "staged" ? "(staged) " : MODE === "live" ? "(live) " : "") + R.seed;
    $("liveYou").textContent = ONLINE ? "You: " + R.home.name + " v your friend: " + R.away.name : "You: " + R.home.name + " (home) v the computer: " + R.away.name;
    feed.textContent = ""; $("facts").textContent = ""; // (nothing has happened yet: before kick-off the old match is gone)
    $("cbarText").textContent = MODE === "live" ? "Pre-match: pick your team, then kick off" : "Kick-off"; $("cbar").style.background = "#3c4552"; $("cbar").dataset.bg = "";
    setText("sc", "0-0"); setText("mn", "00:00"); setText("rt", "0:00 / 0:00");
    if (MODE === "live") {
      PLAN = USER_PLAN; TAC = USER_TAC;
      PLAN.slots = startLineup(); PLAN.formation = R.home.formation || PLAN.formation;
      if (!formationOf(PLAN.formation)) PLAN.formation = "4-4-2";
    } else { PLAN = viewPlan(); TAC = viewTac(); }
    syncTacUi(); resetTacticsPanel(); drawManager();
    note(""); setPlaying(true);
    if (luDone) { clearInterval(luTimer); luDone = null; $("lineups").hidden = true; } // (a new match: the old line-ups go)
    if (MODE !== "live") showLineups(function () {});
  }
  function appendChunk(m) {
    var L2 = m.len, n = m.frames.length / L2, i;
    for (i = 0; i < n; i++) F.push(Array.prototype.slice.call(m.frames, i * L2, (i + 1) * L2));
    JSON.parse(m.events).forEach(function (e) {
      E.push(e); addMoment(e);
      if (e.type === "TacticsChanged") tacticsEvent(e);
      if ((e.type === "Substitution" || e.type === "SubstitutionRequest") && MODE === "live") subEvent(e);
      if (e.type === "Substitution" && e.s) { var parts = String(e.s).split("|"); (SUBS[e.p] = SUBS[e.p] || []).push({ t: e.t, number: parseInt(parts[0], 10), shortName: parts[1] }); }
      if (e.type === "SecondHalfStart" && secondHalfT === Infinity) secondHalfT = e.t;
      if (e.type === "AddedTime") { var am = parseInt(String(e.text).replace(/[^0-9]/g, ""), 10) || 0; addedMinutes[e.t >= et2T ? 3 : e.t >= et1T ? 2 : e.t < secondHalfT ? 0 : 1] = am; }
      if (e.type === "ShootoutKick") SO_KICKS.push(e);
      if (e.type === "ExtraTime") { if (e.detail === 2) et2T = e.t; else et1T = e.t; }
    });
    var SL = 15 + 22 + 22 + 4; // the time, 14 team stats, the energy left of the 22 players, their ratings x10, passes and completed passes h/a (as over 90 minutes)
    var stats = m.stats && typeof m.stats.length === "number" ? m.stats : [];
    function finiteStat(v) { var n = typeof v === "number" ? v : Number(v); return isFinite(n) ? n : 0; }
    for (i = 0; i < stats.length; i += SL) {
      var row = [];
      for (var s = 0; s < SL && i + s < stats.length; s++) row.push(finiteStat(stats[i + s]));
      R.stats.push(row);
    }
    if (F.length) { if (F.length === n) { t0 = F[0][0] / 100; t = t0; lastT = t0; seek.min = t0; } tEnd = F[F.length - 1][0] / 100; seek.max = tEnd; }
    if (m.finished) finished = true;
    if (m.final) { try { R.final = JSON.parse(m.final); } catch (e) {} }
    if (resumeAt !== null && tEnd >= t0 + resumeAt) { t = lastT = t0 + resumeAt; resumeAt = null; }
    if (MODE === "live") worker.postMessage({ cmd: "progress", token: token, frames: Math.max(0, find(REWOUND ? LIVE_T : t)) });
  }
  function rebuildFeed() {
    var last = 0; while (last < E.length && E[last].t <= t) last++;
    if (last === shown) return;
    feed.textContent = "";
    var lines = [];
    for (var i = last - 1; i >= 0 && lines.length < 80; i--) { var e = E[i]; if (e.type === "Whistle" || e.type === "Skill" || !e.text) continue; lines.push(e); }
    lines.reverse().forEach(function (e) {
      var d = document.createElement("div"), m = document.createElement("span"), tx = document.createElement("span");
      m.className = "m"; m.textContent = e.minute; tx.textContent = e.text; tx.className = lineClass(e);
      d.appendChild(m); d.appendChild(tx); feed.appendChild(d);
    });
    feed.scrollTop = feed.scrollHeight; shown = last;
    var c = { Corner: [0, 0], Foul: [0, 0], YellowCard: [0, 0], RedCard: [0, 0], Offside: [0, 0], Save: [0, 0] };
    for (var j = 0; j < last; j++) { var ev = E[j]; if (c[ev.type] && ev.team >= 0) c[ev.type][ev.team]++; }
    // Running team stats from the replay's stats timeline (once a second).
    var st = null, ST = R.stats || [];
    for (var q = 0; q < ST.length && ST[q][0] / 100 <= t; q++) st = ST[q];
    st = st || [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    function acc(done, all) { return all > 0 ? Math.round(done / all * 100) + "%" : "-"; }
    var possTotal = st[1] + st[2], hp = possTotal > 0 ? Math.round(st[1] / possTotal * 100) : 50;
    var rows = [["Possession", [hp + "%", (100 - hp) + "%"]], ["Shots", [st[3], st[4]]], ["Shots on target", [st[5], st[6]]],
                ["Passes", [st[59] || 0, st[60] || 0]], ["Pass accuracy", [acc(st[61], st[59]), acc(st[62], st[60])]],
                ["Tackles", [st[7], st[8]]], ["Tackles won", [st[9], st[10]]], ["Dribbles", [st[11], st[12]]], ["Dribbles won", [st[13], st[14]]],
                ["Saves", c.Save], ["Corners", c.Corner], ["Fouls", c.Foul], ["Offsides", c.Offside], ["Yellow cards", c.YellowCard], ["Red cards", c.RedCard]];
    var facts = $("facts");
    facts.textContent = "";
    rows.forEach(function (r) {
      var tr = document.createElement("tr");
      [r[1][0], r[0], r[1][1]].forEach(function (cell, idx) {
        var td = document.createElement("td");
        if (idx === 1) td.className = "l";
        td.textContent = String(cell);
        tr.appendChild(td);
      });
      facts.appendChild(tr);
    });
  }
  // a live match can be watched again, but only what has happened: never past the live moment (LIVE_T)
  var LIVE_T = 0, REWOUND = false;
  $("liveBadge").addEventListener("click", function () { if (MODE === "live" && REWOUND) { jump(LIVE_T); setRewound(false); } });
  function setRewound(on) {
    REWOUND = on;
    // the red LIVE badge becomes the way back to the live moment
    var b = $("liveBadge"); b.textContent = MODE === "live" && on ? "BACK TO LIVE" : "LIVE"; b.classList.toggle("back", MODE === "live" && on);
  }
  function jump(to) {
    if (MODE === "live") { to = Math.min(to, LIVE_T); setRewound(to < LIVE_T - .5); }
    t = Math.max(t0, Math.min(tEnd, to)); lastT = t; shown = -1;
    if (trodCv) { var g = trodCv.getContext("2d"); g.globalCompositeOperation = "copy"; g.drawImage(snowCv, 0, 0); g.globalCompositeOperation = "source-over"; snowAt = -1; }
  }

  function setPlaying(on) {
    playing = on;
    $("play").setAttribute("aria-label", on ? "Pause" : "Play");
    // the action you can take is drawn bright: pause bars while playing, the play triangle while paused
    $("playTri").setAttribute("class", on ? "dim" : ""); $("pauseBars").setAttribute("class", on ? "" : "dim");
  }
  $("play").addEventListener("click", function () { setPlaying(!playing); });
  $("gear").addEventListener("click", function () { var open = $("settings").hidden; $("settings").hidden = !open; this.setAttribute("aria-expanded", String(open)); });
  $("settingsClose").addEventListener("click", function () { $("settings").hidden = true; $("gear").setAttribute("aria-expanded", "false"); $("gear").focus(); });
