"""
Direct free-kick shots in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

For every match of a competition and season: direct free-kick shots a match (both teams), their outcome (goal, saved, blocked -
nearly always by the wall -, off target, post), technique and distance, the height they crossed the goal line at (end_location z,
when StatsBomb gives it), and from the shot's freeze frame: the wall (the largest row of opponents shoulder to shoulder 8.2-11.2 m from
the ball, nearer the goal) and where the keeper stood across his goal, measured towards the side the wall covers (+) or away from it (-).
Only per-match summaries are cached (summary_freekicks_<c>_<s>.json).

Usage: python3 -I free_kicks.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_freekicks_{comp}_{season}.json")
YARD = 0.9144


def get(url):
    for attempt in range(5):
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                return json.loads(r.read())
        except (urllib.error.URLError, TimeoutError, http.client.IncompleteRead, ConnectionError):
            time.sleep(2 ** attempt)
    with urllib.request.urlopen(url, timeout=120) as r:
        return json.loads(r.read())


summaries = json.load(open(path)) if os.path.exists(path) else {}
matches = get(f"{BASE}/matches/{comp}/{season}.json")[:limit]
for m in matches:
    mid = str(m["match_id"])
    if mid in summaries:
        continue
    events = get(f"{BASE}/events/{mid}.json")
    shots = []
    for e in events:
        if e["type"]["name"] != "Shot" or e["shot"]["type"]["name"] != "Free Kick":
            continue
        x, y = e["location"][:2]
        end = e["shot"].get("end_location", [])
        dist = ((120 - x) ** 2 + (40 - y) ** 2) ** 0.5 * YARD
        opp, keeper = [], None
        for f in e["shot"].get("freeze_frame", []):
            if f["teammate"]:
                continue
            fx, fy = f["location"]
            if f["position"]["name"] == "Goalkeeper":
                keeper = [round(fx, 1), round(fy, 1)]
            else:
                opp.append([round(fx, 1), round(fy, 1)])
        shots.append({
            "outcome": e["shot"]["outcome"]["name"],
            "technique": e["shot"]["technique"]["name"],
            "loc": [x, y],
            "dist": round(dist, 1),
            "xg": e["shot"].get("statsbomb_xg", 0.0),
            "z": round(end[2] * YARD, 2) if len(end) > 2 else None,
            "end": [round(end[0], 1), round(end[1], 1)] if len(end) > 1 else None,
            "opp": opp,
            "keeper": keeper,
        })
    summaries[mid] = shots
    json.dump(summaries, open(path, "w"))

n = len(summaries)
shots = [s for v in summaries.values() for s in v]
k = len(shots)
print(f"matches {n}: direct free-kick shots a match {k / n:.2f} (both teams), xG a shot {sum(s['xg'] for s in shots) / max(1, k):.3f}")
outcomes = {}
for s in shots:
    outcomes[s["outcome"]] = outcomes.get(s["outcome"], 0) + 1
for o, c in sorted(outcomes.items(), key=lambda t: -t[1]):
    print(f"  {o:>14}: {c:4d} ({100 * c / max(1, k):4.1f}%)")
for lo, hi in ((0, 20), (20, 25), (25, 30), (30, 40), (40, 100)):
    b = [s for s in shots if lo <= s["dist"] < hi]
    g = sum(1 for s in b if s["outcome"] == "Goal")
    bl = sum(1 for s in b if s["outcome"] == "Blocked")
    print(f"  {lo:>2}-{hi:<3} m: {len(b):4d} shots, goals {100 * g / max(1, len(b)):4.1f}%, blocked {100 * bl / max(1, len(b)):4.1f}%")
z = [s["z"] for s in shots if s["z"] is not None and s["outcome"] in ("Goal", "Saved", "Saved To Post", "Post")]
if z:
    low = sum(1 for v in z if v < 0.5)
    print(f"  on target with a height: {len(z)}, crossing the line under 0.5 m {100 * low / len(z):.1f}%, median {sorted(z)[len(z) // 2]:.2f} m")
def wall_of(s):
    """The wall: the largest row of opponents standing shoulder to shoulder (gaps of 1.2 m or less across the line from the ball to
    the goal) at 8.2-11.2 m from the ball (the 9.15 m of Law 13, as StatsBomb places them), nearer the goal than the ball."""
    (x, y) = s["loc"]
    gx, gy = 120 - x, 40 - y
    L = (gx * gx + gy * gy) ** 0.5
    across = []
    for fx, fy in s["opp"]:
        dx, dy = fx - x, fy - y
        d = (dx * dx + dy * dy) ** 0.5 * YARD
        if 8.2 <= d <= 11.2 and (dx * gx + dy * gy) > 0:
            across.append((dx * gy - dy * gx) / L * YARD)
    across.sort()
    best, run = 0, 0
    for i, a in enumerate(across):
        run = run + 1 if i > 0 and a - across[i - 1] <= 1.2 else 1
        best = max(best, run)
    return best


def keeper_of(s):
    """The keeper across his goal (m), + towards the side of the goal the ball is on (the post the wall covers)."""
    if not s["keeper"]:
        return None
    side = 1 if s["loc"][1] > 40 else -1
    return (s["keeper"][1] - 40) * YARD * side


for s in shots:
    s["wall"], s["kp"] = wall_of(s), keeper_of(s)
walls = [s["wall"] for s in shots]
# the side the shot went to: towards the near post (the side the ball is on: over or round the wall) or across to the keeper's side
sided = [s for s in shots if s.get("end") and abs(s["loc"][1] - 40) * YARD > 3 and s["outcome"] != "Blocked"]
near = sum(1 for s in sided if (s["end"][1] - 40) * (s["loc"][1] - 40) > 0)
if sided:
    print(f"  not blocked, ball more than 3 m off centre (n={len(sided)}): ended on the wall's (near) side {100 * near / len(sided):.0f}%, "
          f"the keeper's side {100 * (len(sided) - near) / len(sided):.0f}%")
print(f"  wall (the largest row shoulder to shoulder 8.2-11.2 m from the ball): mean {sum(walls) / max(1, k):.2f}; none {100 * sum(1 for w in walls if w == 0) / max(1, k):.0f}%")
kp = [(s["kp"], s["dist"]) for s in shots if s["kp"] is not None and s["wall"] >= 2 and abs(s["loc"][1] - 40) * YARD > 3]
if kp:
    v = sorted(p for p, _ in kp)
    print(f"  keeper across his goal, ball more than 3 m off centre, wall of 2+ (n={len(kp)}): median {v[len(v) // 2]:+.2f} m (+ = towards the wall post), "
          f"quartiles {v[len(v) // 4]:+.2f} / {v[3 * len(v) // 4]:+.2f}")
