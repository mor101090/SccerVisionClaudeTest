"""
Throw-ins in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

For every match of a competition and season: throw-ins a match (both teams), per team, the share won by the team that had the
ball before it went out, and what the last event before the throw-in was (a pass that went out, a clearance, a block, a duel,
a carry, ...). Only per-match summaries are cached (summary_throwins_<c>_<s>.json).

Usage: python3 -I throw_ins.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request
from collections import Counter

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_throwins_{comp}_{season}.json")


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
    teams = Counter()
    before = Counter()
    retained = 0
    for i, e in enumerate(events):
        p = e.get("pass")
        if e["type"]["name"] != "Pass" or not p or p.get("type", {}).get("name") != "Throw-in":
            continue
        team = e["team"]["name"]
        teams[team] += 1
        # the last event before the throw-in that had a team and was not a throw-in itself: who touched it last, and how
        j = i - 1
        while j >= 0 and events[j]["type"]["name"] in ("Ball Receipt*", "Pressure", "Substitution", "Tactical Shift", "Injury Stoppage", "Referee Ball-Drop", "Half Start", "Half End"):
            j -= 1
        if j >= 0:
            prev = events[j]
            kind = prev["type"]["name"]
            if kind == "Pass":
                outcome = prev.get("pass", {}).get("outcome", {}).get("name", "complete")
                kind = f"Pass ({outcome})"
            before[kind] += 1
            if prev.get("possession_team", {}).get("name") == team and prev["team"]["name"] == team:
                retained += 1
    summaries[mid] = {"throw_ins": sum(teams.values()), "per_team": list(teams.values()), "retained_by_last_toucher": retained, "before": dict(before)}
    json.dump(summaries, open(path, "w"))

n = len(summaries)
total = sum(s["throw_ins"] for s in summaries.values())
per_team = [t for s in summaries.values() for t in s["per_team"]]
before = Counter()
for s in summaries.values():
    before.update(s["before"])
print(f"matches {n}: throw-ins a match {total / n:.2f} (per team {sum(per_team) / max(1, len(per_team)):.2f}, min {min(per_team)}, max {max(per_team)})")
print("the event before the throw-in, a match:")
for k, v in before.most_common(12):
    print(f"  {k:40s} {v / n:6.2f}")
