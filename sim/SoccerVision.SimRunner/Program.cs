using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using SoccerVision.Replay;
using SoccerVision.Simulation;

// Headless match runner: calibration statistics against real football, timelines and replay export.
//   dotnet run -c Release -- --matches 50
//   dotnet run -c Release -- --seed 7 --timeline
//   dotnet run -c Release -- --seed 7 --replay ../../web/public/replays/demo.json
//   dotnet run -c Release -- --matches 20 --diag          (decision mix / outcome counters)
//   dotnet run -c Release -- --matches 10 --shotorigins   (what leads to close-range shots)
//   dotnet run -c Release -- --matches 40 --seed 100 --scan  (one line per match: set pieces, goals; to pick demos)
//   dotnet run -c Release -- --golden                    (a fingerprint of a fixed set of matches: events, frames, stats; the same before and
//                                                          after a change that must not alter how the match plays)
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

int matches = 20;
ulong seed = 1;
bool timeline = false;
string replayPath = null;
float? motion = null;
int replayFps = 8;
var conditions = new MatchConditions();
float homeAdvantage = 1f, momentumStrength = 1f;
MatchScript script = null;
// --home "line=0.9,width=0.2,passing=short,tempo=0.8,aggression=0.8": the home team's tactics
string homeTactics = null, awayTactics = null;
bool golden = false;
int exportRoster = 0;
// --home-strength / --away-strength weak|medium|strong: how good each demo squad is (medium: the squads the game always had)
TeamStrength homeStrength = TeamStrength.Medium, awayStrength = TeamStrength.Medium;
bool legacyFlight = false, legacyBounce = false, legacyKick = false, legacyCollide = false, artificial = false; int grassLength = 0; bool swap = false; var adjusts = new List<(string Name, int Delta)>();
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--matches": matches = int.Parse(args[++i]); break;
        case "--seed": seed = ulong.Parse(args[++i]); break;
        case "--timeline": timeline = true; matches = 1; break;
        case "--replay": replayPath = args[++i]; break;
        case "--motion": motion = float.Parse(args[++i]); break;
        case "--fps": replayFps = int.Parse(args[++i]); break;
        // weather and pitch: --weather sunny|cloudy|rain|snow  --maintenance 0..1  --wind m/s  --windangle degrees  --night
        case "--weather": conditions.Weather = Enum.Parse<Weather>(args[++i], true); break;
        case "--maintenance": conditions.Maintenance = float.Parse(args[++i]); break;
        case "--wind": conditions.WindSpeed = float.Parse(args[++i]); break;
        case "--windangle": conditions.WindAngle = float.Parse(args[++i]) * MathF.PI / 180f; break;
        case "--night": conditions.Night = true; break;
        // --script staged: the staged match (light injury, yellow, direct red, serious injury + substitution, second yellow and red)
        // --homeadv 0..1 (default 1: as in the top leagues)  --momentum 0..1 (default 1)
        case "--homeadv": homeAdvantage = float.Parse(args[++i]); break;
        case "--momentum": momentumStrength = float.Parse(args[++i]); break;
        case "--home": homeTactics = args[++i]; break;
        case "--away": awayTactics = args[++i]; break;
        case "--spread": TeamStrengthScaling.StrongSpread = TeamStrengthScaling.WeakSpread = float.Parse(args[++i]); break;
        case "--strong-spread": TeamStrengthScaling.StrongSpread = float.Parse(args[++i]); break;
        case "--weak-spread": TeamStrengthScaling.WeakSpread = float.Parse(args[++i]); break; // how far weak / strong squads are from the medium one: 0..1 (see TeamStrengthScaling)
        case "--swap": swap = true; break;
        case "--scoreeffect": TeamAgent.ScoreEffect = float.Parse(args[++i]); break; // (experiment: how strongly the score changes the way a team plays)
        case "--scorepatience": MatchEngine.ScorePatience = float.Parse(args[++i]); break; // (experiment)
        case "--patienttouch": MatchEngine.PatientTouch = float.Parse(args[++i]); break; // (experiment: the time a patient team takes on the ball)
        case "--adjust": { var kv = args[++i].Split('='); adjusts.Add((kv[0], int.Parse(kv[1]))); break; } // (diagnosis: --adjust Stamina=4 adds 4 to that attribute of every player, both teams; repeatable)
        case "--chest": MatchEngine.ChestControlHeight = float.Parse(args[++i]); break; // (experiment: 0 = no chest control, as before)
        case "--shootkeeper": MatchEngine.ShootBeforeKeeper = float.Parse(args[++i]); break; // (experiment)
        case "--midshots": MatchEngine.MidRangeShotAppetite = float.Parse(args[++i]); break; // (experiment)
        case "--foulrate": MatchEngine.FoulRate = float.Parse(args[++i]); break; // (experiment)
        case "--rangesave": MatchEngine.RangeSaveScale = float.Parse(args[++i]); break; // (experiment)
        case "--xgscale": MatchEngine.XgScale = float.Parse(args[++i]); break; // (experiment: the chance a player sees in a shot)
        case "--boxshots": MatchEngine.BoxShotAppetite = float.Parse(args[++i]); break; // (experiment: how readily a player shoots inside the box)
        case "--mangap": MatchEngine.ManMarkGapScale = float.Parse(args[++i]); break; // (experiment)
        case "--nofreeman": MatchEngine.ManMarkFreeMan = false; break; // (comparison: man-to-man without a free man)
        case "--passon": MatchEngine.ManMarkPassOnDistance = float.Parse(args[++i]); break; // (experiment; 999 = never hands his man on)
        case "--swapgain": MatchEngine.ManMarkSwapGain = float.Parse(args[++i]); break; // (experiment; 999 = never swaps)
        case "--coverdepth": MatchEngine.ManMarkCoverDepth = float.Parse(args[++i]); break; // (experiment)
        case "--onetworun": MatchEngine.OneTwoRunChance = float.Parse(args[++i]); break; // (experiment; polish round 4)
        case "--onetwolane": MatchEngine.OneTwoFirstTimeLane = float.Parse(args[++i]); break; // (experiment; polish round 4)
        case "--onetwopast": MatchEngine.OneTwoPastHisMan = float.Parse(args[++i]); break; // (experiment; polish round 4)
        case "--headerminxg": MatchEngine.HeaderMinXg = float.Parse(args[++i]); break; // (experiment; polish round 1)
        case "--volleyminxg": MatchEngine.VolleyMinXg = float.Parse(args[++i]); break; // (experiment; polish round 1)
        case "--playoffpressure": MatchEngine.PlayOffPressure = float.Parse(args[++i]); break; // (experiment; polish round 1)
        case "--headercontest": MatchEngine.HeaderContestError = float.Parse(args[++i]); break; // (experiment; 0 = a contested header is aimed as a free one)
        case "--contestedatgoal": MatchEngine.ContestedHeaderAtGoal = float.Parse(args[++i]); break; // (experiment)
        case "--zoneradius": MatchEngine.ZoneRadiusScale = float.Parse(args[++i]); break; // (experiment)
        case "--savebase": MatchEngine.SaveBase = float.Parse(args[++i]); break; // (experiment: the save chance of an easy shot)
        case "--legacyflight": legacyFlight = true; break;
        case "--legacykick": legacyKick = true; break; // (comparison: the kicks as they were before stage 7)
        case "--legacycollide": legacyCollide = true; break; // (comparison: the collisions as they were before stage 8)
        case "--bench": Console.Write(BallTestBench.Markdown(BallTestBench.Run())); return; // (the ball-physics test bench, stage 11: docs/BALL_TEST_BENCH.md)
        case "--legacybounce": legacyBounce = true; break; // (comparison: the bounce as it was before stage 4)
        case "--artificial": artificial = true; break; // (the pitch is football turf)
        case "--grass": grassLength = args[++i] == "short" ? -1 : args[i] == "long" ? 1 : 0; break; // (short / normal / long natural grass) // (comparison: the ball flight as it was before the ball-physics work)
        case "--closereach": MatchEngine.CloseReachBase = float.Parse(args[++i]); break; // (experiment: a keeper's reach against a shot that is on him at once)
        case "--home-strength": homeStrength = Enum.Parse<TeamStrength>(args[++i], true); break;
        case "--away-strength": awayStrength = Enum.Parse<TeamStrength>(args[++i], true); break;
        case "--script": if (args[++i] == "staged") script = MatchScript.Staged(); break;
        case "--golden": golden = true; break;
        case "--export-roster": exportRoster = int.Parse(args[++i]); break;
    }
}

if (exportRoster > 0)
{
    // the looks of the demo squads of the first N matches as the face generator's roster (tools/faces/generate_faces.py)
    var list = new List<string>();
    for (ulong sd = 1; sd <= (ulong)exportRoster; sd++)
    {
        ulong nm = sd * 2654435761UL + 17UL;
        foreach (var t in new[] { DemoTeams.Red(nm), DemoTeams.Blue(nm) })
            foreach (var p in t.Starters.Concat(t.Bench))
            {
                var lk = p.Appearance;
                list.Add(JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["key"] = lk.FaceSeed.ToString(), ["age"] = p.Age,
                    ["look"] = new Dictionary<string, object> { ["origin"] = p.Origin, ["look"] = lk.Look, ["skin"] = lk.Skin, ["hair"] = lk.Hair, ["hairColour"] = lk.HairColour, ["facial"] = lk.Facial, ["build"] = lk.Build, ["seed"] = lk.FaceSeed },
                }));
            }
    }
    Console.WriteLine("[" + string.Join(",\n", list) + "]");
    return;
}

if (golden)
{
    Golden();
    return;
}

MatchConfig MakeConfig()
{
    var c = MatchConfig.CreateDefault();
    if (legacyFlight) c.Aero = null;
    if (legacyBounce) c.Ground = null;
    if (legacyKick) c.Kick = null;
    if (legacyCollide) c.Collisions = null;
    c.ArtificialTurf = artificial;
    c.GrassLength = grassLength;
    c.HomeAdvantage = homeAdvantage;
    c.MomentumStrength = momentumStrength;
    if (motion.HasValue) c.MotionSpeed = motion.Value;
    c.Conditions = conditions.Clone();
    c.Script = script;
    return c;
}

// --swap: Team B (the blue squad) plays at home and Team A away - the same two squads in the other seats, to tell a home edge from a difference between the squads
TeamSetup Adjusted(TeamSetup t)
{
    foreach (var (name, delta) in adjusts)
    {
        var f = typeof(PlayerAttributes).GetField(name) ?? throw new ArgumentException("no attribute " + name);
        foreach (var pl in t.Starters.Concat(t.Bench)) f.SetValue(pl.Attributes, Math.Clamp((int)f.GetValue(pl.Attributes) + delta, 1, 99));
    }
    return t;
}
TeamSetup Home() => WithTactics(Adjusted(swap ? DemoTeams.Blue(1001, homeStrength) : DemoTeams.Red(1001, homeStrength)), homeTactics);
TeamSetup Away() => WithTactics(Adjusted(swap ? DemoTeams.Red(1001, awayStrength) : DemoTeams.Blue(1001, awayStrength)), awayTactics);

static TeamSetup WithTactics(TeamSetup t, string spec)
{
    if (spec == null) return t;
    foreach (var kv in spec.Split(','))
    {
        var parts = kv.Split('=');
        string k = parts[0].Trim(), v = parts[1].Trim();
        switch (k)
        {
            case "line": t.Tactics.DefensiveLine = float.Parse(v); break;
            case "width": t.Tactics.Width = float.Parse(v); break;
            case "tempo": t.Tactics.Tempo = float.Parse(v); break;
            case "passing": t.Tactics.Passing = Enum.Parse<PassingStyle>(v, true); break;
            case "defence": t.Tactics.Defence = v == "press" ? DefenceStyle.HighPress : v == "trap" ? DefenceStyle.OffsideTrap : v == "bus" ? DefenceStyle.ParkTheBus : v == "highline" ? DefenceStyle.HighBackline : v == "inverted" ? DefenceStyle.InvertedFullBacks : DefenceStyle.Balanced; break;
            case "attack": t.Tactics.Attack = v == "crosses" ? AttackStyle.CrossesIntoBox : v == "runs" ? AttackStyle.RunsInBehind : v == "long" ? AttackStyle.LongShots : AttackStyle.Mixed; break;
            case "special": t.Tactics.Special = v == "possession" ? SpecialTactic.Possession : v == "waste" ? SpecialTactic.TimeWasting : v == "counter" ? SpecialTactic.CounterAttack : v == "siege" ? SpecialTactic.Siege : SpecialTactic.None; break;
            case "marking": t.Tactics.Marking = v == "man" ? MarkingStyle.ManToMan : MarkingStyle.Zonal; break;
            case "tackle": t.Tactics.Tackling = v == "stay" ? TacklingStyle.StayOnFeet : v == "stuck" ? TacklingStyle.GetStuckIn : TacklingStyle.Normal; break;
            case "formation": TeamPlan.Parse("formation=" + v).ApplyBeforeKickOff(t); break;
            case "aggression":
                float a = float.Parse(v);
                t.Tactics.PressingIntensity = 0.5f + 0.6f * (a - 0.5f);
                t.Tactics.Tackling = a < 0.34f ? TacklingStyle.StayOnFeet : a > 0.66f ? TacklingStyle.GetStuckIn : TacklingStyle.Normal;
                break;
        }
    }
    return t;
}

if (args.Contains("--poss"))
{
    foreach (var tt in new[] { Home(), Away() }) Console.WriteLine($"tactics: press {tt.Tactics.PressingIntensity} defence {tt.Tactics.Defence} passing {tt.Tactics.Passing} special {tt.Tactics.Special} line {tt.Tactics.DefensiveLine} tempo {tt.Tactics.Tempo}");
    // how the ball is shared: per team, per match - possession, passes, pass completion, tackles won, interceptions (so a change that should move possession can be seen)
    double[] xgs = new double[2], gls = new double[2], secs = new double[2], spells = new double[2], pos = new double[2], ps = new double[2], pc = new double[2], tw = new double[2], ic = new double[2], sh = new double[2];
    var tempoSum = new List<double[]>(); var spread = new List<double>(); double[] qual = new double[2]; var byState = new double[5, 2]; bool randomPlans = args.Contains("--random");
    for (int m = 0; m < matches; m++)
    {
        // --random: as the game's random match - both managers pick a random plan and change the tactics during the match
        var hs = Home(); var aw = Away(); ulong ms = seed + (ulong)m;
        if (randomPlans) { TeamPlan.Random(hs, (int)ms * 7 + 1).ApplyBeforeKickOff(hs); TeamPlan.Random(aw, (int)ms * 7 + 2).ApplyBeforeKickOff(aw); }
        var e = new MatchEngine(hs, aw, ms, MakeConfig());
        var mgr = randomPlans ? new[] { new ComputerManager(e, 0), new ComputerManager(e, 1) } : null;
        double h0 = 0, a0 = 0;
        for (int it = 0; !e.IsFinished && it < 10000000; it++)
        {
            e.Advance(0.25f);
            if (mgr != null) { mgr[0].Update(); mgr[1].Update(); }
            // the share of the ball by the score at the time (from each side's view: the home side at d, the away side at -d)
            double h1 = e.Home.Stats.PossessionSeconds, a1 = e.Away.Stats.PossessionSeconds;
            int d = Math.Clamp(e.Home.Score - e.Away.Score, -2, 2);
            byState[d + 2, 0] += h1 - h0; byState[d + 2, 1] += a1 - a0;
            byState[-d + 2, 0] += a1 - a0; byState[-d + 2, 1] += h1 - h0;
            h0 = h1; a0 = a1;
        }
        var st = new[] { e.Home.Stats, e.Away.Stats };
        foreach (var x in st) tempoSum.Add(new double[] { x.OpenPlayPasses, x.PassLengthSum, x.ShortPasses, x.LongPasses, x.PassRuns, x.PassGapSum, x.PassGapCount });
        double tot = st[0].PossessionSeconds + st[1].PossessionSeconds;
        spread.Add(100.0 * st[0].PossessionSeconds / tot);
        qual[0] += e.Home.PassingQualityValue; qual[1] += e.Away.PassingQualityValue;
        for (int i = 0; i < 2; i++) { xgs[i] += st[i].ExpectedGoals; gls[i] += st[i].Goals; secs[i] += st[i].PossessionSeconds; spells[i] += st[i].PossessionSpells; pos[i] += 100.0 * st[i].PossessionSeconds / tot; ps[i] += st[i].Passes; pc[i] += st[i].PassesCompleted; tw[i] += st[i].TacklesWon; ic[i] += st[i].Interceptions; sh[i] += st[i].Shots; }
    }
    spread.Sort();
    string P(double[] a, int i) => (a[i] / matches).ToString("0.0");
    Console.WriteLine($"home: possession {P(pos, 0)}%  passes {P(ps, 0)}  completion {(100 * pc[0] / ps[0]):0}%  tackles won {P(tw, 0)}  interceptions {P(ic, 0)}  shots {P(sh, 0)}  goals {P(gls, 0)}  xG {P(xgs, 0)}  possessions {P(spells, 0)}  s/possession {(secs[0] / Math.Max(1, spells[0])):0.0}");
    Console.WriteLine($"away: possession {P(pos, 1)}%  passes {P(ps, 1)}  completion {(100 * pc[1] / ps[1]):0}%  tackles won {P(tw, 1)}  interceptions {P(ic, 1)}  shots {P(sh, 1)}  goals {P(gls, 1)}  xG {P(xgs, 1)}  possessions {P(spells, 1)}  s/possession {(secs[1] / Math.Max(1, spells[1])):0.0}");
    {
        // the pace of passing, as tools/calibration/passing_tempo.py counts it from real matches (per team per match)
        double top = 0, tpl = 0, tsh = 0, tlg = 0, runs = 0, gs = 0, gn = 0; int tm = 0;
        foreach (var x in tempoSum) { top += x[0]; tpl += x[1]; tsh += x[2]; tlg += x[3]; runs += x[4]; gs += x[5]; gn += x[6]; tm += 1; }
        Console.WriteLine($"tempo (per team per match): open-play passes {top / tm:0.0}  mean length {tpl / Math.Max(1, top):0.0} m  short {100 * tsh / Math.Max(1, top):0.0}%  long {100 * tlg / Math.Max(1, top):0.0}%  passes per run {top / Math.Max(1, runs):0.00}  runs {runs / tm:0.0}  seconds between passes {gs / Math.Max(1, gn):0.00}");
    }
    Console.WriteLine($"pass share (how Opta's published possession is worked out): home {(100 * ps[0] / (ps[0] + ps[1])):0.0}%  |  passing quality (starting XI): home {qual[0] / matches:0.0}  away {qual[1] / matches:0.0}");
    string[] stateNames = { "trailing by 2+", "trailing by 1", "level", "leading by 1", "leading by 2+" };
    Console.WriteLine("share of the ball by the score (both sides, pooled seconds): " + string.Join("  ", Enumerable.Range(0, 5).Where(k => byState[k, 0] + byState[k, 1] > 0).Select(k => $"{stateNames[k]} {100 * byState[k, 0] / (byState[k, 0] + byState[k, 1]):0.0}%")));
    Console.WriteLine($"home possession per match: min {spread[0]:0}  10% {spread[matches / 10]:0}  median {spread[matches / 2]:0}  90% {spread[matches * 9 / 10]:0}  max {spread[^1]:0}");
    return;
}

if (args.Contains("--shotorigins"))
{
    var origins = new Dictionary<string, int>();
    int total = 0;
    for (int m = 0; m < matches; m++)
    {
        var e = new MatchEngine(DemoTeams.Red(1001, homeStrength), DemoTeams.Blue(1001, awayStrength), seed + (ulong)m, MakeConfig());
        e.RunToEnd(0.25f);
        var log = e.EventLog.Where(x => x.Type != MatchEventType.Whistle && x.Type != MatchEventType.NetHit).ToList();
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i].Type != MatchEventType.Shot) continue;
            var shot = log[i];
            var team = e.TeamOf(shot.Team);
            // distance from the goal attacked at that time: infer from shot X sign relative to own goal
            float d = MathF.Min(MathF.Sqrt((52.5f - shot.X) * (52.5f - shot.X) + shot.Y * shot.Y), MathF.Sqrt((52.5f + shot.X) * (52.5f + shot.X) + shot.Y * shot.Y));
            if (d > 12f) continue;
            total++;
            var prev = new List<string>();
            for (int k = i - 1; k >= 0 && prev.Count < 3; k--)
            {
                if (shot.ClockSeconds - log[k].ClockSeconds > 12f) break;
                prev.Add(log[k].Type + (log[k].Team == shot.Team ? "+" : "-"));
            }
            string key = prev.Count > 0 ? string.Join(" < ", prev) : "(none)";
            origins.TryGetValue(key, out int v);
            origins[key] = v + 1;
        }
    }
    Console.WriteLine($"close shots: {total} in {matches} matches");
    foreach (var kv in origins.OrderByDescending(k => k.Value).Take(25)) Console.WriteLine($"{kv.Value,5}  {kv.Key}");
    return;
}

if (replayPath != null)
{
    ExportReplay(replayPath, seed, MakeConfig(), replayFps);
    return;
}

var results = new MatchResult[matches];
var diagnostics = new Dictionary<string, int>();
var sw = Stopwatch.StartNew();
Parallel.For(0, matches, i =>
{
    var engine = new MatchEngine(Home(), Away(), seed + (ulong)i, MakeConfig());
    engine.CollectDiagnostics = args.Contains("--diag");
    var t0 = Stopwatch.GetTimestamp();
    engine.RunToEnd(0.25f);
    double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    results[i] = MatchResult.From(engine, ms);
    lock (diagnostics)
    {
        diagnostics.TryGetValue("keeper.chargedown", out int cd);
        diagnostics["keeper.chargedown"] = cd + engine.KeeperChargeDowns;
        diagnostics.TryGetValue("keeper.chargedown.goal", out int cg);
        diagnostics["keeper.chargedown.goal"] = cg + engine.KeeperChargeDownGoals;
        foreach (var kv in engine.Diagnostics)
        {
            diagnostics.TryGetValue(kv.Key, out int v);
            diagnostics[kv.Key] = v + kv.Value;
        }
    }
});
sw.Stop();

if (args.Contains("--scan"))
{
    for (int i = 0; i < matches; i++)
    {
        var e = new MatchEngine(Home(), Away(), seed + (ulong)i, MakeConfig());
        e.RunToEnd(0.25f);
        var log = e.EventLog;
        Console.WriteLine($"seed {seed + (ulong)i,6}  {e.Home.Score}-{e.Away.Score}  pens {log.Count(x => x.Type == MatchEventType.PenaltyAwarded)}"
            + $"  fkShots {log.Count(x => x.Type == MatchEventType.Shot && x.Detail == 3)}  wallFks {log.Count(x => x.Type == MatchEventType.FreeKick && x.Detail == 1)}"
            + $"  cards {log.Count(x => x.Type == MatchEventType.YellowCard)}/{log.Count(x => x.Type == MatchEventType.RedCard)}  woodwork {log.Count(x => x.Type == MatchEventType.HitWoodwork)}"
            + $"  offsides {log.Count(x => x.Type == MatchEventType.Offside)}  headerGoals {log.Count(x => x.Type == MatchEventType.Goal && x.Detail == 5)}  volleyGoals {log.Count(x => x.Type == MatchEventType.Goal && x.Detail == 1)}");
    }
}

if (timeline)
{
    var engine = new MatchEngine(DemoTeams.Red(1001, homeStrength), DemoTeams.Blue(1001, awayStrength), seed, MakeConfig());
    engine.RunToEnd(0.25f);
    foreach (var e in engine.EventLog)
    {
        if (e.Type == MatchEventType.Whistle || e.Type == MatchEventType.NetHit || e.Type == MatchEventType.Pass) continue;
        Console.WriteLine($"{e.Minute,7} {e.Type,-24} {e.Text}");
    }
    Console.WriteLine();
}

Report(results, sw.Elapsed.TotalSeconds);
if (args.Contains("--diag"))
{
    Console.WriteLine();
    Console.WriteLine("Diagnostics (per match):");
    foreach (var kv in diagnostics.OrderBy(k => k.Key)) Console.WriteLine($"  {kv.Key,-40}{kv.Value / (double)matches,9:0.00} ({kv.Value})");
}

static void Report(MatchResult[] r, double wallSeconds)
{
    double Avg(Func<MatchResult, double> f) => r.Average(f);
    Console.WriteLine($"Matches: {r.Length}   wall time: {wallSeconds:0.0}s   avg sim cost per match: {Avg(x => x.WallMs):0} ms");
    Console.WriteLine($"Finished: {r.Count(x => x.Finished)}/{r.Length}   recovered errors: {r.Sum(x => x.RecoveredErrors)}");
    Console.WriteLine();
    Console.WriteLine("Per match (both teams)        sim     real-world reference. [C] = counted from match data by tools/calibration/real_leagues.py (Premier League 2015/16-2024/25, see CLAIMS.md); [S] = counted from StatsBomb open data by a tools/calibration script (Premier League 2015/16, see CLAIMS.md); every other figure is an unsourced estimate");
    void Row(string name, double v, string reference) => Console.WriteLine($"  {name,-26}{v,7:0.00}   {reference}");
    Row("Goals", Avg(x => x.Goals), "[C] 2.83");
    Row("xG", Avg(x => x.Xg), "~ goals");
    Row("Shots", Avg(x => x.Shots), "[C] 25.4");
    Row("Shots on target", Avg(x => x.ShotsOnTarget), "[C] 8.8");
    Row("Shots blocked", Avg(x => x.ShotsBlocked), "6-7");
    Row("Corners", Avg(x => x.Corners), "[C] 10.4");
    Row("Fouls", Avg(x => x.Fouls), "[C] 21.5");
    Row("Yellow cards", Avg(x => x.Yellows), "[C] 3.44");
    Row("Red cards", Avg(x => x.Reds), "[C] 0.12");
    Row("Offsides", Avg(x => x.Offsides), "~3.4 (unsourced)");
    Row("Throw-ins", Avg(x => x.ThrowIns), "[S] 46.2");
    Row("Goal kicks", Avg(x => x.GoalKicks), "14-18");
    Row("Free kicks", Avg(x => x.FreeKicks), "~ fouls + offsides");
    Row("Penalties", Avg(x => x.Penalties), "0.25-0.35");
    Row("Penalty conversion %", r.Sum(x => x.Penalties) > 0 ? 100.0 * r.Sum(x => x.PenaltiesScored) / r.Sum(x => x.Penalties) : 0, "75-80");
    Row("Saves", Avg(x => x.Saves), "5-6");
    Row("Keeper sorties", Avg(x => x.Sorties), "n/a");
    Row("Headed shots", Avg(x => x.Headers), "16.5% of shots (StatsBomb)");
    Row("Volleys", Avg(x => x.Volleys), "n/a");
    Row("Direct free-kick shots", Avg(x => x.FreeKickShots), "~0.6-1");
    Row("Headed goals % of goals", 100.0 * r.Sum(x => x.HeaderGoals) / Math.Max(1, r.Sum(x => x.Goals)), "13-19 (PL)");
    Row("Volley goals % of goals", 100.0 * r.Sum(x => x.VolleyGoals) / Math.Max(1, r.Sum(x => x.Goals)), "n/a");
    Row("Free-kick goals", Avg(x => x.FreeKickGoals), "~0.05-0.1");
    Row("Woodwork hits", Avg(x => x.Woodwork), "~0.6-0.8 (est.)");
    Row("Keeper catches", Avg(x => x.Catches), "n/a");
    Row("Keeper parries", Avg(x => x.Parries), "n/a");
    Row("Keeper punches", Avg(x => x.Punches), "~1-2");
    Row("Cut-inside shots", Avg(x => x.CutIns), "n/a");
    Row("Passes", Avg(x => x.Passes), "850-1000 (in 57 min ball-in-play)");
    Row("Pass completion %", Avg(x => x.PassPct), "80-85");
    Row("Tackles", Avg(x => x.Tackles), "30-40");
    Row("  standing", Avg(x => x.StandingTackles), "~75-85% of tackles");
    Row("  sliding", Avg(x => x.SlideTackles), "~15-25% of tackles");
    Row("  slides won", Avg(x => x.SlideTacklesWon), "n/a");
    Row("Tackles won %", 100.0 * r.Sum(x => x.TacklesWon) / Math.Max(1, r.Sum(x => x.Tackles)), "~60-65");
    Row("Passes won by the opponent", Avg(x => x.Interceptions), "every lost pass, keepers incl. (Opta interceptions ~16-17)");
    Row("  read and stepped in front", Avg(x => x.AnticipatedInterceptions), "n/a");
    Row("Through balls (completed)", Avg(x => x.ThroughBalls), Avg(x => x.ThroughBallsCompleted).ToString("0.00"));
    Row("Passes into space", Avg(x => x.PassesIntoSpace), "n/a");
    Row("Shielded out for our restart", Avg(x => x.ShieldedOut), "n/a");
    Row("Shoulder duels won", Avg(x => x.ShoulderDuelsWon), "n/a");
    Row("Switches of play (completed)", Avg(x => x.Switches), Avg(x => x.SwitchesCompleted).ToString("0.00"));
    {
        string[] names = { "Goalkeeper", "Centre-back", "Full-back", "Defensive mid", "Central mid", "Attacking mid", "Winger / wide", "Striker" };
        double tot = Math.Max(1, r.Sum(x => x.GoalsByKind.Sum()));
        Console.WriteLine("  Goals by position (share of goals, per match):");
        for (int i = 0; i < 8; i++) { int sum = r.Sum(x => x.GoalsByKind[i]); int shots = r.Sum(x => x.ShotsByKind[i]); Console.WriteLine($"    {names[i],-16}{100.0 * sum / tot,6:0.0}%   {(double)sum / r.Length,5:0.00}   shots {(double)shots / r.Length,5:0.00}   conv {(shots > 0 ? 100.0 * sum / shots : 0),5:0.0}%"); }
    }
    Row("Penalties: home / away conv %", 100.0 * r.Sum(x => x.HomePensScored) / Math.Max(1, r.Sum(x => x.HomePens)), (100.0 * r.Sum(x => x.AwayPensScored) / Math.Max(1, r.Sum(x => x.AwayPens))).ToString("0.0") + "  (n " + r.Sum(x => x.HomePens) + " / " + r.Sum(x => x.AwayPens) + ")");
    Row("Fouls: home / away", Avg(x => x.HomeFouls), Avg(x => x.AwayFouls).ToString("0.00"));
    Row("Yellows: home / away", Avg(x => x.HomeYellows), Avg(x => x.AwayYellows).ToString("0.00"));
    Row("Knuckleballs", Avg(x => x.Knuckles), "");
    Row("One-twos (completed)", Avg(x => x.OneTwos), Avg(x => x.OneTwosCompleted).ToString("0.00"));
    Row("Third-man runs / lay-offs (done)", Avg(x => x.ThirdManRuns), Avg(x => x.ThirdManPasses).ToString("0.00") + " / " + Avg(x => x.ThirdManCompleted).ToString("0.00"));
    Row("Counter-movements", Avg(x => x.CounterMovements), "");
    Row("Counter-presses (won back)", Avg(x => x.CounterPresses), Avg(x => x.CounterPressWins).ToString("0.00"));
    Row("Wide overloads", Avg(x => x.Overloads), "");
    Row("Long throws", Avg(x => x.LongThrows), "[S] 1.43 throw-ins into the box (all of them, long or not)");
    Row("Home: passes / long balls", Avg(x => x.HomePasses), Avg(x => x.HomeLongBalls).ToString("0.0"));
    Row("Home: possession % / goals", Avg(x => x.HomePossession), Avg(x => x.HomeGoals).ToString("0.00") + " (away " + Avg(x => x.AwayGoals).ToString("0.00") + ")");
    Row("Home: fouls / yellows", Avg(x => x.HomeFouls), Avg(x => x.HomeYellows).ToString("0.00"));
    Row("Home: avg back-line depth (m from own goal)", Avg(x => x.HomeLineDepth), "width of outfield spread " + Avg(x => x.HomeSpread).ToString("0.0") + " m");
    Row("Home: midfield / forwards depth (m)", Avg(x => x.HomeMidDepth), Avg(x => x.HomeAttackDepth).ToString("0.0"));
    Row("Real minutes per match", Avg(x => x.RealMinutes), "");
    Row("Home: crosses / long shots (18 m+)", Avg(x => x.HomeCrosses), Avg(x => x.HomeLongShots).ToString("0.0"));
    Row("Home: stamina left at the end (%) / shots", Avg(x => x.HomeEnergy), Avg(x => x.HomeShots).ToString("0.0") + " (away " + Avg(x => x.AwayShots).ToString("0.0") + ")");
    Row("Away caught offside / through balls home", Avg(x => x.AwayOffsides), Avg(x => x.HomeThrough).ToString("0.0"));
    Row("Crosses by full-backs", Avg(x => x.FullBackCrosses), "~35-45% of crosses");
    Row("Crosses by wingers", Avg(x => x.WingerCrosses), "~35-45% of crosses");
    Row("Shots by full-backs", Avg(x => x.FullBackShots), "~1-2");
    Row("Shots by wingers", Avg(x => x.WingerShots), "~5-7");
    Row("Cut-backs from the by-line", Avg(x => x.CutBacks), "n/a");
    Row("Crosses (all)", Avg(x => x.CrossesAll), "[S] 23.0 in open play (this row counts set-piece crosses too)");
    Row("Injuries: light (played on)", Avg(x => x.LightInjuries), "~0.5-0.8 (time-loss ~0.7-1 per match)");
    Row("Injuries: serious (had to go off)", Avg(x => x.SeriousInjuries), "~0.2-0.4");
    Row("Substitutions for injury", Avg(x => x.Substitutions), "~0.2-0.4");
    Row("Goals home / away", Avg(x => x.HomeGoals), Avg(x => x.AwayGoals).ToString("0.00"));
    Row("Results: home win / draw / away win %", 100.0 * r.Count(x => x.HomeGoals > x.AwayGoals) / r.Count(), (100.0 * r.Count(x => x.HomeGoals == x.AwayGoals) / r.Count()).ToString("0") + " / " + (100.0 * r.Count(x => x.HomeGoals < x.AwayGoals) / r.Count()).ToString("0"));
    Row("Shots home / away", Avg(x => x.HomeShots), Avg(x => x.AwayShots).ToString("0.00"));
    Row("Marking switches (zonal home / m2m away)", Avg(x => x.HomeSwitches), Avg(x => x.AwaySwitches).ToString("0.0"));
    Row("Home possession %", Avg(x => x.HomePossession), "50 +/- 15");
    Row("Added time 1st half (min)", Avg(x => x.Added1), "1-3");
    Row("Added time 2nd half (min)", Avg(x => x.Added2), "3-7");
    Row("Real 1st half (s)", Avg(x => x.Real1), "600 + added");
    Row("Real half-time break (s)", Avg(x => x.RealHt), "120");
    Row("Real 2nd half (s)", Avg(x => x.Real2), "600 + added");
    Row("Final clock (min)", Avg(x => x.FinalClock / 60.0), ">= 90");
    Row("Distance per team (km)", Avg(x => x.DistanceKm), "105-115");
    Row("Per player: fouls  anchor / CM / CB / FB / wide / striker", Avg(x => x.FoulsAnchor), $"{Avg(x => x.FoulsCm):0.00} / {Avg(x => x.FoulsCb):0.00} / {Avg(x => x.FoulsFb):0.00} / {Avg(x => x.FoulsWide):0.00} / {Avg(x => x.FoulsSt):0.00}");
    Row("Per player: tackles anchor / CM / CB / FB / wide / striker", Avg(x => x.TacklesAnchor), $"{Avg(x => x.TacklesCm):0.00} / {Avg(x => x.TacklesCb):0.00} / {Avg(x => x.TacklesFb):0.00} / {Avg(x => x.TacklesWide):0.00} / {Avg(x => x.TacklesSt):0.00}");
    Row("Per player: intercepts anchor / CM / CB / FB / wide / striker", Avg(x => x.IntAnchor), $"{Avg(x => x.IntCm):0.00} / {Avg(x => x.IntCb):0.00} / {Avg(x => x.IntFb):0.00} / {Avg(x => x.IntWide):0.00} / {Avg(x => x.IntSt):0.00}");
    Row("Per player: passes   anchor / CM / CB / FB / wide / striker", Avg(x => x.PassAnchor), $"{Avg(x => x.PassCm):0.0} / {Avg(x => x.PassCb):0.0} / {Avg(x => x.PassFb):0.0} / {Avg(x => x.PassWide):0.0} / {Avg(x => x.PassSt):0.0}");
    Row("Avg final energy", Avg(x => x.AvgEnergy), "fatigue model");
    Row("  full-backs", Avg(x => x.EnergyFullBack), "lowest of the outfield group");
    Row("  centre-backs", Avg(x => x.EnergyCentreBack), "n/a");
    Row("  wide midfielders", Avg(x => x.EnergyWide), "n/a");
    Row("  central midfielders", Avg(x => x.EnergyCentral), "n/a");
    Row("  strikers", Avg(x => x.EnergyStriker), "highest of the outfield group");
    Row("  goalkeepers", Avg(x => x.EnergyKeeper), "highest");
    Console.WriteLine();
    var hist = r.GroupBy(x => x.Goals).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}");
    Console.WriteLine("Goals distribution: " + string.Join("  ", hist));
    Console.WriteLine("Scores: " + string.Join(" ", r.Take(30).Select(x => x.Score)));
}

static void ExportReplay(string path, ulong seed, MatchConfig config, int fps)
{
    var engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, config);
    var rec = new ReplayRecorder(engine, fps);
    var frames = new StringBuilder();
    var events = new StringBuilder();
    var stats = new StringBuilder();
    int frameCount = 0, eventCount = 0, guard = 0;
    while (!engine.IsFinished && guard++ < 2000)
    {
        var chunk = rec.Step(500);
        int len = ReplayRecorder.FrameLength;
        for (int i = 0; i < chunk.FrameCount; i++)
        {
            if (frameCount++ > 0) frames.Append(',');
            frames.Append('[').Append(string.Join(",", chunk.Frames.Skip(i * len).Take(len))).Append(']');
        }
        var ev = chunk.EventsJson.Substring(1, chunk.EventsJson.Length - 2);
        if (ev.Length > 0) { if (eventCount++ > 0) events.Append(','); events.Append(ev); }
        for (int i = 0; i < chunk.Stats.Length; i += ReplayRecorder.StatsLength)
        {
            if (stats.Length > 0) stats.Append(',');
            stats.Append('[').Append(string.Join(",", chunk.Stats.Skip(i).Take(ReplayRecorder.StatsLength))).Append(']');
        }
    }
    string json = "{" + rec.Header(true) + ",\"frames\":[" + frames + "],\"events\":[" + events + "],\"stats\":[" + stats + "]}";
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    File.WriteAllText(path, json);
    Console.WriteLine($"Replay written: {path}  frames={frameCount}  score {engine.Home.Score}-{engine.Away.Score}  size={new FileInfo(path).Length / 1024} KB");
}

// The fingerprint: a fixed set of matches (leagues, the staged match as the page runs it, rain, snow and wind, cup and league cup
// with shoot-outs, random plans, computer managers) played through the replay recorder; everything it records - every frame,
// event and statistics row - goes into one hash per match, and the hashes into one. A refactoring that keeps it is a refactoring.
static void Golden()
{
    var sb = new StringBuilder();
    var all = System.Security.Cryptography.SHA256.Create();
    var play = System.Security.Cryptography.SHA256.Create();
    void Run(string name, ulong seed, Action<MatchConfig> config, Func<MatchEngine, (Action update, Action done)> managers = null, Action<TeamSetup, TeamSetup> teams = null)
    {
        var cfg = MatchConfig.CreateDefault();
        config?.Invoke(cfg);
        ulong names = seed * 2654435761UL + 17UL;
        var home = DemoTeams.Red(names); var away = DemoTeams.Blue(names);
        teams?.Invoke(home, away);
        var e = new MatchEngine(home, away, seed, cfg);
        var rec = new ReplayRecorder(e, 10);
        var update = managers?.Invoke(e).update;
        var h = System.Security.Cryptography.SHA256.Create();
        var hp = System.Security.Cryptography.SHA256.Create(); // the same without the headers: how the match played
        void Feed(byte[] b, bool play = false) { h.TransformBlock(b, 0, b.Length, null, 0); if (play) hp.TransformBlock(b, 0, b.Length, null, 0); }
        Feed(Encoding.UTF8.GetBytes(rec.Header(false)));
        int guard = 0;
        while (!e.IsFinished && guard++ < 100000)
        {
            var chunk = rec.Step(30);
            update?.Invoke();
            var fb = new byte[chunk.Frames.Length * 4]; Buffer.BlockCopy(chunk.Frames, 0, fb, 0, fb.Length); Feed(fb, true);
            Feed(Encoding.UTF8.GetBytes(chunk.EventsJson), true);
            var sbts = new byte[chunk.Stats.Length * 4]; Buffer.BlockCopy(chunk.Stats, 0, sbts, 0, sbts.Length); Feed(sbts, true);
        }
        Feed(Encoding.UTF8.GetBytes(rec.Header(true)));
        h.TransformFinalBlock(new byte[0], 0, 0); hp.TransformFinalBlock(new byte[0], 0, 0);
        string hex = Convert.ToHexString(h.Hash).Substring(0, 16);
        var pb = Encoding.UTF8.GetBytes(Convert.ToHexString(hp.Hash).Substring(0, 16)); play.TransformBlock(pb, 0, pb.Length, null, 0);
        sb.AppendLine($"{name,-22} {e.Home.Score}-{e.Away.Score} {hex}");
        var hb = Encoding.UTF8.GetBytes(hex); all.TransformBlock(hb, 0, hb.Length, null, 0);
    }
    for (ulong s = 1; s <= 6; s++) { ulong seed = s; Run("league " + seed, seed, null); }
    Run("staged page 7274", 7274, c => { c.Script = MatchScript.Staged(); c.WalkOut = true; });
    Run("rain wind 5", 5, c => c.Conditions = new MatchConditions { Weather = Weather.Rain, Maintenance = 0f, WindSpeed = 12f, WindAngle = 1f });
    Run("snow 77", 77, c => c.Conditions = new MatchConditions { Weather = Weather.Snow, Maintenance = 0.5f });
    Run("night 12", 12, c => c.Conditions = new MatchConditions { Night = true });
    for (ulong s = 1; s <= 4; s++) { ulong seed = s; Run("cup " + seed, seed, c => c.Competition = Competition.Cup); }
    for (ulong s = 1; s <= 4; s++) { ulong seed = s; Run("league cup " + seed, seed, c => c.Competition = Competition.LeagueCup); }
    Run("random plans 31", 31, null, null, (h, a) => { TeamPlan.Random(h, 31 * 7 + 1).ApplyBeforeKickOff(h); TeamPlan.Random(a, 31 * 7 + 2).ApplyBeforeKickOff(a); });
    Run("managers 17", 17, null, e => { var m0 = new ComputerManager(e, 0); var m1 = new ComputerManager(e, 1); return (() => { m0.Update(); m1.Update(); }, null); });
    all.TransformFinalBlock(new byte[0], 0, 0);
    Console.Write(sb.ToString());
    play.TransformFinalBlock(new byte[0], 0, 0);
    Console.WriteLine("ALL " + Convert.ToHexString(all.Hash).Substring(0, 16));
    Console.WriteLine("PLAY " + Convert.ToHexString(play.Hash).Substring(0, 16) + "   (without the headers: frames, events and statistics only)");
}

sealed class MatchResult
{
    /// <summary>Goals by the position kind of the scorer (own goals left out): GK, CB, FB, DM, CM, AM, W, ST.</summary>
    static int[] ShotKinds(MatchEngine e)
    {
        var k = new int[8];
        foreach (var ev in e.EventLog)
            if (ev.Type == MatchEventType.Shot && ev.Player >= 0) k[(int)e.AllPlayers[ev.Player].Kind]++;
        return k;
    }

    static int[] GoalKinds(MatchEngine e)
    {
        var k = new int[8];
        foreach (var ev in e.EventLog)
        {
            if (ev.Type != MatchEventType.Goal || ev.Value > 0.5f || ev.Player < 0) continue;
            var p = e.AllPlayers[ev.Player];
            k[(int)p.Kind]++;
        }
        return k;
    }

    public bool Finished;
    public int RecoveredErrors;
    public double WallMs;
    public int Headers, Volleys, CutIns, FreeKickShots, HeaderGoals, VolleyGoals, FreeKickGoals, Woodwork, Catches, Parries, Punches, Sorties, Goals, Shots, ShotsOnTarget, ShotsBlocked, Corners, Fouls, Yellows, Reds, Offsides, ThrowIns, GoalKicks, FreeKicks, Penalties, PenaltiesScored, Saves, Passes, Tackles, StandingTackles, SlideTackles, SlideTacklesWon, TacklesWon, HomeSwitches, AwaySwitches, HomeGoals, AwayGoals, HomeShots, AwayShots, LightInjuries, SeriousInjuries, Substitutions, Switches, SwitchesCompleted, FullBackCrosses, WingerCrosses, FullBackShots, WingerShots, CutBacks, CrossesAll, Interceptions, AnticipatedInterceptions, ThroughBalls, ThroughBallsCompleted, PassesIntoSpace, ShieldedOut, ShoulderDuelsWon, OneTwos, OneTwosCompleted, ThirdManRuns, ThirdManPasses, ThirdManCompleted, CounterMovements, CounterPresses, CounterPressWins, Overloads, LongThrows, HomePasses, HomeLongBalls, HomeFouls, HomeYellows, AwayFouls, AwayYellows, HomePens, HomePensScored, AwayPens, AwayPensScored, Knuckles;
    public int[] GoalsByKind = new int[8], ShotsByKind = new int[8];
    public double HomeLineDepth, HomeSpread, RealMinutes, HomeMidDepth, HomeAttackDepth, HomeCrosses, HomeLongShots, HomeEnergy, AwayOffsides, HomeThrough;
    public double Xg, PassPct, HomePossession, Added1, Added2, Real1, Real2, RealHt, FinalClock, DistanceKm, AvgEnergy, FoulsAnchor, FoulsCm, FoulsCb, FoulsFb, FoulsWide, FoulsSt, TacklesAnchor, TacklesCm, TacklesCb, TacklesFb, TacklesWide, TacklesSt, IntAnchor, IntCm, IntCb, IntFb, IntWide, IntSt, PassAnchor, PassCm, PassCb, PassFb, PassWide, PassSt, EnergyFullBack, EnergyCentreBack, EnergyWide, EnergyCentral, EnergyStriker, EnergyKeeper;
    public string Score;

    public static MatchResult From(MatchEngine e, double ms)
    {
        var h = e.Home.Stats;
        var a = e.Away.Stats;
        var added = e.EventLog.Where(x => x.Type == MatchEventType.AddedTime).Select(x => (double)x.Value).ToList();
        int passes = h.Passes + a.Passes;
        float poss = h.PossessionSeconds + a.PossessionSeconds;
        return new MatchResult
        {
            Finished = e.IsFinished,
            RecoveredErrors = e.RecoveredErrors,
            WallMs = ms,
            Goals = e.Home.Score + e.Away.Score,
            Shots = h.Shots + a.Shots,
            ShotsOnTarget = h.ShotsOnTarget + a.ShotsOnTarget,
            ShotsBlocked = h.ShotsBlocked + a.ShotsBlocked,
            Corners = h.Corners + a.Corners,
            Fouls = h.Fouls + a.Fouls,
            Yellows = h.YellowCards + a.YellowCards,
            Reds = h.RedCards + a.RedCards,
            Offsides = h.Offsides + a.Offsides,
            ThrowIns = h.ThrowIns + a.ThrowIns,
            GoalKicks = h.GoalKicks + a.GoalKicks,
            FreeKicks = h.FreeKicks + a.FreeKicks,
            Penalties = h.Penalties + a.Penalties,
            PenaltiesScored = h.PenaltiesScored + a.PenaltiesScored,
            Saves = h.Saves + a.Saves,
            Sorties = h.Sorties + a.Sorties,
            Headers = h.Headers + a.Headers,
            Volleys = h.Volleys + a.Volleys,
            FreeKickShots = h.FreeKickShots + a.FreeKickShots,
            HeaderGoals = h.HeaderGoals + a.HeaderGoals,
            VolleyGoals = h.VolleyGoals + a.VolleyGoals,
            FreeKickGoals = h.FreeKickGoals + a.FreeKickGoals,
            Woodwork = h.WoodworkHits + a.WoodworkHits,
            Catches = h.KeeperCatches + a.KeeperCatches,
            Parries = h.KeeperParries + a.KeeperParries,
            Punches = h.KeeperPunches + a.KeeperPunches,
            CutIns = h.CutInShots + a.CutInShots,
            Passes = passes,
            Tackles = h.Tackles + a.Tackles,
            StandingTackles = h.StandingTackles + a.StandingTackles,
            SlideTackles = h.SlideTackles + a.SlideTackles,
            SlideTacklesWon = h.SlideTacklesWon + a.SlideTacklesWon,
            TacklesWon = h.TacklesWon + a.TacklesWon,
            HomeSwitches = h.MarkingSwitches,
            HomeGoals = h.Goals,
            LightInjuries = h.LightInjuries + a.LightInjuries,
            SeriousInjuries = h.SeriousInjuries + a.SeriousInjuries,
            Substitutions = h.Substitutions + a.Substitutions,
            Switches = h.Switches + a.Switches,
            OneTwos = h.OneTwos + a.OneTwos,
            OneTwosCompleted = h.OneTwosCompleted + a.OneTwosCompleted,
            ThirdManRuns = h.ThirdManRuns + a.ThirdManRuns,
            ThirdManPasses = h.ThirdManPasses + a.ThirdManPasses,
            ThirdManCompleted = h.ThirdManCompleted + a.ThirdManCompleted,
            CounterMovements = h.CounterMovements + a.CounterMovements,
            CounterPresses = h.CounterPresses + a.CounterPresses,
            CounterPressWins = h.CounterPressWins + a.CounterPressWins,
            Overloads = h.Overloads + a.Overloads,
            LongThrows = h.LongThrows + a.LongThrows,
            HomePasses = h.Passes,
            AwayFouls = a.Fouls, AwayYellows = a.YellowCards,
            HomePens = h.Penalties, HomePensScored = h.PenaltiesScored, AwayPens = a.Penalties, AwayPensScored = a.PenaltiesScored,
            Knuckles = h.Knuckleballs + a.Knuckleballs,
            GoalsByKind = GoalKinds(e),
            ShotsByKind = ShotKinds(e),
            HomeLongBalls = e.EventLog.Count(x => x.Type == MatchEventType.LongBall && x.Team == 0),
            HomeFouls = h.Fouls,
            HomeYellows = h.YellowCards,
            HomeLineDepth = e.HomeLineSamples > 0 ? e.HomeLineSum / e.HomeLineSamples : 0,
            HomeSpread = e.HomeLineSamples > 0 ? e.HomeSpreadSum / e.HomeLineSamples : 0,
            HomeMidDepth = e.HomeLineSamples > 0 ? e.HomeMidSum / e.HomeLineSamples : 0,
            HomeAttackDepth = e.HomeLineSamples > 0 ? e.HomeAttackSum / e.HomeLineSamples : 0,
            RealMinutes = e.SimTime / e.Config.MotionSpeed / 60.0,
            HomeCrosses = h.Crosses,
            HomeLongShots = e.EventLog.Count(x => x.Type == MatchEventType.Shot && x.Team == 0 && MathF.Min(MathF.Sqrt((52.5f - x.X) * (52.5f - x.X) + x.Y * x.Y), MathF.Sqrt((52.5f + x.X) * (52.5f + x.X) + x.Y * x.Y)) > 18f),
            HomeEnergy = e.Home.Players.Where(p => !p.IsGoalkeeper).Average(p => p.Energy) * 100.0,
            AwayOffsides = a.Offsides,
            HomeThrough = h.ThroughBalls,
            SwitchesCompleted = h.SwitchesCompleted + a.SwitchesCompleted,
            FullBackCrosses = h.FullBackCrosses + a.FullBackCrosses,
            WingerCrosses = h.WingerCrosses + a.WingerCrosses,
            FullBackShots = h.FullBackShots + a.FullBackShots,
            WingerShots = h.WingerShots + a.WingerShots,
            CutBacks = h.CutBacks + a.CutBacks,
            CrossesAll = h.Crosses + a.Crosses,
            Interceptions = h.Interceptions + a.Interceptions,
            AnticipatedInterceptions = h.AnticipatedInterceptions + a.AnticipatedInterceptions,
            ThroughBalls = h.ThroughBalls + a.ThroughBalls,
            ThroughBallsCompleted = h.ThroughBallsCompleted + a.ThroughBallsCompleted,
            PassesIntoSpace = h.PassesIntoSpace + a.PassesIntoSpace,
            ShieldedOut = h.ShieldedOut + a.ShieldedOut,
            ShoulderDuelsWon = h.ShoulderDuelsWon + a.ShoulderDuelsWon,
            AwayGoals = a.Goals,
            HomeShots = h.Shots,
            AwayShots = a.Shots,
            AwaySwitches = a.MarkingSwitches,
            Xg = h.ExpectedGoals + a.ExpectedGoals,
            PassPct = passes > 0 ? 100.0 * (h.PassesCompleted + a.PassesCompleted) / passes : 0,
            HomePossession = poss > 0 ? 100.0 * h.PossessionSeconds / poss : 50,
            Added1 = added.Count > 0 ? added[0] : -1,
            Added2 = added.Count > 1 ? added[1] : -1,
            Real1 = e.FirstHalfRealSeconds,
            Real2 = e.SecondHalfRealSeconds,
            RealHt = e.HalftimeRealSeconds,
            FinalClock = e.Clock.Seconds,
            DistanceKm = (h.DistanceCoveredKm + a.DistanceCoveredKm) / 2,
            AvgEnergy = e.AllPlayers.Average(p => p.Energy),
            FoulsAnchor = RoleStats.Of(e, 0, p => p.FoulsCommitted), FoulsCm = RoleStats.Of(e, 1, p => p.FoulsCommitted), FoulsCb = RoleStats.Of(e, 2, p => p.FoulsCommitted),
            FoulsFb = RoleStats.Of(e, 3, p => p.FoulsCommitted), FoulsWide = RoleStats.Of(e, 4, p => p.FoulsCommitted), FoulsSt = RoleStats.Of(e, 5, p => p.FoulsCommitted),
            TacklesAnchor = RoleStats.Of(e, 0, p => p.Tackles), TacklesCm = RoleStats.Of(e, 1, p => p.Tackles), TacklesCb = RoleStats.Of(e, 2, p => p.Tackles),
            TacklesFb = RoleStats.Of(e, 3, p => p.Tackles), TacklesWide = RoleStats.Of(e, 4, p => p.Tackles), TacklesSt = RoleStats.Of(e, 5, p => p.Tackles),
            IntAnchor = RoleStats.Of(e, 0, p => p.Interceptions), IntCm = RoleStats.Of(e, 1, p => p.Interceptions), IntCb = RoleStats.Of(e, 2, p => p.Interceptions),
            IntFb = RoleStats.Of(e, 3, p => p.Interceptions), IntWide = RoleStats.Of(e, 4, p => p.Interceptions), IntSt = RoleStats.Of(e, 5, p => p.Interceptions),
            PassAnchor = RoleStats.Of(e, 0, p => p.PassesAttempted), PassCm = RoleStats.Of(e, 1, p => p.PassesAttempted), PassCb = RoleStats.Of(e, 2, p => p.PassesAttempted),
            PassFb = RoleStats.Of(e, 3, p => p.PassesAttempted), PassWide = RoleStats.Of(e, 4, p => p.PassesAttempted), PassSt = RoleStats.Of(e, 5, p => p.PassesAttempted),
            EnergyFullBack = e.AllPlayers.Where(p => !p.IsGoalkeeper && p.Slot.IsFullBack).Average(p => p.Energy),
            EnergyCentreBack = e.AllPlayers.Where(p => !p.IsGoalkeeper && p.Slot.IsCentreBack).Average(p => p.Energy),
            EnergyWide = e.AllPlayers.Where(p => p.Slot.IsWideMidfielder).Average(p => p.Energy),
            EnergyCentral = e.AllPlayers.Where(p => p.Slot.IsCentralMidfielder).Average(p => p.Energy),
            EnergyStriker = e.AllPlayers.Where(p => p.Slot.Line == Line.Attack).Average(p => p.Energy),
            EnergyKeeper = e.AllPlayers.Where(p => p.IsGoalkeeper).Average(p => p.Energy),
            Score = $"{e.Home.Score}-{e.Away.Score}",
        };
    }
}

static class RoleStats
{
    /// <summary>Average of a per-player count over one role: 0 holding midfielder, 1 other central midfielder, 2 centre-back,
    /// 3 full-back, 4 wide midfielder, 5 striker (both teams).</summary>
    public static double Of(MatchEngine e, int role, Func<PlayerAgent, int> f)
    {
        var players = e.AllPlayers.Where(p => !p.IsGoalkeeper && RoleOf(p) == role).ToList();
        return players.Count == 0 ? 0 : players.Average(p => (double)f(p));

        static int RoleOf(PlayerAgent p)
        {
            var s = p.Slot;
            if (s.IsCentralMidfielder) return p == p.Team.Anchor ? 0 : 1;
            if (s.IsCentreBack) return 2;
            if (s.IsFullBack) return 3;
            if (s.IsWideMidfielder) return 4;
            return 5;
        }
    }
}

