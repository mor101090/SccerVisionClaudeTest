  // ---- Team tactic (you are the home team) -----------------------------------------------------------
  // Line and width 0-100; passing 0 short / 1 mixed / 2 balls in behind; tempo and style 0 / 1 / 2. In a live match an
  // instruction goes to the players at once (the computer answers it); otherwise the match is replayed with it.
  var TAC_DEFAULT = { line: 50, width: 50, passing: 1, tempo: 1, style: 1, defence: 0, attack: 0, special: 0 };
  var USER_TAC = null;
  var TAC = (function () { var o = {}; for (var k in TAC_DEFAULT) o[k] = TAC_DEFAULT[k]; try { var st = JSON.parse(localStorage.getItem("sv-tactics") || "{}"); for (var k2 in st) if (k2 in TAC_DEFAULT) o[k2] = +st[k2]; } catch (e) {} return o; })();
  function tacEngine() { return { line: TAC.line / 100, width: TAC.width / 100, passing: TAC.passing, tempo: [.15, .5, .85][TAC.tempo], aggression: [0, .5, 1][TAC.style], defence: TAC.defence, attack: TAC.attack, special: TAC.special }; }
  function words(v, list) { return list[Math.min(list.length - 1, Math.floor(v / (100.01 / list.length)))]; }
  var LINE_WORDS = ["Deep block", "Low", "Middle", "High", "Very high"], WIDTH_WORDS = ["Very narrow", "Narrow", "Normal", "Wide", "Very wide"];
  var SHAPE = { d: [-.75, -.26, .26, .75], m: [-.8, -.26, .26, .8], a: [-.24, .24] };
  // The line and width the players really play, with the chosen styles applied (as TeamTactics.Effective in the engine).
  function effTac() {
    var line = TAC.line, width = TAC.width, sh = function (v, d) { return Math.max(0, Math.min(100, v + d)); };
    if (TAC.defence === 1) line = sh(line, 22); else if (TAC.defence === 2) line = sh(line, 18); else if (TAC.defence === 3) { line = sh(line, -35); width = sh(width, -25); } else if (TAC.defence === 4) line = sh(line, 34);
    if (TAC.attack === 1) width = sh(width, 25);
    if (TAC.special === 2 || TAC.special === 3) line = sh(line, -15); else if (TAC.special === 4) { line = sh(line, 30); width = sh(width, 20); }
    return { line: line, width: width };
  }