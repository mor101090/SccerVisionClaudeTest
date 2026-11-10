"""
How corners play out in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

For every corner kick of a competition and season: what happens first (the first event after the corner pass that is not a pressure, a receipt or a
carry): by the side that took the corner or by the other side, its type (Shot, Pass, Clearance, Goal Keeper, Interception, Block, Duel, Ball Recovery,
Foul Won, ...) and the body part (head or foot) of a shot, a clearance or a pass; and how the corner pass itself ended (completed or not, short or into
the box). Only per-match summaries are cached (summary_cornerout_<c>_<s>.json).

Usage: python3 -I corner_outcomes.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request
from collections import Counter

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_cornerout_{comp}_{season}.json")
YARD = 0.9144
SKIP = ("Pressure", "Ball Receipt*", "Carry")


def get(url):
    for attempt in range(5):
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                return json.loads(r.read())
        except (urllib.error.URLError, TimeoutError, http.client.IncompleteRead, ConnectionError):
            time.sleep(2 ** attempt)
    with urllib.request.urlopen(url, timeout=120) as r:
        return json.loads(r.read())


def body(e):
    for key in ("shot", "clearance", "pass"):
        if key in e and "body_part" in e[key]:
            return "head" if e[key]["body_part"]["name"] == "Head" else "foot"
    return "?"


summaries = json.load(open(path)) if os.path.exists(path) else {}
matches = get(f"{BASE}/matches/{comp}/{season}.json")[:limit]
for m in matches:
    mid = str(m["match_id"])
    if mid in summaries:
        continue
    events = get(f"{BASE}/events/{mid}.json")
    c = Counter()
    for i, e in enumerate(events):
        p = e.get("pass")
        if e["type"]["name"] != "Pass" or not p or p.get("type", {}).get("name") != "Corner":
            continue
        c["corners"] += 1
        c["corner." + ("completed" if "outcome" not in p else p["outcome"]["name"])] += 1
        length = p.get("length", 0) * YARD
        c["corner.length." + ("short(<15m)" if length < 15 else "box")] += 1
        j = i + 1
        while j < len(events) and events[j]["type"]["name"] in SKIP:
            j += 1
        if j >= len(events):
            continue
        nx = events[j]
        side = "att" if nx["team"]["name"] == e["team"]["name"] else "def"
        kind = nx["type"]["name"]
        c[f"first.{side}.{kind}" + (f".{body(nx)}" if kind in ("Shot", "Clearance", "Pass") else "")] += 1
    summaries[mid] = dict(c)
    json.dump(summaries, open(path, "w"))

n = len(summaries)
tot = Counter()
for s in summaries.values():
    tot.update(s)
k = tot["corners"]
print(f"matches {n}: corners {k / n:.2f} a match")
print("  the corner pass: " + ", ".join(f"{key[7:]} {100 * v / k:.1f}%" for key, v in sorted(tot.items(), key=lambda t: -t[1]) if key.startswith("corner.") and not key.startswith("corner.length")))
print("  length: " + ", ".join(f"{key[14:]} {100 * v / k:.1f}%" for key, v in sorted(tot.items(), key=lambda t: -t[1]) if key.startswith("corner.length")))
print("  the first thing that happens after it, as % of corners:")
for key, v in sorted(((key[6:], v) for key, v in tot.items() if key.startswith("first.")), key=lambda t: -t[1])[:16]:
    print(f"    {key:<28} {100 * v / k:5.1f}%")
