"""
How open-play crosses end in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

For every open-play cross (pass.cross, not from a corner, a free kick, a throw-in, a kick-off or a goal kick) of a competition and season:
how it ended (completed, or the pass outcome: Out, Incomplete, Offside, Unknown), who touched it first and how far from where it was struck (the other
team's first event that is not a pressure or a receipt: its type - Clearance, Interception, Block, Ball Recovery, Duel, Goal Keeper, ... - and its distance
from the crosser: under 10 yards, 10-30, 30+), the cross's length, its height, and whether the crosser was under pressure. Only per-match summaries are cached
(summary_crossout_<c>_<s>.json).

Usage: python3 -I cross_outcomes.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request
from collections import Counter

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_crossout2_{comp}_{season}.json")
SET_PIECES = ("Corner", "Free Kick", "Throw-in", "Kick Off", "Goal Kick")
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


def dist_bucket(d):
    return "<10" if d < 10 else "10-30" if d < 30 else "30+"


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
        if e["type"]["name"] != "Pass" or not p or not p.get("cross") or p.get("type", {}).get("name") in SET_PIECES:
            continue
        c["crosses"] += 1
        ox, oy = e["location"][:2]
        gl = (120 - ox) * YARD
        wide = abs(oy - 40) * YARD
        c["origin.fromGoalLine." + ("<6m" if gl < 6 else "6-12m" if gl < 12 else "12-18m" if gl < 18 else "18-26m" if gl < 26 else "26m+")] += 1
        c["origin.fromMiddle." + ("<9m" if wide < 9 else "9-18m" if wide < 18 else "18-26m" if wide < 26 else "26m+")] += 1
        end = p.get("end_location")
        if end:
            c["end.fromGoalLine." + ("<6m" if (120 - end[0]) * YARD < 6 else "6-12m" if (120 - end[0]) * YARD < 12 else "12-18m" if (120 - end[0]) * YARD < 18 else "18m+")] += 1
        outcome = "completed" if "outcome" not in p else p["outcome"]["name"]
        c["outcome." + outcome] += 1
        c["height." + p.get("height", {}).get("name", "?")] += 1
        c["pressure." + ("yes" if e.get("under_pressure") else "no")] += 1
        length = p.get("length", 0) * YARD
        c["length." + ("<15" if length < 15 else "15-25" if length < 25 else "25+")] += 1
        if outcome == "completed":
            continue
        j = i + 1
        while j < len(events) and (events[j]["type"]["name"] in ("Pressure", "Ball Receipt*", "Carry") or events[j]["team"]["name"] == e["team"]["name"]):
            if events[j]["team"]["name"] == e["team"]["name"] and events[j]["type"]["name"] not in ("Pressure", "Ball Receipt*", "Carry"):
                break  # the crosser's side played on: the other side never touched it
            j += 1
        if j >= len(events) or events[j]["team"]["name"] == e["team"]["name"] or "location" not in events[j]:
            c["first.none." + outcome] += 1
            continue
        nx = events[j]
        x, y = nx["location"][:2]
        x, y = 120 - x, 80 - y
        d = ((x - e["location"][0]) ** 2 + (y - e["location"][1]) ** 2) ** 0.5 * YARD
        c["first." + nx["type"]["name"] + "." + dist_bucket(d / YARD)] += 1
    summaries[mid] = dict(c)
    json.dump(summaries, open(path, "w"))

n = len(summaries)
tot = Counter()
for s in summaries.values():
    tot.update(s)
k = tot["crosses"]
print(f"matches {n}: open-play crosses {k / n:.2f} a match")
for pre in ("outcome.", "height.", "pressure.", "length.", "origin.fromGoalLine.", "origin.fromMiddle.", "end.fromGoalLine."):
    print("  " + pre[:-1] + ": " + ", ".join(f"{key[len(pre):]} {100 * v / k:.1f}%" for key, v in sorted(tot.items(), key=lambda t: -t[1]) if key.startswith(pre)))
inc = k - tot["outcome.completed"]
print(f"  not completed ({100 * inc / k:.1f}% of crosses), by what the other side did first (and how far from the crosser), as % of all crosses:")
firsts = sorted(((key[6:], v) for key, v in tot.items() if key.startswith("first.")), key=lambda t: -t[1])
for key, v in firsts[:18]:
    print(f"    {key:<28} {100 * v / k:5.1f}%")
near = sum(v for key, v in firsts if key.endswith(".<10"))
print(f"  the other side's first action within 10 yards of the crosser: {100 * near / k:.1f}% of crosses")
