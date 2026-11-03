#!/usr/bin/env python3
"""Calibration targets for the match engine from real event data: StatsBomb open data (https://github.com/hudl/open-data).

Data: StatsBomb. Its terms ask that any published work based on the data names StatsBomb as the source.

For a sample of matches from complete league seasons it reads every shot - where it was struck from, with which part of the
body and technique, first time or not, the pass that set it up (cross, through ball, cut-back, ground pass), the phase of play,
where the goalkeeper stood at the moment of the shot (from the freeze frame) and the outcome - and writes the shares and rates
the engine should reproduce to targets.json. Only aggregates are written; no event data is kept.

    python3 tools/calibration/statsbomb_targets.py --per-league 40 --out tools/calibration/targets.json

Coordinates: StatsBomb's pitch is 120 x 80 (yards), the attacking goal at x = 120 between y = 36 and 44; converted to metres
for a 105 x 68 pitch.
"""
import argparse, json, math, os, ssl, sys, time, urllib.request
from collections import Counter, defaultdict

BASE = "https://raw.githubusercontent.com/hudl/open-data/master/data/"
# complete men's league seasons: (competition, season, name)
LEAGUES = [(2, 27, "Premier League 2015/16"), (11, 27, "La Liga 2015/16"), (9, 27, "Bundesliga 2015/16"),
           (12, 27, "Serie A 2015/16"), (7, 27, "Ligue 1 2015/16")]


def _context():
    ca = os.environ.get("SSL_CERT_FILE") or os.environ.get("REQUESTS_CA_BUNDLE")
    if not ca and os.path.exists("/root/.ccr/ca-bundle.crt"): ca = "/root/.ccr/ca-bundle.crt"
    return ssl.create_default_context(cafile=ca) if ca else ssl.create_default_context()


CTX = _context()


def fetch(path, tries=4):
    for k in range(tries):
        try:
            with urllib.request.urlopen(BASE + path, timeout=120, context=CTX) as r:
                return json.loads(r.read())
        except Exception as e:  # (a network hiccup: wait and try again)
            if k == tries - 1: raise
            time.sleep(2 ** (k + 1))


def metres(loc):
    """StatsBomb (x, y) to (distance to the goal line, lateral offset from the centre) in metres."""
    x, y = loc[0], loc[1]
    return (120.0 - x) * 105.0 / 120.0, (y - 40.0) * 68.0 / 80.0


def zone(loc):
    x, y = loc[0], loc[1]
    if x >= 114 and 30 <= y <= 50: return "six"
    if x >= 102 and 18 <= y <= 62: return "box"
    return "out"


def keeper_bucket(shot_loc, frame):
    """How far the defending keeper stood from the shooter (metres): a<3, b3-6, c6-10, d10+."""
    for f in frame or []:
        if not f.get("teammate") and f.get("position", {}).get("name") == "Goalkeeper":
            dx = (f["location"][0] - shot_loc[0]) * 105.0 / 120.0
            dy = (f["location"][1] - shot_loc[1]) * 68.0 / 80.0
            d = math.hypot(dx, dy)
            return "a<3" if d < 3 else "b3-6" if d < 6 else "c6-10" if d < 10 else "d10+"
    return None


def assist_kind(p):
    """The pass that set the shot up (its key pass)."""
    if p is None: return "none"
    pa = p.get("pass", {})
    if pa.get("type", {}).get("name") in ("Corner", "Free Kick", "Throw-in", "Goal Kick", "Kick Off"): return "set-piece"
    if pa.get("cut_back"): return "cut-back"
    if pa.get("cross"): return "cross"
    if pa.get("through_ball") or pa.get("technique", {}).get("name") == "Through Ball": return "through-ball"
    if pa.get("body_part", {}).get("name") == "Head": return "header"
    if pa.get("height", {}).get("name") == "High Pass": return "long-high"
    return "ground"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--per-league", type=int, default=40, help="matches taken from each season (spread evenly over it)")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "targets.json"))
    a = ap.parse_args()

    shots = Counter(); goals = Counter(); n_matches = 0
    seen_leagues = []
    for comp, season, name in LEAGUES:
        ms = fetch("matches/%d/%d.json" % (comp, season))
        ms.sort(key=lambda m: (m["match_date"], m["match_id"]))
        step = max(1, len(ms) // a.per_league)
        pick = ms[::step][:a.per_league]
        seen_leagues.append({"league": name, "matches_in_season": len(ms), "used": len(pick)})
        for i, m in enumerate(pick, 1):
            ev = fetch("events/%d.json" % m["match_id"])
            n_matches += 1
            by_id = {e["id"]: e for e in ev}
            last_shot = {}
            for e in ev:
                if e.get("type", {}).get("name") != "Shot": continue
                s = e["shot"]
                team = e["team"]["name"]
                t = e["period"] * 10000 + e["minute"] * 60 + e["second"]
                kind = s.get("type", {}).get("name", "Open Play")
                part = s.get("body_part", {}).get("name", "")
                tech = s.get("technique", {}).get("name", "Normal")
                outcome = s.get("outcome", {}).get("name", "")
                goal = outcome == "Goal"
                on_target = outcome in ("Goal", "Saved", "Saved To Post")
                pattern = e.get("play_pattern", {}).get("name", "")
                rebound = team in last_shot and 0 <= t - last_shot[team] <= 5
                last_shot[team] = t
                key = by_id.get(s.get("key_pass_id")) if s.get("key_pass_id") else None
                tags = ["all", "zone." + zone(e["location"]), "type." + kind.lower().replace(" ", "-"),
                        "pattern." + pattern.lower().replace(" ", "-")]
                if on_target: tags.append("on-target")
                if part == "Head": tags.append("header")
                open_play = kind == "Open Play"
                if open_play:
                    tags.append("open")
                    tags.append("open.assist." + ("rebound" if rebound and key is None else assist_kind(key)))
                    tags.append("open.tech." + tech.lower().replace(" ", "-"))
                    if part != "Head":
                        tags.append("open.foot")
                        if s.get("first_time"): tags.append("open.foot.first-time")
                        kb = keeper_bucket(e["location"], s.get("freeze_frame"))
                        if kb: tags.append("open.foot.keeper." + kb)
                    dist, lat = metres(e["location"])
                    db = "0-6" if math.hypot(dist, lat) < 6 else "6-11" if math.hypot(dist, lat) < 11 else "11-16" if math.hypot(dist, lat) < 16 else "16-22" if math.hypot(dist, lat) < 22 else "22+"
                    tags.append("open.dist." + db)
                for tg in tags:
                    shots[tg] += 1
                    if goal: goals[tg] += 1
            print("  %s: %d/%d" % (name, i, len(pick)), file=sys.stderr, flush=True)

    def per_match(c, k): return round(c[k] / max(1, n_matches), 3)
    def share(c, k, of): return round(c[k] / max(1, c[of]), 4)
    keys = sorted(set(shots) | set(goals))
    out = {
        "source": "StatsBomb open data (https://github.com/hudl/open-data) - data: StatsBomb",
        "matches": n_matches,
        "leagues": seen_leagues,
        "per_match": {"goals": per_match(goals, "all"), "shots": per_match(shots, "all"), "shots_on_target": per_match(shots, "on-target")},
        "goal_share": {k: share(goals, k, "all") for k in keys if not k.startswith("open.")},
        "open_play_goal_share": {k: share(goals, k, "open") for k in keys if k.startswith("open.") and not k.startswith("open.foot")},
        "open_play_foot_goal_share": {k: share(goals, k, "open.foot") for k in keys if k.startswith("open.foot")},
        "open_play_shot_share": {k: share(shots, k, "open") for k in keys if k.startswith("open.") and not k.startswith("open.foot")},
        "open_play_foot_shot_share": {k: share(shots, k, "open.foot") for k in keys if k.startswith("open.foot")},
        "conversion": {k: round(goals[k] / shots[k], 4) for k in keys if shots[k] >= 20},
        "counts": {"shots": dict(shots), "goals": dict(goals)},
    }
    with open(a.out, "w") as f: json.dump(out, f, indent=1, sort_keys=True)
    print(json.dumps(out["per_match"]), file=sys.stderr)
    print("wrote", a.out, "-", n_matches, "matches", file=sys.stderr)


if __name__ == "__main__":
    main()
