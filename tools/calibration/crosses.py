"""
Open-play crosses in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

For every match of a competition and season: open-play crosses a match (pass.cross, not from a corner, a free kick or a
throw-in), the share completed, and the share stopped next to the crosser (marked deflected, or the other team's first action
after it within 10 yards of where it was struck: a block, a clearance or an interception at once). Only per-match summaries are cached (summary_crosses_<c>_<s>.json).

Usage: python3 -I crosses.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_crosses_{comp}_{season}.json")
SET_PIECES = ("Corner", "Free Kick", "Throw-in", "Kick Off", "Goal Kick")


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
    crosses = {}
    deflected = set()
    for e in events:
        p = e.get("pass")
        if e["type"]["name"] != "Pass" or not p or not p.get("cross"):
            continue
        if p.get("type", {}).get("name") in SET_PIECES:
            continue
        crosses[e["id"]] = (e["team"]["name"], "outcome" not in p)
        if p.get("deflected"):
            deflected.add(e["id"])
    # stopped next to the crosser: the other team's first action after it (pressures and receipts aside) is within 10 yards of
    # where it was struck - a block, a clearance or an interception at once (StatsBomb pitch: 120 x 80 yards)
    blocked = set(deflected)
    for i, e in enumerate(events):
        c = crosses.get(e["id"])
        if c is None or c[1]:
            continue
        j = i + 1
        while j < len(events) and events[j]["type"]["name"] in ("Pressure", "Ball Receipt*"):
            j += 1
        if j >= len(events):
            continue
        nx = events[j]
        if nx["team"]["name"] == c[0] or "location" not in nx or "location" not in e:
            continue
        x, y = nx["location"]
        x, y = 120 - x, 80 - y  # (the other team's coordinates, turned round)
        if ((x - e["location"][0]) ** 2 + (y - e["location"][1]) ** 2) ** 0.5 < 10:
            blocked.add(e["id"])
    summaries[mid] = {"crosses": len(crosses), "completed": sum(1 for c in crosses.values() if c[1]), "blocked": len(blocked), "deflected": len(deflected)}
    json.dump(summaries, open(path, "w"))

n = len(summaries)
crosses = sum(s["crosses"] for s in summaries.values())
completed = sum(s["completed"] for s in summaries.values())
blocked = sum(s["blocked"] for s in summaries.values())
deflected = sum(s.get("deflected", 0) for s in summaries.values())
print(f"matches {n}: open-play crosses a match {crosses / n:.2f} (both teams)")
print(f"completed {100 * completed / max(1, crosses):.1f}%, stopped next to the crosser {100 * blocked / max(1, crosses):.1f}% (marked deflected {100 * deflected / max(1, crosses):.1f}%)")
