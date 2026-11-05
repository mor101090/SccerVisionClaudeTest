#!/usr/bin/env python3
"""How teams pass in real matches, counted from StatsBomb open data (event data).

Per team and match: passes (open play only - throw-ins, goal kicks, corners, free kicks and
kick-offs left out, as the engine's passes are counted from open play), completed share, mean
pass length (m), share of short (< 15 m) / long (>= 32 m) passes, passes per possession
sequence (a possession ends when the other team has the ball), seconds from one pass to the
next within a sequence, and seconds per sequence. Also the share of passes by the top
possession side of each match v the other.

Data: StatsBomb open data, https://github.com/statsbomb/open-data
Usage: python3 -I passing_tempo.py [competition_id season_id] [cache_dir] [max_matches] [team]
       default 2 27 (Premier League 2015/16). StatsBomb's pitch is 120 x 80 yards; lengths are
       converted to metres. Only per-match summaries are cached (summary_tempo_<c>_<s>.json).
"""
import json, os, sys, time, urllib.error, urllib.request
from collections import defaultdict

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (sys.argv[1], sys.argv[2]) if len(sys.argv) > 2 else ("2", "27")
cache = sys.argv[3] if len(sys.argv) > 3 else "."
limit = int(sys.argv[4]) if len(sys.argv) > 4 else 10 ** 9
only = sys.argv[5] if len(sys.argv) > 5 else None  # a team's name: print that team and its opponents separately
path = os.path.join(cache, f"summary_tempo_{comp}_{season}.json")
YARD = 0.9144


def get(url):
    for wait in (5, 15, 45, 120, 300):  # (raw.githubusercontent.com answers 429 to a long run of downloads)
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                return json.load(r)
        except urllib.error.HTTPError as e:
            if e.code != 429:
                raise
            time.sleep(wait)
    with urllib.request.urlopen(url, timeout=120) as r:
        return json.load(r)


def secs(e):
    h, m, s = e["timestamp"].split(":")
    return 3600 * int(h) + 60 * int(m) + float(s)


matches = get(f"{BASE}/matches/{comp}/{season}.json")[:limit]
summary = json.load(open(path)) if os.path.exists(path) else {}
for i, m in enumerate(matches):
    mid = str(m["match_id"])
    if mid in summary:
        continue
    evs = get(f"{BASE}/events/{mid}.json")
    t = defaultdict(lambda: defaultdict(float))
    seq = {}  # possession number -> [team, passes, first pass time, last pass time]
    for e in evs:
        if e["type"]["name"] != "Pass":
            continue
        p = e["pass"]
        team = e["team"]["name"]
        kind = p.get("type", {}).get("name")  # None = open play; Kick Off, Throw-in, Goal Kick, Corner, Free Kick, Recovery, Interception
        if kind in ("Throw-in", "Goal Kick", "Corner", "Free Kick", "Kick Off"):
            continue
        d = t[team]
        d["passes"] += 1
        d["completed"] += 1 if "outcome" not in p else 0
        d["length"] += p["length"] * YARD
        d["short"] += 1 if p["length"] * YARD < 15 else 0
        d["long"] += 1 if p["length"] * YARD >= 32 else 0
        pn = e["possession"]
        s = seq.setdefault((pn, team), [0, secs(e), secs(e), e["period"]])
        s[0] += 1; s[2] = secs(e)
    for (pn, team), (n, t0, t1, per) in seq.items():
        d = t[team]
        d["seqs"] += 1
        d["seq_passes"] += n
        if n >= 2:
            d["gap_sum"] += t1 - t0; d["gap_n"] += n - 1
    summary[mid] = {k: dict(v) for k, v in t.items()}
    if i % 20 == 0:
        json.dump(summary, open(path, "w"))
        print(f"{len(summary)}/{len(matches)}", flush=True)
json.dump(summary, open(path, "w"))

def report(label, pick):
    tot = defaultdict(float); n = 0
    shares = []
    for mid, teams in summary.items():
        if len(teams) != 2:
            continue
        for name, d in teams.items():
            if not pick(name):
                continue
            n += 1
            for k, v in d.items():
                tot[k] += v
            other = [v for k, v in teams.items() if k != name][0]
            shares.append(d["passes"] / (d["passes"] + other["passes"]))
    p = tot["passes"]
    print(f"\n{label}: {n} team-matches (open-play passes only: throw-ins, goal kicks, corners, free kicks, kick-offs left out)")
    print(f"passes per team per match      {p / n:6.1f}")
    print(f"completed                      {100 * tot['completed'] / p:6.1f}%")
    print(f"mean pass length               {tot['length'] / p:6.1f} m")
    print(f"short (<15 m) / long (>=32 m)  {100 * tot['short'] / p:5.1f}% / {100 * tot['long'] / p:5.1f}%")
    print(f"possession sequences with a pass, per team per match  {tot['seqs'] / n:6.1f}")
    print(f"passes per sequence            {tot['seq_passes'] / tot['seqs']:6.2f}")
    print(f"seconds between passes in a sequence  {tot['gap_sum'] / max(1, tot['gap_n']):5.2f}")
    shares.sort()
    print(f"its share of the match's passes {100 * sum(shares) / len(shares):.1f}% (10% {100 * shares[len(shares) // 10]:.0f}, median {100 * shares[len(shares) // 2]:.0f}, 90% {100 * shares[len(shares) * 9 // 10]:.0f})")


if only:
    report(only, lambda name: name == only)
    report(f"{only}'s opponents", lambda name: name != only)
    sys.exit(0)

# per team-match averages
tot = defaultdict(float); n = 0
shares = []
for mid, teams in summary.items():
    ts = list(teams.values())
    if len(ts) != 2:
        continue
    for d in ts:
        n += 1
        for k, v in d.items():
            tot[k] += v
    a, b = sorted(ts, key=lambda d: -d["passes"])
    shares.append(a["passes"] / (a["passes"] + b["passes"]))
p = tot["passes"]
print(f"\n{len(summary)} matches, {n} team-matches (open-play passes only: throw-ins, goal kicks, corners, free kicks, kick-offs left out)")
print(f"passes per team per match      {p / n:6.1f}")
print(f"completed                      {100 * tot['completed'] / p:6.1f}%")
print(f"mean pass length               {tot['length'] / p:6.1f} m")
print(f"short (<15 m) / long (>=32 m)  {100 * tot['short'] / p:5.1f}% / {100 * tot['long'] / p:5.1f}%")
print(f"possession sequences with a pass, per team per match  {tot['seqs'] / n:6.1f}")
print(f"passes per sequence            {tot['seq_passes'] / tot['seqs']:6.2f}")
print(f"seconds between passes in a sequence  {tot['gap_sum'] / max(1, tot['gap_n']):5.2f}")
shares.sort()
print(f"the side with more passes has {100 * sum(shares) / len(shares):.1f}% of the match's passes (10% {100 * shares[len(shares) // 10]:.0f}, median {100 * shares[len(shares) // 2]:.0f}, 90% {100 * shares[len(shares) * 9 // 10]:.0f}, max {100 * shares[-1]:.0f})")
