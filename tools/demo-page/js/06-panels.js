  // ---- panels ----------------------------------------------------------------------------------------
  var feed = $("feed"), KEY = { Goal: 1, YellowCard: 1, RedCard: 1, PenaltyAwarded: 1, Offside: 1, HitWoodwork: 1, Injury: 1, Substitution: 1, ExtraTime: 1, PenaltyShootout: 1, ShootoutKick: 1, Skill: 1 };
  var moments = $("moments");
  function addMoment(e) {
    if (!KEY[e.type] && !(e.type === "Shot" && (e.detail === 3 || e.detail === 4)) && !(e.type === "FreeKick" && e.detail === 1)) return;
    var b = document.createElement("button"); b.type = "button";
    b.innerHTML = '<span class="m"></span><span></span>';
    b.children[0].textContent = e.minute; b.children[1].textContent = e.text; b.children[1].className = "tone-" + e.tone;
    b.addEventListener("click", function () {
      jump(Math.max(t0, e.t - 8));
      // (on a phone the list is far below the pitch: take the viewer to the moment, not just move the clock)
      try { cv.scrollIntoView({ block: "center", behavior: "smooth" }); } catch (x) { cv.scrollIntoView(); } // (on a computer too)
      if (!playing) setPlaying(true);
    });
    moments.appendChild(b);
  }
