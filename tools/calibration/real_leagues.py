#!/usr/bin/env python3
"""Real league results, to check the engine against: home advantage, and how the top, the middle and the bottom of a table do against each other.

Data: https://www.football-data.co.uk/data.php (match results, ten seasons 2015/16 - 2024/25). Nothing is estimated here: every number is counted from the matches.

    python3 tools/calibration/real_leagues.py [--dir /tmp/fd] [--leagues E0 F1 N1 ...]

Tiers, by the final table of each season: strong = the top two, medium = 7th to 14th place (the 8 teams in the middle of an 18-20 team league), weak = the bottom three.
"The stronger side's result" pools home and away matches. The leagues' split seasons (Scotland, Belgium) are left out of the tier table: their tables are not one 38-match league.
"""
import argparse, collections, csv, glob, os, ssl, statistics as st, urllib.request

NAMES = {"E0": "Premier League", "F1": "Ligue 1", "N1": "Eredivisie", "D1": "Bundesliga", "SP1": "La Liga", "I1": "Serie A", "P1": "Primeira Liga", "SC0": "Scottish Premiership"}
SEASONS = ["1516", "1617", "1718", "1819", "1920", "2021", "2122", "2223", "2324", "2425"]
TIER_LEAGUES = ["E0", "F1", "N1", "D1", "SP1", "I1", "P1"]


def fetch(directory, leagues):
    os.makedirs(directory, exist_ok=True)
    ca = "/root/.ccr/ca-bundle.crt"
    ctx = ssl.create_default_context(cafile=ca) if os.path.exists(ca) else ssl.create_default_context()
    for lg in leagues:
        for s in SEASONS:
            path = os.path.join(directory, "%s_%s.csv" % (lg, s))
            if not os.path.exists(path) or os.path.getsize(path) < 1000:
                with urllib.request.urlopen("https://football-data.co.uk/mmz4281/%s/%s.csv" % (s, lg), timeout=60, context=ctx) as r:
                    open(path, "wb").write(r.read())


def load(directory, lg):
    rows = []
    for f in sorted(glob.glob(os.path.join(directory, lg + "_*.csv"))):
        season = os.path.basename(f).split("_")[1][:4]
        for r in csv.DictReader(open(f, encoding="latin-1")):
            if r.get("FTR") in ("H", "D", "A") and r.get("HomeTeam"):
                r["season"] = lg + season
                rows.append(r)
    return rows


def tiers(rows):
    out = {}
    for s in sorted(set(r["season"] for r in rows)):
        pts, gd = collections.Counter(), collections.Counter()
        for r in rows:
            if r["season"] != s: continue
            h, a, hg, ag = r["HomeTeam"], r["AwayTeam"], int(r["FTHG"]), int(r["FTAG"])
            pts[h] += 0; pts[a] += 0
            if r["FTR"] == "H": pts[h] += 3
            elif r["FTR"] == "A": pts[a] += 3
            else: pts[h] += 1; pts[a] += 1
            gd[h] += hg - ag; gd[a] += ag - hg
        order = sorted(pts, key=lambda t: (-pts[t], -gd[t])); n = len(order)
        out[s] = {t: ("S" if i < 2 else "W" if i >= n - 3 else "M" if 6 <= i <= 13 else "-") for i, t in enumerate(order)}
    return out


def pair(rows, T, A, B):
    w = d = l = n = 0; gf = ga = 0
    for r in rows:
        t = T[r["season"]]
        for ta, tb, side in ((t[r["HomeTeam"]], t[r["AwayTeam"]], "H"), (t[r["AwayTeam"]], t[r["HomeTeam"]], "A")):
            if ta == A and tb == B:
                n += 1; hg, ag = int(r["FTHG"]), int(r["FTAG"]); f, g = (hg, ag) if side == "H" else (ag, hg); gf += f; ga += g
                if r["FTR"] == side: w += 1
                elif r["FTR"] == "D": d += 1
                else: l += 1
    return (100 * w / n, 100 * d / n, 100 * l / n, gf / n, ga / n, n) if n else None


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--dir", default="/tmp/football-data"); ap.add_argument("--leagues", nargs="*", default=list(NAMES))
    a = ap.parse_args(); fetch(a.dir, a.leagues)
    print("HOME ADVANTAGE (all matches, 10 seasons): home win / draw / away win %, goals home - away")
    for lg in a.leagues:
        rows = load(a.dir, lg); n = len(rows); h = sum(r["FTR"] == "H" for r in rows); d = sum(r["FTR"] == "D" for r in rows)
        print("  %-22s n=%-5d %2.0f / %2.0f / %2.0f   %.2f - %.2f" % (NAMES[lg], n, 100 * h / n, 100 * d / n, 100 * (n - h - d) / n, st.mean(int(r["FTHG"]) for r in rows), st.mean(int(r["FTAG"]) for r in rows)))
    print("\nPER MATCH (both teams): goals, shots, on target, fouls, corners, yellow cards, red cards")
    for lg in a.leagues:
        rows = load(a.dir, lg)
        def m(*cols):
            vals = [sum(int(r[c]) for c in cols) for r in rows if all(r.get(c, "").strip().isdigit() for c in cols)]
            return (st.mean(vals), len(vals)) if vals else (float("nan"), 0)
        g = m("FTHG", "FTAG"); sh = m("HS", "AS"); so = m("HST", "AST"); fo = m("HF", "AF"); co = m("HC", "AC"); y = m("HY", "AY"); rc = m("HR", "AR")
        print("  %-22s goals %.2f  shots %.1f  on target %.1f  fouls %.1f  corners %.1f  yellows %.2f  reds %.3f   (n %d)" % (NAMES[lg], g[0], sh[0], so[0], fo[0], co[0], y[0], rc[0], sh[1]))
    print("\nBY SEASON, Premier League (2020/21: no spectators)")
    rows = load(a.dir, "E0")
    for s in sorted(set(r["season"] for r in rows)):
        rs = [r for r in rows if r["season"] == s]; n = len(rs); h = sum(r["FTR"] == "H" for r in rs); d = sum(r["FTR"] == "D" for r in rs)
        print("  %s n=%d  %2.0f / %2.0f / %2.0f" % (s[2:], n, 100 * h / n, 100 * d / n, 100 * (n - h - d) / n))
    print("\nTIERS: the stronger side's win / draw / loss %, goals for - against (both seats pooled)")
    allrows, allT = [], {}
    for lg in a.leagues:
        if lg not in TIER_LEAGUES: continue
        rows = load(a.dir, lg); T = tiers(rows); allrows += rows; allT.update(T)
        print("  %-22s " % NAMES[lg] + "  |  ".join("%s v %s: %2.0f/%2.0f/%2.0f %.2f-%.2f (n%d)" % ((A, B) + pair(rows, T, A, B)) for A, B in (("S", "W"), ("S", "M"), ("M", "W"))))
    print("  %-22s " % "all of them" + "  |  ".join("%s v %s: %2.0f/%2.0f/%2.0f %.2f-%.2f (n%d)" % ((A, B) + pair(allrows, allT, A, B)) for A, B in (("S", "W"), ("S", "M"), ("M", "W"))))


if __name__ == "__main__":
    main()
