"""
Corners in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

For every match of a competition and season: corner kicks a match, and what a corner is worth to the team that takes it - the
StatsBomb expected goals (shot.statsbomb_xg), the shots (and how many are headers) and the goals of that team in the same possession as the
corner kick. Only
per-match summaries are cached (summary_corners_<c>_<s>.json).

Usage: python3 -I corners.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_corners2_{comp}_{season}.json")


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
    # the possessions that start with (or contain) a corner kick, and the team that took it
    corner_possessions = {}
    kicks = 0
    for e in events:
        p = e.get("pass")
        if e["type"]["name"] == "Pass" and p and p.get("type", {}).get("name") == "Corner":
            kicks += 1
            corner_possessions.setdefault(e["possession"], e["team"]["name"])
    xg = 0.0
    goals = 0
    shots = headers = head_goals = 0
    for e in events:
        team = corner_possessions.get(e.get("possession"))
        if team is None or e["type"]["name"] != "Shot" or e["team"]["name"] != team:
            continue
        s = e.get("shot", {})
        if s.get("type", {}).get("name") == "Penalty":
            continue
        xg += s.get("statsbomb_xg", 0.0)
        shots += 1
        head = s.get("body_part", {}).get("name") == "Head"
        headers += head
        if s.get("outcome", {}).get("name") == "Goal":
            goals += 1
            head_goals += head
    summaries[mid] = {"corners": kicks, "corner_possessions": len(corner_possessions), "xg": xg, "goals": goals, "shots": shots, "headers": headers, "head_goals": head_goals}
    json.dump(summaries, open(path, "w"))

n = len(summaries)
corners = sum(s["corners"] for s in summaries.values())
possessions = sum(s.get("corner_possessions", s["corners"]) for s in summaries.values())
xg = sum(s["xg"] for s in summaries.values())
goals = sum(s["goals"] for s in summaries.values())
shots = sum(s["shots"] for s in summaries.values())
headers = sum(s["headers"] for s in summaries.values())
head_goals = sum(s["head_goals"] for s in summaries.values())
print(f"matches {n}: corners a match {corners / n:.2f}")
print(f"a corner is worth {xg / max(1, corners):.4f} xG and {goals / max(1, corners):.4f} goals ({goals} goals, non-penalty, in the {possessions} possessions"
      f" with a corner; {corners} corner kicks)")
print(f"  shots a corner {shots / max(1, corners):.3f} (a match {shots / n:.2f}), {100 * headers / max(1, shots):.1f}% headers; goals from headers {100 * head_goals / max(1, goals):.1f}% of the goals; {goals / n:.3f} goals a match from corners")
