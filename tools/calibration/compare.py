#!/usr/bin/env python3
"""The engine against real football: reads the output of `SimRunner --diag` and targets.json (statsbomb_targets.py) and prints
the same measures side by side - how many shots and goals, where the goals come from, what set them up, how they were struck,
and where the keeper was.

    dotnet run -c Release --project sim/SoccerVision.SimRunner -- --matches 100 --diag > sim.txt
    python3 tools/calibration/compare.py sim.txt [tools/calibration/targets.json]
"""
import json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))


def read_sim(path):
    diag, rows = {}, {}
    for line in open(path, encoding="utf8"):
        m = re.match(r"\s+([a-zA-Z][\w.<+\-]*)\s+(-?[\d.]+) \((\d+)\)\s*$", line)
        if m:
            diag[m.group(1)] = float(m.group(2))
            continue
        m = re.match(r"\s{2}(Goals|Shots|Shots on target|Saves)\s+(-?[\d.]+)", line)
        if m: rows[m.group(1)] = float(m.group(2))
    return diag, rows


def group(diag, prefix, buckets, merge=lambda k: k):
    """Sum the per-match values of the keys under prefix into buckets (merge maps a key's tail to its bucket)."""
    out = {b: 0.0 for b in buckets}
    for k, v in diag.items():
        if not k.startswith(prefix): continue
        b = merge(k[len(prefix):])
        if b in out: out[b] += v
    return out


def pct(x): return "%5.1f%%" % (100 * x)


def main():
    sim = sys.argv[1]
    targets = json.load(open(sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "targets.json")))
    d, rows = read_sim(sim)
    gs, ops, opf, ofs = targets["goal_share"], targets["open_play_goal_share"], targets["open_play_foot_goal_share"], targets["open_play_foot_shot_share"]
    lines = []

    def row(name, ours, real, fmt=pct): lines.append("| %s | %s | %s |" % (name, fmt(ours), fmt(real)))

    num = lambda x: "%6.2f" % x
    lines.append("| | engine | real (%s, %d matches) |" % ("StatsBomb", targets["matches"]))
    lines.append("|---|---|---|")
    row("goals a match", rows.get("Goals", 0), targets["per_match"]["goals"], num)
    row("shots a match", rows.get("Shots", 0), targets["per_match"]["shots"], num)
    row("shots on target a match", rows.get("Shots on target", 0), targets["per_match"]["shots_on_target"], num)

    goals = sum(group(d, "goal.how.", ["penalty", "header", "volley", "firstTime", "controlled"]).values()) or 1
    z = group(d, "goal.zone.", ["six", "box", "out"])
    for k, label in (("six", "six-yard box"), ("box", "rest of the box"), ("out", "outside the box")):
        row("goals from " + label, z[k] / goals, gs.get("zone." + k, 0))
    how = group(d, "goal.how.", ["penalty", "header", "volley", "firstTime", "controlled"])
    row("goals: headers", how["header"] / goals, gs.get("header", 0))
    row("goals: penalties", how["penalty"] / goals, gs.get("type.penalty", 0))
    foot = how["volley"] + how["firstTime"] + how["controlled"]
    row("foot goals in open play hit first time", (how["volley"] + how["firstTime"]) / max(1e-9, foot), opf.get("open.foot.first-time", 0))

    # what set the goal / the shot up (open play; a corner or a free kick into the box is a "set-piece" delivery, as StatsBomb has it - polish round 10)
    KINDS = ["Cross", "CrossSetPiece", "Restart", "ThroughBall", "Pass", "LongPass", "solo", "rebound", "Header"]

    def setup(prefix, share_key, label):
        a = group(d, prefix, KINDS)
        if prefix == "shot.assist.":
            a["solo"] -= group(d, "shot.how.", ["penalty"])["penalty"]  # (a penalty has no pass before it either)
        total = sum(a.values()) or 1
        t = targets[share_key]
        real = {"cross": t.get("open.assist.cross", 0), "setpiece": t.get("open.assist.set-piece", 0), "through": t.get("open.assist.through-ball", 0),
                "ground": t.get("open.assist.ground", 0) + t.get("open.assist.cut-back", 0), "long": t.get("open.assist.long-high", 0),
                "none": t.get("open.assist.none", 0), "rebound": t.get("open.assist.rebound", 0), "header": t.get("open.assist.header", 0)}
        row("open-play %s after a cross (not a set piece)" % label, a["Cross"] / total, real["cross"])
        row("... after a corner / free kick into the box", (a["CrossSetPiece"] + a["Restart"]) / total, real["setpiece"])
        row("... after a through ball", a["ThroughBall"] / total, real["through"])
        row("... after a ground pass / cut-back", a["Pass"] / total, real["ground"])
        row("... after a long high ball", a["LongPass"] / total, real["long"])
        row("... after a headed pass", a["Header"] / total, real["header"])
        row("... a rebound", a["rebound"] / total, real["rebound"])
        row("... no pass before it (solo, loose ball)", a["solo"] / total, real["none"])

    setup("goal.assist.", "open_play_goal_share", "goals")
    setup("shot.assist.", "open_play_shot_share", "shots")

    # where the keeper was (foot shots / goals)
    def kb(k):
        return k.split(".")[0]
    buckets = ["a<3", "b3-6", "c6-10", "d10+"]
    gk = group(d, "goal.keeper.", buckets, kb); sk = group(d, "shot.keeper.", buckets, kb)
    tg, ts = sum(gk.values()) or 1, sum(sk.values()) or 1
    rg = {b: opf.get("open.foot.keeper." + b, 0) for b in buckets}; rs = {b: ofs.get("open.foot.keeper." + b, 0) for b in buckets}
    rgt, rst = sum(rg.values()) or 1, sum(rs.values()) or 1
    for b, label in zip(buckets, ["under 3 m", "3-6 m", "6-10 m", "10 m+"]):
        row("foot goals with the keeper " + label + " away", gk[b] / tg, rg[b] / rgt)
    for b, label in zip(buckets, ["under 3 m", "3-6 m", "6-10 m", "10 m+"]):
        row("foot shots with the keeper " + label + " away", sk[b] / ts, rs[b] / rst)
    chips = d.get("shot.tech.Chip", 0)
    row("shots that are chips", chips / max(1e-9, rows.get("Shots", 1)), targets["open_play_shot_share"].get("open.tech.lob", 0))
    print("\n".join(lines))
    print("\nReal data: StatsBomb open data (https://github.com/hudl/open-data).")


if __name__ == "__main__":
    main()
