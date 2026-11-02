  // ---- the player page: photo, age, height, feet, the five categories on a pentagon, every skill ------------------
  function gauge(v) {
    var a = Math.PI * (1 - Math.max(0, Math.min(100, v)) / 100), x = 46 + 38 * Math.cos(a), y = 48 - 38 * Math.sin(a);
    return '<svg class="pf-gauge" viewBox="0 0 92 54" aria-hidden="true"><path d="M8 48 A38 38 0 0 1 84 48" fill="none" stroke="#d8dce6" stroke-width="7"/>' +
      '<path d="M8 48 A38 38 0 0 1 ' + x.toFixed(1) + ' ' + y.toFixed(1) + '" fill="none" stroke="' + levelColour(v) + '" stroke-width="7"/>' +
      '<text x="46" y="47" text-anchor="middle" fill="' + levelColour(v) + '">' + Math.round(v) + '</text></svg>';
  }
  function footSvg(p) {
    var l = p.leftFoot || 1, r = p.rightFoot || 3, c = function (v) { return v >= 3 ? "#9bd23c" : v >= 2 ? "#e7cf2e" : "#f39a1e"; };
    var foot = function (x, v, letter) { return '<g transform="translate(' + x + ' 0)"><path d="M8 4 C14 2 18 8 17 16 C16 26 15 34 10 40 C6 44 1 41 2 34 C3 24 1 12 3 8 C4 6 6 4.6 8 4 Z" fill="' + c(v) + '"/><text x="9.5" y="17" text-anchor="middle" font-weight="800" font-size="9" fill="#1b1f2b">' + letter + '</text><text x="9" y="35" text-anchor="middle" font-weight="800" font-size="9" fill="#1b1f2b">' + v + '</text></g>'; };
    return '<svg viewBox="0 0 42 46" aria-label="Left foot ' + l + ', right foot ' + r + '">' + foot(0, l, "L") + foot(21, r, "R") + '</svg>';
  }
  function pentagonSvg(c, gk) {
    var axes = [[gk ? "GK" : "ATT", gk ? c[5] : c[3]], ["ATH", c[4]], ["PLA", c[2]], ["DEF", c[1]], ["MEN", c[0]]], cx = 130, cy = 116, Rr = 82;
    var pt = function (k, v) { var a = -Math.PI / 2 + k * 2 * Math.PI / 5; return [cx + Math.cos(a) * Rr * v, cy + Math.sin(a) * Rr * v]; };
    var poly = function (v) { return axes.map(function (_, k) { return pt(k, typeof v === "number" ? v : v[k]).map(function (n) { return n.toFixed(1); }).join(","); }).join(" "); };
    var bands = [[1, "#1e8a2a"], [.84, "#32b33d"], [.68, "#9bd23c"], [.52, "#e7cf2e"], [.36, "#f39a1e"], [.2, "#cf3a2c"]];
    return '<svg viewBox="0 0 260 220" aria-label="Skill categories">' + bands.map(function (b) { return '<polygon points="' + poly(b[0]) + '" fill="' + b[1] + '"/>'; }).join("") +
      '<polygon points="' + poly(axes.map(function (a) { return Math.max(.04, Math.min(1, a[1] / 100)); })) + '" fill="rgba(255,255,255,.12)" stroke="#fff" stroke-width="2"/>' +
      axes.map(function (a, k) { var q = pt(k, 1.2); return '<text x="' + q[0].toFixed(1) + '" y="' + (q[1] + 5).toFixed(1) + '" text-anchor="middle">' + a[0] + '</text>'; }).join("") + '</svg>';
  }
  // the player's character on the ball (from the engine): name, icon, what it does in a match
  var TRAITS = {
    CutsInsideToShoot: ["↪", "Cuts inside and shoots", "From the flank he comes inside onto his stronger foot and has a go at goal."],
    LovesToDribble: ["⚡", "Loves to dribble", "Runs with the ball and takes his man on rather than giving it away early."],
    Unselfish: ["🤝", "Sets up his team-mates", "Looks for the better-placed man and lays it on - fewer shots of his own, more assists."],
    OutsideOfTheFoot: ["🌀", "Outside of the foot", "Strikes and bends it with the outside of his boot (the trivela) instead of the weaker foot."],
    UsesWeakFoot: ["🦶", "Uses his weaker foot", "Happy on either side: he does not shift the ball onto his strong foot, and his weaker one is reliable."],
    Showboat: ["✨", "Likes tricks", "Backheels, dummies and fake shots - the flair players' repertoire, more often than most."],
    HardTackler: ["🛡", "Goes in hard", "Throws himself into tackles and slides - wins the ball back, but gives away more fouls."],
    ShootsFromDistance: ["🎯", "Shoots from distance", "Has a go from outside the box whenever he sees the target."],
    SwitchesPlay: ["↔", "Switches the play", "Looks for the long ball to the other flank to get out of a crowd."],
    BackToGoal: ["🧱", "Plays with his back to goal", "Holds the ball up, uses his body against the defender and lays it off to the runners."],
    AerialCharger: ["🦅", "Attacks every high ball", "Goes for every ball in the air, further and with a stretch - at corners, crosses and long balls."],
    HoldsPosition: ["⚓", "Holds his position", "Keeps to his post: does not roam, does not chase, does not run in behind - the rest of the team can count on him being there."],
    Roams: ["🧭", "Roams", "Leaves his post and drifts towards the ball, looking for it between the lines - and leaves a hole behind him."],
    RunsBehind: ["🏃", "Runs in behind", "Lives on the last defender's shoulder: times his runs beyond the line and calls for the ball."],
    PressesHard: ["🔥", "Presses relentlessly", "Chases the ball everywhere and closes down at once - at a cost in energy."],
    SavesEnergy: ["🔋", "Saves his energy", "Picks his moments: presses less and has more left at the end of the match."],
    Leader: ["📣", "Leader", "Organises the line (calls the step up with an arm out), makes the back line work as one, lifts the others after a goal against."],
    BigGamePlayer: ["⭐", "Big-game player", "Rises to the moment - the last quarter of an hour of a close match, extra time - and is flat when it is settled."],
    Fragile: ["🩹", "Fragile", "Tires faster and is hurt more often."],
    DrawsFouls: ["🎭", "Draws fouls", "Wins free kicks and penalties near the box - and now and then goes down too easily, and is booked for it."],
    BoldKeeper: ["🧤", "Bold keeper", "Comes off his line to sweep up behind the defence and claims crosses through the crowd."],
    StaysOnLine: ["🥅", "Stays on his line", "A shot-stopper first: stays home and trusts his reflexes."]
  };
  function traitsHtml(p) {
    var list = p.traits || [];
    if (!list.length) return '<p class="muted">No particular habits.</p>';
    return list.map(function (t, i) {
      var d = TRAITS[t] || ["•", t, ""];
      return '<div class="pf-trait" style="animation-delay:' + (i * 70) + 'ms"><span class="ic" aria-hidden="true">' + d[0] + '</span><span><b>' + esc(d[1]) + '</b><small>' + esc(d[2]) + '</small></span></div>';
    }).join("");
  }
  function openProfile(p, team) {
    if (MODE === "live" && team === 1) { toast("In a live match you cannot see the opponents' data"); return; }
    if (!p) return;
    var tm = teams[team || 0], a = p.attrs || [], names = R.attrNames || [], c = p.cats || [0, 0, 0, 0, 0, 0], gk = p.group === "Goalkeeper";
    var rows = function (from, to) { var h = ""; for (var i = from; i <= to; i++) h += '<div class="pf-row"><span>' + esc(names[i]) + '</span><b>' + (a[i] != null ? a[i] : "-") + '</b></div>'; return h; };
    var cat = function (title, v, from, to) { return '<div class="pf-cat"><h4>' + title + '</h4>' + gauge(v) + rows(from, to) + '</div>'; };
    var en = team === 0 ? energyOf(p.number) : null;
    var html = '<button type="button" class="pf-close" id="pfClose" aria-label="Close the player page">✕</button>' +
      '<div class="pf-top"><div class="pf-photo">' + jerseySvg(gk ? tm.kit.gk : tm.kit.primary, gk ? tm.kit.gkNumber : tm.kit.number, p.number, 18) + '</div>' +
      '<div class="pf-id"><span class="nm" id="pfName">' + esc((p.role || "") + " " + p.name) + '</span><span>Age: ' + esc(p.age) + '</span><span>Height: ' + esc(p.height) + ' cm</span>' +
      '<span class="pf-feet">' + footSvg(p) + '<span>Preferred foot: ' + esc(p.foot) + '<br><small class="muted">weak foot ' + esc(p.weakFoot) + '/3</small></span></span>' +
      '<span class="team">' + esc(tm.name) + ' · Num. ' + esc(p.number) + (p.nation ? ' · ' + esc(p.nation) : "") + '</span></div>' +
      '<div class="pf-pent">' + pentagonSvg(c, gk) + '</div></div>' +
      '<div class="pf-tabs" role="tablist"><button type="button" role="tab" id="pfTabAttr" aria-selected="true" aria-controls="pfAttr">ATTRIBUTES</button>' +
      '<button type="button" role="tab" id="pfTabTraits" aria-selected="false" aria-controls="pfTraits">TRAITS</button></div>' +
      '<div class="pf-traits" id="pfTraits" role="tabpanel" hidden>' + traitsHtml(p) + '</div>' +
      '<div id="pfAttr" role="tabpanel"><div class="pf-cats">' + cat("MENTAL", c[0], 0, 3) + cat("DEFENCE", c[1], 4, 6) + cat("PLAYMAKING", c[2], 7, 9) + cat("ATTACKING", c[3], 10, 12) + cat("ATHLETIC", c[4], 13, 19) + '</div>' +
      '<div class="pf-lower"><div><h4>FORM</h4><div class="pf-row"><span>Stamina</span><b>' + esc(p.stamina) + '</b></div><div class="pf-row"><span>Fitness</span><b class="fit-' + esc(p.fitness) + '">' + esc(p.fitness) + '</b></div>' +
      (en != null ? '<div class="pf-row"><span>Energy left now</span><b style="color:' + levelColour(en) + '">' + en + '%</b></div>' : "") + '</div>' +
      '<div><h4>SET PIECES</h4>' + rows(20, 21) + '</div>' + (gk ? '<div><h4>GOALKEEPING</h4>' + rows(22, 26) + '</div>' : "") + '</div></div>';
    $("profile").innerHTML = html;
    ["pfTabAttr", "pfTabTraits"].forEach(function (id) {
      $(id).addEventListener("click", function () {
        var traits = id === "pfTabTraits";
        $("pfTabAttr").setAttribute("aria-selected", String(!traits));
        $("pfTabTraits").setAttribute("aria-selected", String(traits));
        $("pfAttr").hidden = traits;
        $("pfTraits").hidden = !traits;
        if (traits) $("pfTraits").innerHTML = traitsHtml(p); // (replays the entrance)
      });
    });
    $("profileBack").hidden = false;
    $("pfClose").focus();
  }
  function closeProfile() { $("profileBack").hidden = true; }
  $("profileBack").addEventListener("click", function (ev) { if (ev.target === this || ev.target.closest("#pfClose")) closeProfile(); });
  document.addEventListener("keydown", function (ev) { if (ev.key === "Escape" && !$("profileBack").hidden) closeProfile(); });
