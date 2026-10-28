#!/usr/bin/env python3
"""Turns the teams made by the team generator (https://soccer-simulation.onrender.com/api/team, 22 players each, saved
under data/teams/) into the engine's squads: unity/.../Simulation/Demo/GeneratedTeams.cs.

Each squad: 11 starters in a 4-4-2 (the generator's positions decide who plays where; left-footed full-backs and
wingers go on the left, right-footed on the right, a second winger of the same foot plays "inverted") and a bench of 7
(a keeper, two defenders, two midfielders, two forwards). Every attribute, the height, the feet (the strong foot is
rated 3, the other by the weak-foot level), the stamina and the fitness come from the generator unchanged.

    python3 tools/teams/build_teams.py
"""
import json, os

root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TEAMS = [("Red", "red-rovers"), ("Blue", "blue-falcons")]

ATTRS = [  # (engine attribute, generator group, generator key)
    ("Aggression", "mental", "aggression"), ("Teamwork", "mental", "teamwork"), ("Composure", "mental", "composure"), ("Decisions", "mental", "decisions"),
    ("Tackling", "defense", "tackling"), ("Marking", "defense", "marking"), ("Positioning", "defense", "positioning"),
    ("Creativity", "playmaking", "creative"), ("Passing", "playmaking", "passing"), ("Crossing", "playmaking", "crossing"),
    ("Finishing", "attack", "finishing"), ("LongShots", "attack", "long_shots"), ("OffTheBall", "attack", "off_the_ball"),
    ("PhysicalPower", "athletic", "physical_power"), ("BallControl", "athletic", "ball_control"), ("Dribbling", "athletic", "dribbling"),
    ("Acceleration", "athletic", "acceleration"), ("Speed", "athletic", "speed"), ("Jumping", "athletic", "jumping"), ("Heading", "athletic", "heading"),
    ("FreeKicks", "set_pieces", "free_kicks"), ("Penalty", "set_pieces", "penalty"),
    ("AerialAbility", "goalkeeping", "aerial_ability"), ("Handling", "goalkeeping", "handling"), ("Kicking", "goalkeeping", "kicking"),
    ("OneOnOne", "goalkeeping", "one_on_one"), ("Reflexes", "goalkeeping", "reflexes"),
]
# a keeper has no outfield skills in the generator; an outfield player has no goalkeeping skills
KEEPER_OUTFIELD = {"Tackling": 20, "Marking": 20, "Creativity": 35, "Crossing": 25, "Finishing": 12, "LongShots": 15, "OffTheBall": 20}
OUTFIELD_KEEPING = {"AerialAbility": 15, "Handling": 15, "Kicking": 22, "OneOnOne": 15, "Reflexes": 18}


def attr(p, name, group, key):
    v = p["stats"].get(group, {}).get(key)
    if v is not None:
        return int(v)
    if p["position"] == "goalkeeper":
        if name == "Passing": return int(p["stats"]["goalkeeping"]["kicking"] * 0.85)
        if name == "Positioning": return int((p["stats"]["goalkeeping"]["one_on_one"] + p["stats"]["goalkeeping"]["reflexes"]) / 2)
        return KEEPER_OUTFIELD[name]
    return OUTFIELD_KEEPING[name]


def foot_ratings(p):
    dom, weak = p["preferred_foot"]["dominant_foot"], int(p["preferred_foot"]["weak_foot_level"])
    return (3, weak) if dom == "Left" else (weak, 3)  # (left, right)


def left_footed(p):
    return p["preferred_foot"]["dominant_foot"] == "Left"


def score(p, kind):
    s = p["stats"]
    g = lambda grp, k: s.get(grp, {}).get(k, 30)
    if kind == "cb": return g("defense", "tackling") + g("defense", "marking") + g("athletic", "heading") + g("athletic", "physical_power")
    if kind == "fb": return g("defense", "tackling") + g("athletic", "speed") + g("athletic", "acceleration") + g("playmaking", "crossing") + p["stamina"]
    if kind == "wide": return g("athletic", "speed") + g("athletic", "acceleration") + g("athletic", "dribbling") + g("playmaking", "crossing")
    if kind == "cm": return g("playmaking", "passing") + g("playmaking", "creative") + g("mental", "decisions") + g("athletic", "ball_control") + p["stamina"]
    if kind == "st": return g("attack", "finishing") + g("attack", "off_the_ball") + g("athletic", "speed") + g("athletic", "heading")
    return sum(sum(v.values()) / len(v) for v in s.values())


def pick(pool, kinds, kind, n):
    """The n best of the first positions in `kinds` that has enough players, topped up from the rest of the pool."""
    chosen = []
    for pos in kinds:
        cand = sorted([p for p in pool if p["position"] == pos and p not in chosen], key=lambda p: -score(p, kind))
        chosen += cand[: n - len(chosen)]
        if len(chosen) == n:
            break
    if len(chosen) < n:  # not enough of those positions: the best of whoever is left
        chosen += sorted([p for p in pool if p not in chosen and p["position"] != "goalkeeper"], key=lambda p: -score(p, kind))[: n - len(chosen)]
    for p in chosen:
        pool.remove(p)
    return chosen


def side(two, wants_left_first=True):
    """Two players for the two flanks: left-footers on the left (an even split is by foot, the rest by order)."""
    a, b = two
    if left_footed(a) and not left_footed(b): return b, a  # (right, left)
    if left_footed(b) and not left_footed(a): return a, b
    return a, b


def squad(players):
    pool = list(players)
    keepers = sorted([p for p in pool if p["position"] == "goalkeeper"], key=lambda p: -score(p, "gk"))
    gk, gk2 = keepers[0], (keepers[1] if len(keepers) > 1 else None)
    pool = [p for p in pool if p["position"] != "goalkeeper"]
    cbs = pick(pool, ["defender"], "cb", 2)
    fbs = pick(pool, ["fullback", "defender"], "fb", 2)
    wide = pick(pool, ["winger", "midfielder"], "wide", 2)
    cms = pick(pool, ["midfielder"], "cm", 2)
    sts = pick(pool, ["striker", "winger"], "st", 2)
    rb, lb = side(fbs)
    rm, lm = side(wide)
    cbr, cbl = side(cbs)
    cmr, cml = side(cms)
    str_, stl = side(sts)
    starters = [gk, rb, cbr, cbl, lb, rm, cmr, cml, lm, str_, stl]
    bench = []
    if gk2: bench.append(gk2)
    bench += pick(pool, ["defender", "fullback"], "cb", 2)
    bench += pick(pool, ["midfielder", "winger"], "cm", 2)
    bench += pick(pool, ["striker", "winger"], "st", 2)
    return starters, bench


GROUP = {"goalkeeper": "Goalkeeper", "defender": "Defender", "fullback": "Defender", "midfielder": "Midfielder", "winger": "Midfielder", "striker": "Forward"}


def player_cs(p, pid, group_override=None):
    first, _, last = p["name"].partition(" ")
    left, right = foot_ratings(p)
    values = ", ".join(str(attr(p, n, g, k)) for n, g, k in ATTRS)
    fit = {"Low": "Low", "Moderate": "Normal", "Peak": "High"}[p["fitness"]]
    group = group_override or GROUP[p["position"]]
    return (f'            P({pid}, "{first}", "{last}", {p["shirt_number"]}, PositionGroup.{group}, {p["height"]}, '
            f'{p["stamina"]}, FitnessLevel.{fit}, {left}, {right}, new[] {{ {values} }}),')


def main():
    out = ['// <auto-generated> by tools/teams/build_teams.py from data/teams/*.json (the team generator). Do not edit by hand. </auto-generated>',
           "using System.Collections.Generic;", "", "namespace SoccerVision.Simulation", "{",
           "    /// <summary>The two demo squads, made by the team generator: real spread of speed, strength, height and feet.</summary>",
           "    public static class GeneratedTeams", "    {",
           "        private static readonly string[] Order =", "        {"]
    out.append("            " + ", ".join(f'"{n}"' for n, _, _ in ATTRS))
    out += ["        };", "",
            "        private static PlayerProfile P(int id, string first, string last, int number, PositionGroup group, int height, int stamina, FitnessLevel fitness, int left, int right, int[] v)",
            "        {",
            "            var a = new PlayerAttributes { Stamina = stamina, Fitness = fitness, LeftFoot = left, RightFoot = right };",
            "            a.Aggression = v[0]; a.Teamwork = v[1]; a.Composure = v[2]; a.Decisions = v[3];",
            "            a.Tackling = v[4]; a.Marking = v[5]; a.Positioning = v[6];",
            "            a.Creativity = v[7]; a.Passing = v[8]; a.Crossing = v[9];",
            "            a.Finishing = v[10]; a.LongShots = v[11]; a.OffTheBall = v[12];",
            "            a.PhysicalPower = v[13]; a.BallControl = v[14]; a.Dribbling = v[15]; a.Acceleration = v[16]; a.Speed = v[17]; a.Jumping = v[18]; a.Heading = v[19];",
            "            a.FreeKicks = v[20]; a.Penalty = v[21];",
            "            a.AerialAbility = v[22]; a.Handling = v[23]; a.Kicking = v[24]; a.OneOnOne = v[25]; a.Reflexes = v[26];",
            "            return new PlayerProfile { Id = id, FirstName = first, LastName = last, ShirtNumber = number, NaturalPosition = group, HeightCm = height, Attributes = a };",
            "        }", ""]
    pid = 100
    for name, fname in TEAMS:
        players = json.load(open(os.path.join(root, "data", "teams", fname + ".json")))
        starters, bench = squad(players)
        slots = ["GK", "RB", "CB right", "CB left", "LB", "RM", "CM right", "CM left", "LM", "ST right", "ST left"]
        out += [f"        /// <summary>{name} squad. Starters (4-4-2): " + "; ".join(f"{s} {p['name']} #{p['shirt_number']}" for s, p in zip(slots, starters)) + ".</summary>",
                f"        public static (List<PlayerProfile> Starters, List<PlayerProfile> Bench) {name}()", "        {",
                "            var starters = new List<PlayerProfile>", "            {"]
        for p in starters:
            pid += 1
            out.append(player_cs(p, pid, "Midfielder" if p in starters[5:9] else None))
        out += ["            };", "            var bench = new List<PlayerProfile>", "            {"]
        for p in bench:
            pid += 1
            out.append(player_cs(p, pid))
        out += ["            };", "            return (starters, bench);", "        }", ""]
        pid += 100 - pid % 100
    out += ["    }", "}", ""]
    path = os.path.join(root, "unity", "SoccerVision", "Assets", "SoccerVision", "Simulation", "Demo", "GeneratedTeams.cs")
    open(path, "w").write("\n".join(out))
    print("wrote", path)


main()
