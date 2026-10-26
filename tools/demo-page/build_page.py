import json, gzip, base64
SEEDS = (7014, 7052, 7070, 7099, 7117, 7130)  # demo matches: each has a penalty, a free-kick shot and offsides
import os, sys
sp=os.path.dirname(os.path.abspath(__file__))  # templates and audio bundle live here
work=sys.argv[1]  # directory with the e<seed>.json replays and where audio.iife.js was built
out=sys.argv[2]   # output html
t=open(sp+'/match-template.html').read()
def rep(old,new):
    global t
    assert old in t, old[:70]
    t=t.replace(old,new,1)
rep('<script id="replay" type="application/json">__REPLAY__</script>','<script id="replays" type="application/json">__REPLAYS__</script>')
rep('''  var R = JSON.parse(document.getElementById("replay").textContent);
  var F = R.frames, E = R.events, teams = [R.home, R.away];
  var PB = 9, PS = 5, OB = PB + 22 * PS;
  var t0 = F[0][0] / 100, tEnd = F[F.length - 1][0] / 100;
  var secondHalfT = Infinity;
  E.forEach(function (e) { if (e.type === "SecondHalfStart" && secondHalfT === Infinity) secondHalfT = e.t; });
  var t = t0, playing = true, speed = 1;
''','''  var PACKED = JSON.parse(document.getElementById("replays").textContent);
  var R, F, E, teams, t0, tEnd, secondHalfT, t, shown = -1;
  var PB = 9, PS = 5, OB = PB + 22 * PS;
  var playing = true, speed = 1, current = -1, addedMinutes = [0, 0];
''')
rep('''  $("hn").textContent = R.home.shortName; $("an").textContent = R.away.shortName;
  $("hc").style.background = R.home.kit.primary; $("ac").style.background = R.away.kit.primary;
  var seek = $("seek"); seek.min = t0; seek.max = tEnd;
''','''  var seek = $("seek");
''')
rep('var feed = $("feed"), shown = -1, KEY','var feed = $("feed"), KEY')
rep('''  var moments = $("moments");
  E.forEach(function (e) {''','''  var moments = $("moments");
  function buildMoments() {
    moments.textContent = "";
    E.forEach(function (e) {''')
rep('''    moments.appendChild(b);
  });''','''    moments.appendChild(b);
    });
  }
  function load(r) {
    R = r; F = R.frames; E = R.events; teams = [R.home, R.away];
    t0 = F[0][0] / 100; tEnd = F[F.length - 1][0] / 100; secondHalfT = Infinity;
    E.forEach(function (e) { if (e.type === "SecondHalfStart" && secondHalfT === Infinity) secondHalfT = e.t; });
    addedMinutes = [0, 0];
    E.forEach(function (e) { if (e.type === "AddedTime") { var m = parseInt(String(e.text).replace(/[^0-9]/g, ""), 10) || 0; if (e.t < secondHalfT) addedMinutes[0] = m; else addedMinutes[1] = m; } });
    t = t0; shown = -1;
    $("hn").textContent = R.home.shortName; $("an").textContent = R.away.shortName;
    $("hc").style.background = R.home.kit.primary; $("ac").style.background = R.away.kit.primary;
    seek.min = t0; seek.max = tEnd;
    $("seedText").textContent = R.seed;
    buildMoments();
  }
  function unpack(b64) {
    var bin = atob(b64), bytes = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return new Response(new Blob([bytes]).stream().pipeThrough(new DecompressionStream("gzip"))).json();
  }
  var cache = {};
  function newMatch() {
    var btn = $("newmatch"); btn.disabled = true; btn.textContent = "Loading...";
    var n = PACKED.length, pick = current;
    while (n > 1 && pick === current) pick = Math.floor(Math.random() * n);
    if (n === 1) pick = 0;
    var p = cache[pick] ? Promise.resolve(cache[pick]) : unpack(PACKED[pick]).then(function (r) { cache[pick] = r; return r; });
    p.then(function (r) { current = pick; load(r); playing = true; $("play").textContent = "Pause"; btn.textContent = "New match"; })
     .catch(function () { btn.textContent = "Could not load"; })
     .then(function () { btn.disabled = false; });
  }
  $("newmatch").addEventListener("click", newMatch);''')
rep('''    if (playing && t < tEnd) t''','''    if (!R) { requestAnimationFrame(frame); return; }
    if (playing && t < tEnd) t''')
rep('''  resize();
  requestAnimationFrame(frame);''','''  resize();
  newMatch();
  requestAnimationFrame(frame);''')
rep('<button id="play" type="button">Pause</button>','<button id="newmatch" type="button">New match</button>\n    <button id="play" type="button">Pause</button>')
rep('recorded at seed __SEED__','recorded at seed <span id="seedText"></span>')
rep('''      disc(X(o.x), Y(o.y), .78 * s, "#121212", "#f2c230");''','''      disc(X(o.x), Y(o.y), .8 * s, "#121212", "#f2c230");
      ctx.fillStyle = "#f2c230"; ctx.font = "800 " + Math.max(8, s) + "px Barlow, system-ui, sans-serif"; ctx.textAlign = "center"; ctx.textBaseline = "middle"; ctx.fillText(i === 0 ? "R" : "L", X(o.x), Y(o.y) + .5);''')
rep('''x = X(p.x), y = Y(p.y), g = 1 + Math.min(1.2, Math.max(0, p.j)) * .45;
      ctx.fillStyle = "rgba(0,0,0," + (.3 * (1 - Math.min(1, p.j) * .4)) + ")"; ctx.beginPath(); ctx.ellipse(x + .15 * s, y + .2 * s, r * 1.05, r * .95,''','''x = X(p.x), gy = Y(p.y), jh = Math.min(1, Math.max(0, p.j)), y = gy - jh * 2.2 * s, g = 1 + jh * .2, sh = 1 - jh * .6;
      ctx.fillStyle = "rgba(0,0,0," + (.5 * sh) + ")"; ctx.beginPath(); ctx.ellipse(x + .15 * s, gy + .2 * s, r * 1.05 * sh, r * .95 * sh,''')
rep('''ctx.fillStyle = "rgba(0,0,0," + Math.max(.12, .45 - h * .035) + ")"; ctx.beginPath(); ctx.arc(bx, by, .42 * s * Math.max(.45, 1 - h * .05), 0, Math.PI * 2); ctx.fill();
    disc(bx, by, .38 * s * (1 + Math.min(20, h) * .09), "#ffffff", "#111111");''','''ctx.fillStyle = "rgba(0,0,0," + Math.max(.18, .6 - h * .04) + ")"; ctx.beginPath(); ctx.arc(bx, by, .42 * s * Math.max(.45, 1 - h * .05), 0, Math.PI * 2); ctx.fill();
    disc(bx, by - Math.min(20, h) * .55 * s, .38 * s * (1 + Math.min(20, h) * .07), "#ffffff", "#111111");''')
exec(open(sp+'/extra.py').read())
packed=[]
for s in SEEDS:
    r=json.load(open(f'{work}/e{s}.json'))
    packed.append(base64.b64encode(gzip.compress(json.dumps(r,separators=(',',':')).encode(),9)).decode())
open(out,'w').write(t.replace('__REPLAYS__',json.dumps(packed)))
print(len(open(out).read())/1e6,'MB')
