  // ---- developer settings ----------------------------------------------------------------------------
  var DEFAULTS = { pace: 100, grass: "along", maint: "2", weather: "Sunny", wind: "0", night: "0", net: "#ebf0eb", home: null, away: null, refs: null, snowLines: "yellow", snowCover: "cleared", cb: "0", gest: "1", comp: "League", stadium: "stand" };
  var SET = (function () { var o = {}; for (var k in DEFAULTS) o[k] = DEFAULTS[k]; try { var st = JSON.parse(localStorage.getItem("sv-dev-settings") || "{}"); for (var k2 in st) if (k2 in DEFAULTS) o[k2] = st[k2]; } catch (e) {} return o; })();
  SET.night = SET.night === true || SET.night === "1";
  function saveSettings() {
    try {
      if (typeof ROLE !== "undefined" && ROLE === "user") { localStorage.setItem("sv-user-settings", JSON.stringify({ cb: SET.cb, gest: SET.gest })); return; } // (the user's own: nothing of the developer's)
      var o = {}; for (var k in DEFAULTS) o[k] = SET[k]; o.night = SET.night ? "1" : "0"; localStorage.setItem("sv-dev-settings", JSON.stringify(o));
    } catch (e) {}
  }
  // Which recording fits the chosen conditions: d(ry)/r(ain)/s(now) + upkeep 2/1/0 + wind 0/1 ("d20" is the normal one).
  function condCode() { return (SET.weather === "Rain" ? "r" : SET.weather === "Snow" ? "s" : "d") + SET.maint + SET.wind + (SET.comp || "League"); }
  function syncSettingsUi() {
    $("pace").value = SET.pace; $("paceText").textContent = SET.pace + "%";
    document.querySelectorAll(".seg[data-set]").forEach(function (g) {
      var key = g.getAttribute("data-set"), val = key === "night" ? (SET.night ? "1" : "0") : String(SET[key]);
      g.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", String(b.getAttribute("data-v") === val)); });
    });
    $("netColor").value = SET.net;
    if (R) {
      var c = teamColours();
      ["home", "away"].forEach(function (side, i) {
        $(side + "P").value = c[i].primary; $(side + "S").value = c[i].secondary; $(side + "N").value = c[i].number; $(side + "B").value = c[i].boots;
        $(side + "G").value = c[i].gk; $(side + "GS").value = c[i].gkTrim; $(side + "GN").value = c[i].gkNumber;
      });
      var rc = refColours(); $("refC").value = rc.shirt; $("refT").value = rc.trim; $("refL").value = rc.letter; $("refB").value = rc.boots;
      $("homeLabel").textContent = R.home.shortName; $("awayLabel").textContent = R.away.shortName;
      $("homeGkLabel").textContent = R.home.shortName + " keeper"; $("awayGkLabel").textContent = R.away.shortName + " keeper";
    }
  }
  function note(text) { $("setNote").textContent = text || ""; }
  // Team colours: shirt, trim, numbers and goalkeeper picked here (or the team's own). A number not picked follows the
  // shirt (black or white, whichever reads best); the keeper's number follows his shirt.
  function hex6(c) { return c.length === 4 ? "#" + c[1] + c[1] + c[2] + c[2] + c[3] + c[3] : c.toLowerCase(); }
  function teamColours() {
    return [0, 1].map(function (i) {
      var base = (i ? R.away : R.home)._kit0, pick = SET[i ? "away" : "home"];
      var k = {}; for (var key in base) k[key] = hex6(base[key]);
      k.gkTrim = k.secondary; // the keeper's trim follows the team's until it is picked
      k.boots = BOOTS;
      // colour-blind mode: blue against orange (Okabe-Ito), keepers in pink and yellow - told apart by everyone
      if (SET.cb === "1") return i ? { primary: "#e69f00", secondary: "#000000", number: "#111111", gk: "#f0e442", gkTrim: "#000000", gkNumber: "#111111", boots: "#111111" }
                                   : { primary: "#0072b2", secondary: "#ffffff", number: "#ffffff", gk: "#cc79a7", gkTrim: "#ffffff", gkNumber: "#111111", boots: "#111111" };
      if (!pick) return k;
      if (pick.boots) k.boots = pick.boots;
      if (pick.primary) { k.primary = pick.primary; k.number = textOn(pick.primary) === "#111" ? "#111111" : "#ffffff"; }
      if (pick.secondary) { k.secondary = pick.secondary; k.gkTrim = pick.secondary; }
      if (pick.number) k.number = pick.number;
      if (pick.gk) { k.gk = pick.gk; k.gkNumber = textOn(pick.gk) === "#111" ? "#111111" : "#ffffff"; }
      if (pick.gkTrim) k.gkTrim = pick.gkTrim;
      if (pick.gkNumber) k.gkNumber = pick.gkNumber;
      if (pick.gkBoots) k.gkBoots = pick.gkBoots;
      return k;
    });
  }
  function applyColours() {
    var c = teamColours(); R.home.kit = c[0]; R.away.kit = c[1];
    document.documentElement.style.setProperty("--home", c[0].primary);
    ["kit", c[0].primary, "kitn", c[0].number, "gk", c[0].gk, "gkn", c[0].gkNumber].forEach(function (v, i, a) { if (i % 2 === 0) document.documentElement.style.setProperty("--" + v + "-home", a[i + 1]); }); document.documentElement.style.setProperty("--away", c[1].primary);
    $("hb").style.setProperty("--team", c[0].primary); $("ab").style.setProperty("--team", c[1].primary);
    $("cbar").dataset.bg = "";
  }
  // The wind of the recording; with no wind set there is still a light breeze (too light to move the ball, shown on the
  // chip and in the flags), from a direction fixed per match.
  function applyWind() {
    var c = R.conditions || {};
    WIND.speed = c.wind || 0;
    WIND.angle = WIND.speed > .5 ? c.windAngle || 0 : (R.seed * 97 % 360) * Math.PI / 180;
    WIND.breeze = WIND.speed > .5 ? WIND.speed : 1.4 + (R.seed % 9) * .35;
    WIND.screenAngle = Math.atan2(-Math.sin(WIND.angle), Math.cos(WIND.angle));
    updateWeatherChip();
  }
  document.querySelectorAll(".seg[data-set]").forEach(function (g) {
    var key = g.getAttribute("data-set");
    g.addEventListener("click", function (ev) {
      var b = ev.target.closest("button"); if (!b) return;
      var v = b.getAttribute("data-v"), before = condCode(), wasWeather = SET.weather;
      if (key === "night") SET.night = v === "1"; else SET[key] = v;
      saveSettings(); syncSettingsUi(); updateWeatherChip();
      if (condCode() !== before) restartMatch(true);
      else if ((key === "weather" && wasWeather !== SET.weather) || key === "night" || key === "snowCover") buildSurface();
      if (key === "cb" && R) { applyColours(); drawManager && drawManager(); }
    });
  });
  $("pace").addEventListener("input", function () { SET.pace = +this.value; $("paceText").textContent = SET.pace + "%"; saveSettings(); });
  $("netColor").addEventListener("input", function () { SET.net = this.value; saveSettings(); });
  [["P", "primary"], ["S", "secondary"], ["N", "number"], ["B", "boots"], ["G", "gk"], ["GS", "gkTrim"], ["GN", "gkNumber"]].forEach(function (d) {
    ["home", "away"].forEach(function (side) {
      $(side + d[0]).addEventListener("input", function () {
        var pick = SET[side] || {};
        pick[d[1]] = this.value;
        if (d[1] === "primary") delete pick.number; // numbers follow a new shirt colour until they are picked again
        if (d[1] === "gk") delete pick.gkNumber;
        SET[side] = pick; saveSettings(); applyColours(); syncSettingsUi();
      });
    });
  });
  // Referees and assistants: shirt, trim and letter (the trim and letter follow a new shirt until they are picked).
  function refColours() {
    var r = typeof SET.refs === "string" ? { shirt: SET.refs } : SET.refs || {};
    var shirt = r.shirt || "#121212", auto = r.shirt ? (textOn(r.shirt) === "#111" ? "#111111" : "#ffffff") : "#f2c230";
    return { shirt: shirt, trim: r.trim || auto, letter: r.letter || auto, boots: r.boots || BOOTS };
  }
  [["refC", "shirt"], ["refT", "trim"], ["refL", "letter"], ["refB", "boots"]].forEach(function (d) {
    $(d[0]).addEventListener("input", function () {
      var r = typeof SET.refs === "string" ? { shirt: SET.refs } : SET.refs || {};
      r[d[1]] = this.value;
      if (d[1] === "shirt") { delete r.trim; delete r.letter; }
      SET.refs = r; saveSettings(); syncSettingsUi();
    });
  });
  $("setReset").addEventListener("click", function () {
    var before = condCode();
    for (var k in DEFAULTS) SET[k] = DEFAULTS[k]; SET.night = false;
    if (ROLE === "user") { SET.grass = "across"; applyUserKits(); }
    saveSettings(); syncSettingsUi(); applyColours(); updateWeatherChip();
    if (condCode() !== before) restartMatch(true); else buildSurface();
  });
