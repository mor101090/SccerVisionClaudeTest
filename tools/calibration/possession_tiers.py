#!/usr/bin/env python3
"""Possession by table tier, counted from StatsBomb open data (full seasons).

Two measures of possession are printed: each side's share of the match's passes
(the way Opta's published possession figure is worked out) and each side's share
of the time between events while its team had the ball (gaps over 10 s - dead
ball - left out), which is closer to the engine's own seconds-in-possession figure. Tiers come from the final table built
from the same matches' scores: S = top 2, M = 7th-14th, W = bottom 3 (as in
real_leagues.py).

Data: StatsBomb open data, https://github.com/statsbomb/open-data
Usage: python3 -I possession_tiers.py [competition_id season_id] [cache_dir]
       default 2 27 = Premier League 2015/16. Events are fetched one match at a
       time; only the per-match pass counts and seconds (in all and by score) are kept (cache_dir/summary_<c>_<s>.json).
"""
import json, os, sys, time, urllib.error, urllib.request
from collections import defaultdict

BASE = "https://raw.githubusercontent.com/statsbomb/open-data/master/data"
comp, season = (sys.argv[1], sys.argv[2]) if len(sys.argv) > 2 else ("2", "27")
cache = sys.argv[3] if len(sys.argv) > 3 else "."
summary_path = os.path.join(cache, f"summary_{comp}_{season}.json")


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


matches = get(f"{BASE}/matches/{comp}/{season}.json")
summary = json.load(open(summary_path)) if os.path.exists(summary_path) else {}
for i, m in enumerate(matches):
    mid = str(m["match_id"])
    if mid in summary:
        continue
    home, away = m["home_team"]["home_team_name"], m["away_team"]["away_team_name"]
    passes, secs = defaultdict(int), defaultdict(float)
    by_state = {"h": defaultdict(float), "a": defaultdict(float)}  # seconds on the ball by the home side's goal difference at the time (-2..2)
    goals = {home: 0, away: 0}
    evs = get(f"{BASE}/events/{mid}.json")
    ts = lambda e: (lambda h, m, x: 3600 * int(h) + 60 * int(m) + float(x))(*e["timestamp"].split(":"))
    for e, nxt in zip(evs, evs[1:] + [None]):
        kind = e["type"]["name"]
        if kind == "Pass":
            passes[e["team"]["name"]] += 1
        if nxt is not None and nxt["period"] == e["period"] and e["period"] <= 4 and "possession_team" in e:
            dt = ts(nxt) - ts(e)
            if 0 < dt <= 10:
                who = e["possession_team"]["name"]
                secs[who] += dt
                d = max(-2, min(2, goals[home] - goals[away]))
                by_state["h" if who == home else "a"][str(d)] += dt
        if (kind == "Shot" and e["shot"]["outcome"]["name"] == "Goal") or kind == "Own Goal For":
            goals[e["team"]["name"]] += 1
    summary[mid] = {"home": home, "away": away, "hg": m["home_score"], "ag": m["away_score"],
                    "hp": passes[home], "ap": passes[away],
                    "hs": round(secs[home], 1), "as": round(secs[away], 1),
                    "state": {k: {d: round(v, 1) for d, v in by_state[k].items()} for k in by_state}}
    if i % 20 == 0:
        json.dump(summary, open(summary_path, "w"))
        print(f"{len(summary)}/{len(matches)}", flush=True)
json.dump(summary, open(summary_path, "w"))

rows = list(summary.values())
pts, gd, gf = defaultdict(int), defaultdict(int), defaultdict(int)
for r in rows:
    h, a, hg, ag = r["home"], r["away"], r["hg"], r["ag"]
    pts[h] += 3 if hg > ag else 1 if hg == ag else 0
    pts[a] += 3 if ag > hg else 1 if hg == ag else 0
    gd[h] += hg - ag; gd[a] += ag - hg; gf[h] += hg; gf[a] += ag
table = sorted(pts, key=lambda t: (-pts[t], -gd[t], -gf[t]))
n = len(table)
tier = {t: ("S" if i < 2 else "M" if 6 <= i < n - 6 else "W" if i >= n - 3 else "-") for i, t in enumerate(table)}

share, tshare = defaultdict(list), defaultdict(list)  # team -> its pass share / time share per match
pair, tpair = defaultdict(list), defaultdict(list)    # (tier, tier) -> the first tier's share
for r in rows:
    tot, ttot = r["hp"] + r["ap"], r["hs"] + r["as"]
    if tot == 0 or ttot == 0:
        continue
    hs, hts = r["hp"] / tot, r["hs"] / ttot
    share[r["home"]].append(hs); share[r["away"]].append(1 - hs)
    tshare[r["home"]].append(hts); tshare[r["away"]].append(1 - hts)
    th, ta = tier[r["home"]], tier[r["away"]]
    pair[(th, ta)].append(hs); pair[(ta, th)].append(1 - hs)
    tpair[(th, ta)].append(hts); tpair[(ta, th)].append(1 - hts)

pct = lambda xs: 100 * sum(xs) / len(xs)
print(f"\n{len(rows)} matches. Season possession, final table order (pass share / time share):")
for i, t in enumerate(table, 1):
    print(f"{i:2} {tier[t]} {t:28} {pts[t]:3} pts  {pct(share[t]):5.1f}%  {pct(tshare[t]):5.1f}%")
for name, pr in (("pass share", pair), ("time share", tpair)):
    print(f"\nTier v tier, {name} of the first (matches seen from both sides):")
    for a, b in [("S", "W"), ("S", "M"), ("M", "W"), ("M", "M"), ("S", "S"), ("W", "W")]:
        xs = sorted(pr[(a, b)])
        if xs:
            q = lambda f: 100 * xs[min(len(xs) - 1, int(f * len(xs)))]
            print(f"{a} v {b}: {pct(xs):5.1f}%  (n {len(xs)}, 10% {q(.1):.0f}, median {q(.5):.0f}, 90% {q(.9):.0f}, min {100*xs[0]:.0f}, max {100*xs[-1]:.0f})")

# Score effects: a side's share of the ball while it leads / is level / trails (seconds pooled over all matches; each
# match counted from both sides, so "level" is 50% by construction). The same sides take part in every state, so the
# difference is the game state, not the squads - though sides that lead more often are, on the whole, better ones.
print("\nShare of the ball by score state (both sides of every match, pooled seconds):")
tot = defaultdict(lambda: [0.0, 0.0])  # state from the side's view -> [its seconds, the other side's]
for r in rows:
    st = r.get("state")
    if not st:
        continue
    for d in set(st["h"]) | set(st["a"]):
        h, a = st["h"].get(d, 0.0), st["a"].get(d, 0.0)
        tot[int(d)][0] += h; tot[int(d)][1] += a     # the home side, at goal difference d
        tot[-int(d)][0] += a; tot[-int(d)][1] += h   # the away side, at -d
for d in sorted(tot):
    mine, theirs = tot[d]
    label = {-2: "trailing by 2+", -1: "trailing by 1", 0: "level", 1: "leading by 1", 2: "leading by 2+"}[d]
    print(f"{label:15} {100 * mine / (mine + theirs):5.1f}%  ({(mine + theirs) / 60 / len(rows) / 2:4.1f} min of play a match)")
