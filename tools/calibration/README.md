# Calibration against real football

The engine's matches are compared with real ones: shots, goals and, above all, **how** the goals come about.

- `statsbomb_targets.py` reads a sample of matches from complete league seasons (Premier League, La Liga, Bundesliga, Serie A and
  Ligue 1, 2015/16) of the **StatsBomb open data** (<https://github.com/hudl/open-data>) and writes `targets.json`: shots and
  goals a match; where the goals were struck from; headers and penalties; how many foot goals were hit first time; the pass that set
  each open-play goal up (cross, through ball, ground pass or cut-back, long ball, a rebound, none); where the goalkeeper stood at
  the moment of the shot (from the freeze frame). Only aggregates are kept.
- `compare.py` puts the engine's measures (`SimRunner --diag`) next to them.

```
python3 tools/calibration/statsbomb_targets.py --per-league 40
dotnet run -c Release --project sim/SoccerVision.SimRunner -- --matches 100 --diag > sim.txt
python3 tools/calibration/compare.py sim.txt
```

**Data: StatsBomb.** Its terms ask that any published work based on the data names StatsBomb as the source.
