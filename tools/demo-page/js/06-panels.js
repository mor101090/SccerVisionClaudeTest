  // ---- panels ----------------------------------------------------------------------------------------
  var feed = $("feed"), KEY = { Goal: 1, YellowCard: 1, RedCard: 1, PenaltyAwarded: 1, HitWoodwork: 1, Injury: 1, Substitution: 1, ExtraTime: 1, PenaltyShootout: 1, ShootoutKick: 1 };
  // The colours of the lines (the same in the key moments and in the commentary): a goal green, a yellow card yellow, a red card red,
  // a substitution light blue, the woodwork orange - everything else in the plain colour. (A trick - a fake shot, a heel flick -
  // is not an event a coach needs: it has no line at all.)
  var LINE_CLASS = { Goal: "ln-goal", YellowCard: "ln-yellow", RedCard: "ln-red", Substitution: "ln-sub", HitWoodwork: "ln-wood" };
  function lineClass(e) { return LINE_CLASS[e.type] || ""; }
  // a dangerous chance: a shot the model rated well (the model's xG runs high: 0.3 is a clear chance)
  function bigChance(e) { return e.type === "Shot" && e.v >= 0.3 && e.detail !== 4; }
  var moments = $("moments");
  function addMoment(e) {
    if (!KEY[e.type] && !bigChance(e) && !(e.type === "Shot" && (e.detail === 3 || e.detail === 4)) && !(e.type === "FreeKick" && e.detail === 1)) return;
    var b = document.createElement("button"); b.type = "button";
    b.innerHTML = '<span class="m"></span><span></span>';
    b.children[0].textContent = e.minute; b.children[1].textContent = e.text; b.children[1].className = lineClass(e);
    b.addEventListener("click", function () {
      jump(Math.max(t0, e.t - 8));
      // (on a phone the list is far below the pitch: take the viewer to the moment, not just move the clock)
      try { cv.scrollIntoView({ block: "center", behavior: "smooth" }); } catch (x) { cv.scrollIntoView(); } // (on a computer too)
      if (!playing) setPlaying(true);
    });
    moments.appendChild(b);
  }
