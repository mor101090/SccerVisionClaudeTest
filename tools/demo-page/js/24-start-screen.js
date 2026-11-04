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
  // How good your team and the opponent are (weak / medium / strong squads): only sets the choice - the match that follows uses it
  function syncStrength() {
    document.querySelectorAll(".seg[data-str]").forEach(function (g) {
      g.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", String(b.getAttribute("data-v") === SET[g.getAttribute("data-str")])); });
    });
  }
  document.querySelectorAll(".seg[data-str]").forEach(function (g) {
    g.addEventListener("click", function (ev) { var b = ev.target.closest("button"); if (!b) return; SET[g.getAttribute("data-str")] = b.getAttribute("data-v"); syncStrength(); });
  });
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
    if (part === "base" && KIT_MODE === "online") return team === "home" ? [KIT.away.base] : team === "away" ? [KIT.home.base] : [KIT.home.base]; // (your two kits differ; your keeper is not dressed like your team)
    if (part === "base") return team === "home" ? [KIT.away.base].concat(OTHER_GK ? [OTHER_GK.base] : []) : team === "away" ? [KIT.home.base] : [KIT.home.base, KIT.away.base].concat(OTHER_GK ? [OTHER_GK.base] : []); // (a team may take its own keeper's colour: the keeper changes)
    if (part === "num") return [KIT[team].base];
    return [];
  }
  var KIT_MODE = "computer"; // (the kit picker for a match against the computer, or for an online game: then only your own kits)
  function drawKitPicker() {
    var names = ONLINE === "guest" ? { home: "Your kit", away: "Your friend's kit", gk: "Your goalkeeper" }
      : KIT_MODE === "online" ? { home: "Your kit", away: "Your second kit (worn if your friend's looks like yours)", gk: "Your goalkeeper" }
      : { home: "Home kit", away: "Away kit", gk: "Goalkeeper" };
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
    var gkAvoid = KIT_MODE === "online" ? [KIT.home.base] : [KIT.home.base, KIT.away.base];
    if (clashAny(KIT.gk.base, gkAvoid)) KIT.gk.base = KEEPER_PICKS.filter(function (c) { return !clashAny(c, gkAvoid); })[0];
    drawKitPicker();
    var again = document.querySelector('#kitBlocks [data-team="' + t + '"][data-part="' + part + '"] button[aria-pressed="true"]'); if (again) again.focus();
  });
  // ---- the club crest: described in words, made by the faces server (tools/faces/server.py: Qwen-Image in ComfyUI) ----------------
  // the address of that server: #faces=http://127.0.0.1:8765 in the page's address, or the last one used; without it the button says how to start it
  var CREST = (function () { try { return localStorage.getItem("sv-crest") || ""; } catch (e) { return ""; } })();
  var FACES_API = (/#faces=([^&]+)/.exec(location.hash) || [])[1] || (function () { try { return localStorage.getItem("sv-faces-api") || ""; } catch (e) { return ""; } })() || "http://127.0.0.1:8765";
  function drawCrest() {
    $("crestPreview").innerHTML = CREST ? '<img alt="" src="' + CREST + '">' : "";
    $("crestClear").hidden = !CREST;
  }
  function setCrest(dataUrl) { CREST = dataUrl || ""; try { localStorage.setItem("sv-crest", CREST); } catch (e) {} drawCrest(); }
  $("crestGo").addEventListener("click", function () {
    var prompt = $("crestPrompt").value.trim(); if (!prompt) { $("crestNote").textContent = "Write what the crest should look like."; return; }
    $("crestGo").disabled = true; $("crestNote").textContent = "Creating your crest... (about a minute)";
    fetch(FACES_API + "/logo", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ prompt: prompt, name: (R && R.home && R.home.name) || "" }) })
      .then(function (r) { if (!r.ok) return r.json().then(function (j) { throw new Error(j.error || "the server said " + r.status); }); return r.blob(); })
      .then(function (b) { return new Promise(function (res) { var im = new Image(), u = URL.createObjectURL(b); im.onload = function () { var c = document.createElement("canvas"); c.width = c.height = 256; c.getContext("2d").drawImage(im, 0, 0, 256, 256); URL.revokeObjectURL(u); res(c.toDataURL("image/png")); }; im.src = u; }); })
      .then(function (d) { setCrest(d); $("crestNote").textContent = "Your crest."; try { localStorage.setItem("sv-faces-api", FACES_API); } catch (e) {} })
      .catch(function (e) { $("crestNote").textContent = e instanceof TypeError ? "The crest server is not running. Start it with: python3 tools/faces/server.py  (ComfyUI with Qwen-Image must be running)" : "Could not make the crest: " + e.message; })
      .then(function () { $("crestGo").disabled = false; });
  });
  $("crestClear").addEventListener("click", function () { setCrest(""); $("crestNote").textContent = ""; });
  drawCrest();
  $("asDev").addEventListener("click", function () {
    ROLE = "dev"; document.body.classList.remove("user"); $("welcome").hidden = true;
    startMatch("staged", STAGED_SEED);
  });
  function openKits(mode) {
    ROLE = "user"; document.body.classList.add("user");
    SET = userSettings(); syncSettingsUi();
    KIT_MODE = mode;
    $("kTitle").textContent = mode === "online" ? "Your kits for the online game" : "Your kits";
    $("kitStrength").hidden = mode === "online"; syncStrength();
    $("kitGo").hidden = mode === "online"; $("kitHost").hidden = $("kitJoin").hidden = mode !== "online"; $("kitInvite").hidden = true;
    $("kitNext").textContent = mode === "online" ? "Your friend picks his own kits. If his shirt looks like yours, one of you wears the second kit - you both see it before the match." : "Next: your eleven, the formation and the instructions - then start the match.";
    $("welcome").hidden = true; drawKitPicker(); $("kitSetup").hidden = false; (mode === "online" ? $("kitHost") : $("kitGo")).focus();
  }
  $("asUser").addEventListener("click", function () { openKits("computer"); });
  $("asOnline").addEventListener("click", function () {
    if (!window.RTCPeerConnection) { toast("This browser cannot play online"); return; }
    openKits("online");
  });
  $("kitGo").addEventListener("click", function () {
    if (ONLINE === "guest") {
      $("kitSetup").hidden = true; $("online").hidden = false;
      $("oText").textContent = "Preparing your reply code...";
      NET.answer({ home: KIT.home, gk: KIT.gk }).then(function (code) {
        $("oText").textContent = "Send this code back to your friend. The match opens here as soon as he connects.";
        $("oAnswer").value = code; $("oAnswerBox").hidden = false; NET.status("Waiting for your friend... keep this page open until the match starts.");
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
      $("oText").textContent = "Your friend opens the link, picks his kit and sends you back a reply code. Keep this page open (a new page needs a new invite).";
      $("oLink").value = link; $("oLinkBox").hidden = false; $("oReplyBox").hidden = false;
    }, function (e) { $("oText").textContent = "Could not prepare the invite: " + e.message; });
  });
  // ---- online with a game code: the kits of both, worked out the same way on both sides ----
  function kitShirt(k) { return '<svg viewBox="0 0 60 44" aria-hidden="true"><path d="M18 6 L24 3 Q30 8 36 3 L42 6 L52 13 L47 21 L42 18 L42 42 L18 42 L18 18 L13 21 L8 13 Z" fill="' + k.base + '" stroke="' + k.trim + '" stroke-width="2.4"/><rect x="21" y="40" width="7" height="4" fill="' + k.socks + '"/><rect x="32" y="40" width="7" height="4" fill="' + k.socks + '"/></svg>'; }
  function readable(k) { var c = JSON.parse(JSON.stringify(k)); if (clash(c.num, c.base)) c.num = textOn(c.base) === "#111" ? "#1b1b1b" : "#f2f2f2"; return c; }
  // The host keeps his kit. The friend wears his own kit - or his second kit if his looks like the host's (or the host's
  // keeper's) - or, if both do, a shirt that clashes with neither. Each keeper takes a colour that clashes with no shirt.
  function onlineKits(h, g) {
    var hostKit = h.home, hostGk = h.gk, used;
    var guestKit = !clashAny(g.home.base, [hostKit.base, hostGk.base]) ? g.home : !clashAny(g.away.base, [hostKit.base, hostGk.base]) ? g.away
      : (function () { var k = JSON.parse(JSON.stringify(g.away)); k.base = COLS.map(function (c) { return c[1]; }).filter(function (c) { return !clashAny(c, [hostKit.base, hostGk.base]); })[0]; return readable(k); })();
    var second = guestKit === g.away;
    used = [hostKit.base, guestKit.base];
    var guestGk = g.gk;
    if (clashAny(guestGk.base, used.concat([hostGk.base]))) { guestGk = JSON.parse(JSON.stringify(g.gk)); guestGk.base = KEEPER_PICKS.filter(function (c) { return !clashAny(c, used.concat([hostGk.base])); })[0]; guestGk = readable(guestGk); }
    var hostGk2 = hostGk;
    if (clashAny(hostGk.base, [guestKit.base])) { hostGk2 = JSON.parse(JSON.stringify(hostGk)); hostGk2.base = KEEPER_PICKS.filter(function (c) { return !clashAny(c, used.concat([guestGk.base])); })[0]; hostGk2 = readable(hostGk2); }
    return { host: { kit: hostKit, gk: hostGk2 }, guest: { kit: guestKit, gk: guestGk, second: second } };
  }
  NET.onKits(function (theirs) {
    var mine = { home: KIT.home, away: KIT.away, gk: KIT.gk };
    var k = ONLINE === "host" ? onlineKits(mine, theirs) : onlineKits(theirs, mine);
    var me = ONLINE === "host" ? k.host : k.guest, him = ONLINE === "host" ? k.guest : k.host;
    KIT.home = me.kit; KIT.gk = me.gk; KIT.away = him.kit; OTHER_GK = him.gk;
    $("oKits").innerHTML = '<span>' + kitShirt(me.kit) + ' You</span><b>v</b><span>' + kitShirt(him.kit) + ' Your friend</span>';
    $("oKits").hidden = false;
    var note = k.guest.second ? (ONLINE === "guest" ? "Your shirt looked like your friend's: you wear your second kit." : "Your friend's shirt looked like yours: he wears his second kit.") : "";
    NET.status("Connected! " + note);
    setTimeout(startOnline, note ? 2200 : 900);
  });
  function onlineKitsMine() { try { localStorage.setItem("sv-user-kits", JSON.stringify(KIT)); } catch (e) {} return { home: KIT.home, away: KIT.away, gk: KIT.gk }; }
  $("kitHost").addEventListener("click", function () {
    var kits = onlineKitsMine();
    $("kitSetup").hidden = true; $("online").hidden = false;
    $("oText").textContent = "Creating your game...";
    NET.hostGame(kits, function (code) {
      $("oText").textContent = "Tell your friend this code. He opens SoccerVision, picks his kit, chooses \"Join a game\" and types it in. Keep this page open.";
      $("oCode").textContent = code.slice(0, 3) + " " + code.slice(3); $("oCodeBox").hidden = false;
      $("oCodeLink").textContent = "Or send him this link: " + location.origin + location.pathname + "#game=" + code;
      NET.status("Waiting for your friend to join...");
    }, function () { legacyInvite(); }).catch(function () { legacyInvite(); });
  });
  // (without the game-code service: the old way, a link and a reply code)
  function legacyInvite() { toast("The game-code service is not reachable - using an invite link instead"); $("online").hidden = true; $("kitSetup").hidden = false; $("kitInvite").click(); }
  $("oCopyCode").addEventListener("click", function () {
    var code = $("oCode").textContent.replace(/\s/g, "");
    try { navigator.clipboard.writeText(code).then(function () { toast("Copied"); }); } catch (e) {}
  });
  $("kitJoin").addEventListener("click", function () {
    $("kitSetup").hidden = true; $("online").hidden = false;
    $("oText").textContent = "Type in the six-digit code your friend gives you.";
    $("oJoinBox").hidden = false; $("oJoinCode").focus();
    var pre = /#game=(\d{6})/.exec(location.hash); if (pre) { $("oJoinCode").value = pre[1]; }
  });
  function joinNow() {
    var code = $("oJoinCode").value.replace(/\D/g, "");
    if (code.length !== 6) { NET.status("The code has six digits."); return; }
    $("oJoinGo").disabled = true; NET.status("Connecting...");
    NET.joinGame(code, onlineKitsMine(), function (e) { $("oJoinGo").disabled = false; NET.status(e.message); }).catch(function (e) { $("oJoinGo").disabled = false; NET.status("Could not join: " + e.message); });
  }
  $("oJoinGo").addEventListener("click", joinNow);
  $("oJoinCode").addEventListener("keydown", function (ev) { if (ev.key === "Enter") joinNow(); });
  $("oConnect").addEventListener("click", function () {
    var code = $("oReply").value; if (!code.trim()) return;
    NET.status("Connecting...");
    NET.accept(code).then(function (kit) {
      // the friend's colours are the away kit (his keeper gets a colour that clashes with nothing)
      KIT.away = kit.home; OTHER_GK = kit.gk; NET.status("Connecting...");
    }, function (e) { NET.status(e && /already used/.test(e.message) ? "This invite was already used - press Cancel and make a new one." : "That reply code does not work - copy it again from your friend (the whole code)."); });
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
  function gameFromLink() {
    if (!/#game=\d{6}/.test(location.hash)) return false;
    if (!window.RTCPeerConnection) return false;
    openKits("online"); $("kitHost").hidden = true; $("kitJoin").focus();
    return true;
  }
  if (!joinFromLink() && !gameFromLink()) $("asUser").focus();
  requestAnimationFrame(frame);
  // ---- a phone: one thing at a time ---------------------------------------------------------------------------------
  // On a narrow screen the page is a very long scroll (manager, formation, the whole squad table, instructions, commentary, facts).
  // These two tab rows show one part at a time; on a wide screen they are hidden (CSS) and everything shows as before.
  function pickTab(rowId, attr, apply) {
    var row = $(rowId); if (!row) return;
    row.addEventListener("click", function (ev) {
      var b = ev.target.closest("button"); if (!b) return;
      var v = b.getAttribute(attr);
      row.querySelectorAll("button").forEach(function (o) { o.setAttribute("aria-selected", String(o === b)); });
      apply(v);
    });
  }
  pickTab("ptabs", "data-pv", function (v) { document.body.setAttribute("data-pv", v); });
  pickTab("svtabs", "data-sv", function (v) { var pane = document.querySelector('.mtab[data-pane="team"]'); if (pane) pane.setAttribute("data-sv", v); });
})();