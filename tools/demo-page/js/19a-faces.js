  // ---- faces: a portrait made for the player (tools/faces), or a vector one from the same data; the shirt is drawn over it ----
  // The portrait is made once, when the player is created, in a few ages (faces/<seed>/<age>.webp, listed in faces/manifest.json);
  // the one for his age (the nearest below it) is shown, so he ages with the years. Without a portrait the vector face below
  // is drawn from his look - skin, hair, beard, build, greying and lines with his age - and the shirt takes the team's colours.
  var FACES = null;
  fetch("faces/manifest.json").then(function (r) { return r.ok ? r.json() : null; }).then(function (m) { FACES = m; }, function () {});
  function faceUrl(look, age) {
    var ages = FACES && look && FACES[String(look.seed)];
    if (!ages || !ages.length) return null;
    var best = ages[0]; ages.forEach(function (a) { if (a <= age) best = a; });
    return "faces/" + look.seed + "/" + best + ".webp";
  }
  var SKIN = { 1: "#f6d9c4", 2: "#efc6a6", 3: "#d9a27a", 4: "#bd825a", 5: "#8a5a3c", 6: "#5e3a26" };
  var HAIRC = { black: "#1b1714", "dark-brown": "#33241b", brown: "#52382a", "light-brown": "#7a5a3c", blond: "#c9a965", red: "#9a4a25" };
  function mixHex(a, b, k) { var x = parseInt(a.slice(1), 16), y = parseInt(b.slice(1), 16), m = function (s) { return Math.round(((x >> s) & 255) * (1 - k) + ((y >> s) & 255) * k); }; return "#" + ((1 << 24) | (m(16) << 16) | (m(8) << 8) | m(0)).toString(16).slice(1); }
  // the vector face: viewBox 0 0 100 120, head and shoulders; the shirt (kit colours) at the bottom
  function avatarSvg(look, age, kit, gk, number) {
    look = look || {}; age = age || 25;
    var skin = SKIN[look.skin] || SKIN[3], shade = mixHex(skin, "#000000", .16);
    var grey = Math.max(0, Math.min(1, (age - 31) / 20)), hair = mixHex(HAIRC[look.hairColour] || HAIRC["dark-brown"], "#b9b9b9", grey * .85);
    var shoulders = look.build === "stocky" ? 8 : look.build === "slim" ? 20 : 14, st = look.hair, fa = look.facial;
    var s = '<svg viewBox="0 0 100 120" aria-hidden="true">';
    // shirt and neck
    s += '<path d="M' + shoulders + ' 120 Q' + (shoulders + 4) + ' 94 34 90 L66 90 Q' + (96 - shoulders) + ' 94 ' + (100 - shoulders) + ' 120Z" fill="' + (gk ? kit.gk : kit.primary) + '" stroke="' + (gk ? kit.gkTrim || kit.secondary : kit.secondary) + '" stroke-width="2.2"/>';
    s += '<path d="M40 90 L50 101 L60 90" fill="none" stroke="' + (gk ? kit.gkTrim || kit.secondary : kit.secondary) + '" stroke-width="2.2"/>';
    s += '<rect x="42" y="74" width="16" height="20" rx="6" fill="' + shade + '"/>';
    // hair behind the head (long, afro)
    if (st === "afro") s += '<circle cx="50" cy="38" r="31" fill="' + hair + '"/>';
    if (st === "long") s += '<path d="M24 40 Q22 78 30 86 L70 86 Q78 78 76 40Z" fill="' + hair + '"/>';
    // ears and head
    s += '<ellipse cx="29" cy="52" rx="4" ry="7" fill="' + shade + '"/><ellipse cx="71" cy="52" rx="4" ry="7" fill="' + shade + '"/>';
    s += '<path d="M30 40 Q30 16 50 16 Q70 16 70 40 L70 58 Q70 80 50 84 Q30 80 30 58Z" fill="' + skin + '"/>';
    // beard
    if (fa === "stubble") s += '<path d="M31 56 Q32 80 50 84 Q68 80 69 56 Q60 66 50 66 Q40 66 31 56Z" fill="' + hair + '" opacity=".28"/>';
    if (fa === "short-beard") s += '<path d="M30 54 Q30 82 50 86 Q70 82 70 54 Q62 68 50 68 Q38 68 30 54Z" fill="' + hair + '" opacity=".92"/>';
    if (fa === "full-beard") s += '<path d="M29 50 Q28 90 50 96 Q72 90 71 50 Q64 70 50 71 Q36 70 29 50Z" fill="' + hair + '"/>';
    // eyes, brows, nose, mouth
    s += '<ellipse cx="41" cy="50" rx="3" ry="2" fill="#fff"/><ellipse cx="59" cy="50" rx="3" ry="2" fill="#fff"/><circle cx="41" cy="50" r="1.5" fill="#2a1d15"/><circle cx="59" cy="50" r="1.5" fill="#2a1d15"/>';
    s += '<path d="M36 44 Q41 41 46 44M54 44 Q59 41 64 44" fill="none" stroke="' + hair + '" stroke-width="2" stroke-linecap="round"/>';
    s += '<path d="M50 52 Q47 62 51 63" fill="none" stroke="' + shade + '" stroke-width="1.6" stroke-linecap="round"/>';
    s += fa === "moustache" || fa === "short-beard" || fa === "full-beard" ? '<path d="M42 68 Q50 64 58 68 Q50 70 42 68Z" fill="' + hair + '"/>' : '<path d="M43 70 Q50 74 57 70" fill="none" stroke="' + mixHex(skin, "#7a2f2f", .5) + '" stroke-width="1.8" stroke-linecap="round"/>';
    // the years: lines on the forehead and round the eyes
    if (age >= 33) s += '<path d="M40 36 L60 36" stroke="' + shade + '" stroke-width=".9" opacity="' + Math.min(.8, (age - 30) / 20) + '"/><path d="M38 54 L35 57M62 54 L65 57" stroke="' + shade + '" stroke-width=".9" opacity="' + Math.min(.8, (age - 30) / 20) + '"/>';
    if (age >= 40) s += '<path d="M41 40 L59 40M44 73 Q50 75 56 73" stroke="' + shade + '" stroke-width=".9" fill="none" opacity=".6"/>';
    // hair on top
    var cap = '<path d="M29 42 Q27 12 50 12 Q73 12 71 42 Q68 28 50 27 Q32 28 29 42Z" fill="' + hair + '"/>';
    if (st === "short" || st === "long" || st === "wavy") s += cap;
    if (st === "wavy") s += '<path d="M28 40 Q24 30 30 24M72 40 Q76 30 70 24" fill="none" stroke="' + hair + '" stroke-width="5" stroke-linecap="round"/>';
    if (st === "buzz") s += '<path d="M30 40 Q29 14 50 14 Q71 14 70 40 Q66 28 50 26 Q34 28 30 40Z" fill="' + hair + '" opacity=".55"/>';
    if (st === "curly") s += [[34, 24], [44, 19], [56, 19], [66, 24], [30, 34], [70, 34], [50, 17]].map(function (c) { return '<circle cx="' + c[0] + '" cy="' + c[1] + '" r="7" fill="' + hair + '"/>'; }).join("");
    if (st === "afro") s += '<path d="M31 40 Q31 18 50 18 Q69 18 69 40 Q66 30 50 29 Q34 30 31 40Z" fill="' + hair + '"/>';
    if (st === "receding") s += '<path d="M29 42 Q28 22 40 16 Q50 14 60 16 Q72 22 71 42 Q68 34 62 30 Q56 22 50 24 Q44 22 38 30 Q32 34 29 42Z" fill="' + hair + '"/>';
    if (st === "bald") s += '<path d="M29 44 Q28 36 31 32 M71 44 Q72 36 69 32" stroke="' + hair + '" stroke-width="3.5" fill="none" stroke-linecap="round" opacity=".7"/>';
    if (number != null && number !== "") s += '<text x="50" y="112" text-anchor="middle" font-family="Barlow, sans-serif" font-weight="800" font-size="14" fill="' + (gk ? kit.gkNumber : kit.number) + '">' + esc(number) + '</text>';
    return s + "</svg>";
  }
  function loadImg(src, ok, bad) { var i = new Image(); i.onload = function () { ok(i); }; i.onerror = bad || function () {}; i.src = src; }
  function paintFaces() {
    document.querySelectorAll("canvas[data-face]:not([data-done])").forEach(function (cv) {
      cv.setAttribute("data-done", "1");
      var src = cv.getAttribute("data-face"), col = cv.getAttribute("data-col");
      loadImg(src, function (face) {
        cv.width = face.naturalWidth; cv.height = face.naturalHeight;
        var g = cv.getContext("2d"); g.drawImage(face, 0, 0);
        loadImg(src.replace(/\.webp$/, ".shirt.png"), function (mask) {
          var o = document.createElement("canvas"); o.width = cv.width; o.height = cv.height;
          var c = o.getContext("2d");
          c.drawImage(face, 0, 0);
          c.globalCompositeOperation = "multiply"; c.fillStyle = col; c.fillRect(0, 0, o.width, o.height);
          c.globalCompositeOperation = "destination-in"; c.drawImage(mask, 0, 0, o.width, o.height);
          g.drawImage(o, 0, 0);
          var svg = cv.parentNode && cv.parentNode.querySelector("svg"); if (svg) svg.remove(); cv.parentNode.classList.add("whole");
        });
      }, function () { cv.remove(); });
    });
  }
  // the portrait over the shirt of the team's colours, or the vector face
  function faceHtml(p, kit, gk) {
    var url = faceUrl(p.look, p.age);
    if (!url) return avatarSvg(p.look, p.age, kit, gk, p.number);
    var sh = '<svg viewBox="0 0 100 120" aria-hidden="true"><path d="M10 120 Q14 94 34 90 L66 90 Q86 94 90 120Z" fill="' + (gk ? kit.gk : kit.primary) + '" stroke="' + (gk ? kit.gkTrim || kit.secondary : kit.secondary) + '" stroke-width="2.2"/>' +
      '<text x="50" y="112" text-anchor="middle" font-family="Barlow, sans-serif" font-weight="800" font-size="14" fill="' + (gk ? kit.gkNumber : kit.number) + '">' + esc(p.number) + '</text></svg>';
    // the portrait; when it has its shirt mask (<age>.shirt.png: the shirt in grey shading) the shirt is coloured in the kit
    // with its real folds and light, and the drawn shirt goes; otherwise the drawn shirt stays over the portrait
    var col = gk ? kit.gk : kit.primary;
    setTimeout(paintFaces, 0);
    return '<div class="face-stack"><canvas class="face-img" data-face="' + url + '" data-col="' + esc(col) + '"></canvas>' + sh + '</div>';
  }
