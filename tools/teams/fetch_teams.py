#!/usr/bin/env python3
"""Fetches squads from the Soccer Vision team generator API (https://soccer-api.dev-spirit.com/docs, v2) and saves them in the
format tools/teams/build_teams.py reads (a JSON list of players, as the old generator wrote them).

    python3 tools/teams/fetch_teams.py --out data/teams/api red-rovers:101 blue-falcons:202 [--level normal] [--formation 4-4-2]

Each NAME:SEED is one squad; the same seed and options always give the same squad (the API is reproducible). Skill groups the
API returns as null (a keeper's "defense", an outfield player's "goalkeeping") are left out, as the old generator did.
It does not touch the squads the game uses now - build them with build_teams.py --dir <out> when you want them.
"""
import argparse, json, os, ssl, urllib.parse, urllib.request

API = "https://soccer-api.dev-spirit.com/api/v2/team"


def ctx():
    ca = "/root/.ccr/ca-bundle.crt"
    return ssl.create_default_context(cafile=ca) if os.path.exists(ca) else ssl.create_default_context()


def fetch(seed, level, formation):
    url = API + "?" + urllib.parse.urlencode({"seed": seed, "level": level, "formation": formation})
    with urllib.request.urlopen(url, timeout=60, context=ctx()) as r:
        data = json.loads(r.read())
    players = data["players"]
    for p in players:
        p["stats"] = {k: v for k, v in p["stats"].items() if v is not None}
    return data["seed"], players


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("teams", nargs="+", help="NAME:SEED")
    ap.add_argument("--out", required=True)
    ap.add_argument("--level", default="normal")
    ap.add_argument("--formation", default="4-4-2")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    for t in a.teams:
        name, _, seed = t.partition(":")
        s, players = fetch(int(seed), a.level, a.formation)
        with open(os.path.join(a.out, name + ".json"), "w", encoding="utf-8") as f: json.dump(players, f, indent=1, ensure_ascii=False)
        print("%s: %d players (seed %s)" % (name, len(players), s))


if __name__ == "__main__":
    main()
