  // ---- the start screen: developer (everything) or user (kits, eleven, a live match) ----------------------------------
  var ROLE = null;
  document.querySelectorAll(".wlogo").forEach(function (i) { i.src = document.querySelector(".lu-top img").src; }); // (the logo once in the page)
  // a kit has four parts: the shirt, the trim round it, the socks (boots) and the number
  var COLS = [["Red", "#d32f2f"], ["Blue", "#1e5bc6"], ["White", "#f2f2f2"], ["Black", "#1b1b1b"], ["Green", "#2e9e4f"], ["Yellow", "#f9d71c"], ["Orange", "#f57c00"],
    ["Sky blue", "#4fb3e8"], ["Navy", "#14244f"], ["Purple", "#6a3fb5"], ["Maroon", "#7b1e2b"], ["Pink", "#e05aa0"], ["Grey", "#7d8796"], ["Cyan", "#18c6c6"]];
  var PARTS = [["base", "Shirt"], ["trim", "Trim"], ["socks", "Socks"], ["num", "Number"]];
  var KIT_DEFAULT = { home: { base: "#d32f2f", trim: "#1b1b1b", socks: "#1b1b1b", num: "#f2f2f2" }, away: { base: "#1e5bc6", trim: "#1b1b1b", socks: "#f2f2f2", num: "#f2f2f2" }, gk: { base: "#f9d71c", trim: "#1b1b1b", socks: "#1b1b1b", num: "#1b1b1b" } };
  var KIT = JSON.parse(JSON.stringify(KIT_DEFAULT));
  try { var kst = JSON.parse(localStorage.getItem("sv-user-kits") || "{}"); ["home", "away", "gk"].forEach(function (t) { if (kst[t] && typeof kst[t] === "object") PARTS.forEach(function (q) { if (kst[t][q[0]]) KIT[t][q[0]] = kst[t][q[0]]; }); }); } catch (e) {}
  function rgbOf(h) { var n = parseInt(h.slice(1), 16); return [n >> 16, (n >> 8) & 255, n & 255]; }
  // two colours that a viewer could mix up (red and maroon, white and grey...)
  function clash(a, b) { var x = rgbOf(a), y = rgbOf(b); return Math.hypot(x[0] - y[0], x[1] - y[1], x[2] - y[2]) < 110; }
  function clashAny(c, list) { return list.some(function (o) { return o && clash(c, o); }); }
  // the opponents' keeper and the referees: a colour that clashes with none of the shirts on the pitch
  var KEEPER_PICKS = ["#2e9e4f", "#f9d71c", "#e05aa0", "#f57c00", "#18c6c6", "#7d8796", "#6a3fb5", "#1b1b1b"];
  var REF_PICKS = ["#1b1b1b", "#f9d71c", "#e05aa0", "#18c6c6", "#2e9e4f", "#f57c00", "#7d8796", "#6a3fb5"];
  function awayKeeper() { var used = [KIT.home.base, KIT.away.base, KIT.gk.base]; return KEEPER_PICKS.filter(function (c) { return !clashAny(c, used); })[0] || "#2e9e4f"; }
  function refShirt() { var used = [KIT.home.base, KIT.away.base, KIT.gk.base, OTHER_GK ? OTHER_GK.base : awayKeeper()]; return REF_PICKS.filter(function (c) { return !clashAny(c, used); })[0] || "#1b1b1b"; }
  function userSettings() {
    var o = {}; for (var k in DEFAULTS) o[k] = DEFAULTS[k];
    o.grass = "across"; o.weather = "Sunny"; o.night = false; // (sunny, day, stripes across - the rest as it comes)
    try { var st = JSON.parse(localStorage.getItem("sv-user-settings") || "{}"); ["cb", "gest"].forEach(function (k) { if (k in st) o[k] = st[k]; }); } catch (e) {}
    return o;
  }
  var OTHER_GK = null; // (online: the friend's own keeper kit, which he chose)
  function applyUserKits() {
    var ag = OTHER_GK ? OTHER_GK.base : awayKeeper(), rs = refShirt();
    SET.home = { primary: KIT.home.base, secondary: KIT.home.trim, boots: KIT.home.socks, number: KIT.home.num, gk: KIT.gk.base, gkTrim: KIT.gk.trim, gkBoots: KIT.gk.socks, gkNumber: KIT.gk.num };
    SET.away = { primary: KIT.away.base, secondary: KIT.away.trim, boots: KIT.away.socks, number: KIT.away.num, gk: ag, gkTrim: textOn(ag) === "#111" ? "#1b1b1b" : "#f2f2f2", gkNumber: textOn(ag) === "#111" ? "#111111" : "#ffffff" };
    if (OTHER_GK) { SET.away.gkTrim = OTHER_GK.trim; SET.away.gkNumber = OTHER_GK.num; SET.away.gkBoots = OTHER_GK.socks; }
    SET.refs = { shirt: rs };
  }
  // what a part may not be: the shirt clashes with no other shirt on the pitch, a keeper with neither team, the number is readable on the shirt
  function forbidden(team, part) {
    if (part === "base") return team === "home" ? [KIT.away.base].concat(OTHER_GK ? [OTHER_GK.base] : []) : team === "away" ? [KIT.home.base] : [KIT.home.base, KIT.away.base].concat(OTHER_GK ? [OTHER_GK.base] : []); // (a team may take its own keeper's colour: the keeper changes)
    if (part === "num") return [KIT[team].base];
    return [];
  }
  function drawKitPicker() {
    var names = ONLINE === "guest" ? { home: "Your kit", away: "Your friend's kit", gk: "Your goalkeeper" } : { home: "Home kit", away: "Away kit", gk: "Goalkeeper" };
    $("kitBlocks").innerHTML = ["home", "away", "gk"].map(function (t) {
      var k = KIT[t];
      var shirt = '<svg viewBox="0 0 60 44" aria-hidden="true"><path d="M18 6 L24 3 Q30 8 36 3 L42 6 L52 13 L47 21 L42 18 L42 42 L18 42 L18 18 L13 21 L8 13 Z" fill="' + k.base + '" stroke="' + k.trim + '" stroke-width="2.4"/><rect x="21" y="40" width="7" height="4" fill="' + k.socks + '"/><rect x="32" y="40" width="7" height="4" fill="' + k.socks + '"/><text x="30" y="31" text-anchor="middle" font-family="Barlow, sans-serif" font-weight="800" font-size="15" fill="' + k.num + '">' + (t === "gk" ? 1 : 10) + '</text></svg>';
      return '<div class="kit' + (ONLINE === "guest" && t === "away" ? " fixed" : "") + '"><h3>' + shirt + names[t] + '</h3>' + PARTS.map(function (q) {
        var no = forbidden(t, q[0]);
        return '<div class="part"><span>' + q[1] + '</span><div class="swatches" data-team="' + t + '" data-part="' + q[0] + '" role="group" aria-label="' + names[t] + ' ' + q[1].toLowerCase() + '">' + COLS.map(function (c) {
          var dis = no.length && clashAny(c[1], no);
          return '<button type="button" data-c="' + c[1] + '" style="background:' + c[1] + '" aria-label="' + c[0] + '" title="' + c[0] + (dis ? " (too close to another kit)" : "") + '" aria-pressed="' + (k[q[0]] === c[1]) + '"' + (dis ? " disabled" : "") + '></button>';
        }).join("") + '</div></div>';
      }).join("") + '</div>';
    }).join("");
    $("kRefs").textContent = "The other team's keeper and the referees are dressed in colours that clash with none of yours.";
  }
  $("kitBlocks").addEventListener("click", function (ev) {
    var b = ev.target.closest("button[data-c]"); if (!b || b.disabled) return;
    var g = b.parentNode, t = g.getAttribute("data-team"), part = g.getAttribute("data-part");
    KIT[t][part] = b.getAttribute("data-c");
    // keep the rest legal: a number that became unreadable, a keeper that now clashes with a team
    ["home", "away", "gk"].forEach(function (x) { if (clash(KIT[x].num, KIT[x].base)) KIT[x].num = textOn(KIT[x].base) === "#111" ? "#1b1b1b" : "#f2f2f2"; });
    if (clashAny(KIT.gk.base, [KIT.home.base, KIT.away.base])) KIT.gk.base = KEEPER_PICKS.filter(function (c) { return !clashAny(c, [KIT.home.base, KIT.away.base]); })[0];
    drawKitPicker();
    var again = document.querySelector('#kitBlocks [data-team="' + t + '"][data-part="' + part + '"] button[aria-pressed="true"]'); if (again) again.focus();
  });
  $("asDev").addEventListener("click", function () {
    ROLE = "dev"; document.body.classList.remove("user"); $("welcome").hidden = true;
    startMatch("staged", STAGED_SEED);
  });
  $("asUser").addEventListener("click", function () {
    ROLE = "user"; document.body.classList.add("user");
    SET = userSettings(); syncSettingsUi();
    $("welcome").hidden = true; drawKitPicker(); $("kitSetup").hidden = false; $("kitGo").focus();
  });
  $("kitGo").addEventListener("click", function () {
    if (ONLINE === "guest") {
      $("kitSetup").hidden = true; $("online").hidden = false;
      $("oText").textContent = "Preparing your reply code...";
      NET.answer({ home: KIT.home, gk: KIT.gk }).then(function (code) {
        $("oText").textContent = "Send this code back to your friend. The match opens here as soon as he connects.";
        $("oAnswer").value = code; $("oAnswerBox").hidden = false; NET.status("Waiting for your friend...");
      }, function (e) { $("oText").textContent = "Could not make the reply code: " + e.message; });
      return;
    }
    try { localStorage.setItem("sv-user-kits", JSON.stringify(KIT)); } catch (e) {}
    applyUserKits(); saveSettings();
    $("kitSetup").hidden = true;
    $("kickoff").textContent = "Start match";
    $("prematchText").textContent = "Pick your eleven, the formation, roles and instructions below - then start the match.";
    startMatch("live", newSeed());
  });
  $("setStart").addEventListener("click", function () { location.hash = ""; location.reload(); });
  // online - the host: kits chosen, the invite link, then the friend's reply code
  function copyField(id) { var el = $(id); el.select(); try { navigator.clipboard.writeText(el.value).then(function () { toast("Copied"); }, function () { document.execCommand("copy"); toast("Copied"); }); } catch (x) { document.execCommand("copy"); } }
  $("oCopyLink").addEventListener("click", function () { copyField("oLink"); });
  $("oCopyAnswer").addEventListener("click", function () { copyField("oAnswer"); });
  $("oCancel").addEventListener("click", function () { location.hash = ""; location.reload(); });
  function startOnline() {
    // both sides: the match with the friend's kit as the away kit, then the eleven and "Start match"
    applyUserKits(); saveSettings();
    $("online").hidden = true;
    $("kickoff").textContent = "Start match";
    $("prematchText").textContent = "Pick your eleven, the formation, roles and instructions below - then start the match (it kicks off when you are both ready).";
    if (ONLINE === "host") startMatch("live", newSeed());
    else { startMatch("live", 0); $("cbarText").textContent = "Waiting for your friend's match..."; }
  }
  $("kitInvite").addEventListener("click", function () {
    try { localStorage.setItem("sv-user-kits", JSON.stringify(KIT)); } catch (e) {}
    if (!window.RTCPeerConnection) { toast("This browser cannot play online"); return; }
    $("kitSetup").hidden = true; $("online").hidden = false;
    $("oText").textContent = "Preparing your invite...";
    NET.invite({ home: KIT.home, gk: KIT.gk }).then(function (link) {
      $("oText").textContent = "Your friend opens the link, picks his kit and sends you back a reply code.";
      $("oLink").value = link; $("oLinkBox").hidden = false; $("oReplyBox").hidden = false;
    }, function (e) { $("oText").textContent = "Could not prepare the invite: " + e.message; });
  });
  $("oConnect").addEventListener("click", function () {
    var code = $("oReply").value; if (!code.trim()) return;
    NET.status("Connecting...");
    NET.accept(code).then(function (kit) {
      // the friend's colours are the away kit (his keeper gets a colour that clashes with nothing)
      KIT.away = kit.home; OTHER_GK = kit.gk; NET.status("Connecting...");
    }, function () { NET.status("That reply code does not work - copy it again from your friend."); });
  });
  NET.onOpen(function () { startOnline(); });
  // online - the friend: the link brought him here
  function joinFromLink() {
    var m = /#join=([^&]+)/.exec(location.hash); if (!m) return false;
    ROLE = "user"; document.body.classList.add("user"); SET = userSettings(); syncSettingsUi();
    $("welcome").hidden = true;
    if (!window.RTCPeerConnection) { $("online").hidden = false; $("oText").textContent = "This browser cannot play online."; return true; }
    NET.join(m[1]).then(function (hostKit) {
      KIT.away = hostKit.home; OTHER_GK = hostKit.gk; // (the friend's own kit must not look like the host's)
      if (clashAny(KIT.home.base, [KIT.away.base, OTHER_GK.base])) KIT.home.base = COLS.map(function (c) { return c[1]; }).filter(function (c) { return !clashAny(c, [KIT.away.base, OTHER_GK.base]); })[0];
      if (clash(KIT.home.num, KIT.home.base)) KIT.home.num = textOn(KIT.home.base) === "#111" ? "#1b1b1b" : "#f2f2f2";
      if (clashAny(KIT.gk.base, [KIT.home.base, KIT.away.base, OTHER_GK.base])) KIT.gk.base = KEEPER_PICKS.filter(function (c) { return !clashAny(c, [KIT.home.base, KIT.away.base, OTHER_GK.base]); })[0];
      $("kTitle").textContent = "Your kit"; $("kitInvite").hidden = true;
      $("kitGo").innerHTML = "<b>Continue</b><span>Next: a reply code for your friend, then your eleven.</span>";
      drawKitPicker(); $("kitSetup").hidden = false;
    }, function () { $("online").hidden = false; $("oText").textContent = "This invite link does not work - ask your friend for a new one."; });
    return true;
  }

  // the sound switch of the user's settings (the developer has his own button under the pitch)
  function syncSoundRow() { $("sndOn").setAttribute("aria-pressed", String(soundOn)); $("sndOff").setAttribute("aria-pressed", String(!soundOn)); }
  $("sndOn").addEventListener("click", function () { if (!soundOn) $("sound").click(); syncSoundRow(); });
  $("sndOff").addEventListener("click", function () { if (soundOn) $("sound").click(); syncSoundRow(); });
  if (!joinFromLink()) $("asUser").focus();
  requestAnimationFrame(frame);
})();