"""
Carries in real matches, counted from StatsBomb open data (https://github.com/statsbomb/open-data).

For every match of a competition and season: carries a match (both teams), their length (m), how far upfield they went, and whether the
carrier was under pressure (StatsBomb's under_pressure flag) - the basis for the dribbling touch styles (a short carry under pressure:
close control; a long one forward into space: progressive or knock-and-run). Only per-match summaries are cached
(summary_carries_<c>_<s>.json).

Usage: python3 -I carries.py [competition_id season_id] [cache_dir] [max_matches]
       default: 2 27 (Premier League 2015/16), current directory, all matches.
"""
import http.client, json, os, sys, time, urllib.error, urllib.request

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (int(sys.argv[1]), int(sys.argv[2])) if len(sys.argv) > 2 else (2, 27)
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10**9
path = os.path.join(cache, f"summary_carries_{comp}_{season}.json")
YARD = 0.9144
BINS = [0, 2, 5, 10, 20, 1000]


def get(url):
    for attempt in range(5):
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                return json.loads(r.read())
        except (urllib.error.URLError, TimeoutError, http.client.IncompleteRead, ConnectionError):
            time.sleep(2 ** attempt)
    with urllib.request.urlopen(url, timeout=120) as r:
        return json.loads(r.read())


def bin_of(x):
    for i in range(len(BINS) - 1):
        if BINS[i] <= x < BINS[i + 1]:
            return f"{BINS[i]}-{BINS[i + 1]}"
    return "?"


summaries = json.load(open(path)) if os.path.exists(path) else {}
matches = get(f"{BASE}/matches/{comp}/{season}.json")[:limit]
for m in matches:
    mid = str(m["match_id"])
    if mid in summaries:
        continue
    events = get(f"{BASE}/events/{mid}.json")
    s = {"carries": 0, "length": {}, "pressured": {}, "forward10": 0, "seconds": 0.0}
    for e in events:
        if e["type"]["name"] != "Carry":
            continue
        (x0, y0), (x1, y1) = e["location"], e["carry"]["end_location"]
        length = ((x1 - x0) ** 2 + (y1 - y0) ** 2) ** 0.5 * YARD
        b = bin_of(length)
        s["carries"] += 1
        s["length"][b] = s["length"].get(b, 0) + 1
        if e.get("under_pressure"):
            s["pressured"][b] = s["pressured"].get(b, 0) + 1
        if (x1 - x0) * YARD >= 10:
            s["forward10"] += 1
        s["seconds"] += e.get("duration", 0.0) or 0.0
    summaries[mid] = s
    json.dump(summaries, open(path, "w"))

n = len(summaries)
carries = sum(v["carries"] for v in summaries.values())
print(f"matches {n}: carries a match {carries / n:.1f} (both teams), mean duration {sum(v['seconds'] for v in summaries.values()) / max(1, carries):.2f} s")
print(f"  10 m or more upfield: {sum(v['forward10'] for v in summaries.values()) / n:.2f} a match")
for i in range(len(BINS) - 1):
    b = f"{BINS[i]}-{BINS[i + 1]}"
    c = sum(v["length"].get(b, 0) for v in summaries.values())
    p = sum(v["pressured"].get(b, 0) for v in summaries.values())
    print(f"  length {b:>8} m: {c / n:6.2f} a match ({100 * c / max(1, carries):4.1f}%), under pressure {100 * p / max(1, c):4.1f}%")
