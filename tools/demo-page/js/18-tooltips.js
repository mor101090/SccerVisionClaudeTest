  // ---- tooltips: every team instruction, role and individual instruction explains itself ---------------------------
  var TEAM_TIPS = {
    passing: ["Short passing combinations: keep the ball on the ground, one-twos and third-man runs.", "A mix of short and long, as the situation asks.", "Balls in behind the defence and long passes to the forwards."],
    tempo: ["Slow, patient build-up: the players take their time on the ball.", "Normal tempo.", "Fast and direct: the ball is moved forward quickly."],
    style: ["Gentle: stay on your feet, few fouls, less pressing.", "Balanced pressing and tackling.", "Aggressive: press hard, get stuck in - more fouls and cards."],
    defence: ["Normal shape and pressing.", "Win the ball high up the pitch - tires the players faster.", "The whole line steps up together to catch the forwards offside.", "A bunker in front of the box, wasting time.", "The back four squeeze up to halfway, the whole team compact, the keeper sweeping behind - beaten by balls over the top.", "In possession the full-backs come inside into midfield beside the holding man - control in the middle, but the flanks are open."],
    attack: ["A bit of everything.", "Wide men deliver into the box; the forwards attack the crosses.", "Through balls and runs behind the defence.", "Shoots on sight - from the edge of the box and from distance."],
    special: ["Balanced: no special style - the team plays as the other settings say.", "Keep the ball, no gambles: short passes and a patient build-up.", "Slow restarts, hold the ball, keep the lead.", "Sit deeper and break fast the moment the ball is won.", "Everybody up round the box: relentless pressure (tires the legs)."],
  };
  document.querySelectorAll(".tac-seg[data-tac]").forEach(function (g) {
    var k = g.getAttribute("data-tac");
    g.querySelectorAll("button").forEach(function (b) { var tip = (TEAM_TIPS[k] || [])[+b.getAttribute("data-v")]; if (tip) { b.setAttribute("data-tip", b.textContent + "|" + tip); b.removeAttribute("title"); } });
  });
  $("tacLine").setAttribute("data-tip", "Defensive line|How high the back four defend: a deep block on the edge of the box to a high line near halfway (the whole team moves with it).");
  $("tacWidth").setAttribute("data-tip", "Width|How far the players spread across the pitch with and without the ball.");
  (function () {
    var tip = $("tip"), cur = null;
    function show(el, x, y) {
      var parts = String(el.getAttribute("data-tip")).split("|");
      tip.innerHTML = parts.length > 1 ? "<b>" + esc(parts[0]) + "</b>" + esc(parts.slice(1).join("|")) : esc(parts[0]);
      tip.hidden = false;
      var w = tip.offsetWidth, h = tip.offsetHeight;
      tip.style.left = Math.max(6, Math.min(innerWidth - w - 6, x + 14)) + "px";
      tip.style.top = Math.max(6, y - h - 12 < 6 ? y + 18 : y - h - 12) + "px";
    }
    // On a touch screen there is no hovering: a tooltip shown on the tap's emulated mouseover makes iOS treat the first tap
    // as a hover and swallow the click (it then needs a second tap) - so no tooltips and no hover cards on touch.
    document.addEventListener("pointerdown", function (ev) { TOUCH = ev.pointerType === "touch"; if (TOUCH) { cur = null; tip.hidden = true; } }, true);
    document.addEventListener("mouseover", function (ev) { if (TOUCH) return; var el = ev.target.closest && ev.target.closest("[data-tip]"); if (el !== cur) { cur = el; if (!el) tip.hidden = true; else show(el, ev.clientX, ev.clientY); } });
    document.addEventListener("mousemove", function (ev) { if (TOUCH) return; if (cur) show(cur, ev.clientX, ev.clientY); });
    document.addEventListener("focusin", function (ev) { if (TOUCH) return; var el = ev.target.closest && ev.target.closest("[data-tip]"); if (el) { var r = el.getBoundingClientRect(); cur = el; show(el, r.left, r.top); } });
    document.addEventListener("focusout", function () { cur = null; tip.hidden = true; });
    document.addEventListener("scroll", function () { cur = null; tip.hidden = true; }, true);
  })();
