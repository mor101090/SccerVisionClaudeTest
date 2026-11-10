"""
One-twos and third-man combinations in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

A one-two: A passes to B (open play, completed), and B's next pass goes back to A, completed, within 6 s of B receiving it, and A
receives it at least 5 m further forward than he played it. Immediate when B's pass is his first touch (no carry between), delayed
otherwise. The return pass is recorded by its technique (ground / low / high, StatsBomb's pass.height) and body part.
A third-man combination (an approximation: the data has no positions off the ball): A passes to B, B lays it first time (no carry)
within 2 s on to C (not A), completed, and C receives it 10 m or more further forward than A played the first pass.
Only per-match summaries are cached (summary_combinations_<c>_<s>.json).

Usage: python3 -I combinations.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request
from collections import Counter

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_combinations_{comp}_{season}.json")
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


def t(e):
    return e["minute"] * 60 + e["second"] + (e["timestamp"][-3:] and int(e["timestamp"][-3:]) / 1000.0)


summaries = json.load(open(path)) if os.path.exists(path) else {}
matches = get(f"{BASE}/matches/{comp}/{season}.json")[:limit]
for m in matches:
    mid = str(m["match_id"])
    if mid in summaries:
        continue
    events = get(f"{BASE}/events/{mid}.json")
    # the open-play passes that reached a team-mate, in order
    passes = [e for e in events if e["type"]["name"] == "Pass" and "outcome" not in e["pass"] and e["pass"].get("type", {}).get("name") not in SET_PIECES
              and "recipient" in e["pass"]]
    by_index = {e["index"]: e for e in events}
    s = Counter()
    for i in range(len(passes) - 1):
        a, b = passes[i], passes[i + 1]
        if a["period"] != b["period"] or a["possession"] != b["possession"]:
            continue
        if b["player"]["id"] != a["pass"]["recipient"]["id"]:
            continue
        # B's touches between receiving and passing: a carry means he took it on first
        carried = any(by_index.get(k, {}).get("type", {}).get("name") == "Carry" and by_index[k].get("player", {}).get("id") == b["player"]["id"]
                      for k in range(a["index"] + 1, b["index"]))
        dt = t(b) - t(a)
        if b["pass"]["recipient"]["id"] == a["player"]["id"]:
            gain = (b["pass"]["end_location"][0] - a["location"][0]) * YARD
            if dt <= 6.0 and gain >= 5.0:
                kind = "delayed" if carried else "immediate"
                s["onetwo." + kind] += 1
                s["onetwo.return." + b["pass"].get("height", {}).get("name", "?") + "." + b["pass"].get("body_part", {}).get("name", "?")] += 1
                if b["pass"].get("technique", {}).get("name"):
                    s["onetwo.return.technique." + b["pass"]["technique"]["name"]] += 1
        elif dt <= 2.0 and not carried and (b["pass"]["end_location"][0] - a["location"][0]) * YARD >= 10.0:
            s["thirdman"] += 1
    summaries[mid] = dict(s)
    json.dump(summaries, open(path, "w"))

n = len(summaries)
total = Counter()
for v in summaries.values():
    total.update(v)
print(f"matches {n} (both teams, per match):")
for k, v in sorted(total.items()):
    print(f"  {k:55s} {v / n:7.2f}")
