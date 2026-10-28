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
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

int matches = 20;
ulong seed = 1;
bool timeline = false;
string replayPath = null;
float? motion = null;
int replayFps = 8;
var conditions = new MatchConditions();
MatchScript script = null;
// --home "line=0.9,width=0.2,passing=short,tempo=0.8,aggression=0.8": the home team's tactics
string homeTactics = null, awayTactics = null;
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
        case "--home": homeTactics = args[++i]; break;
        case "--away": awayTactics = args[++i]; break;
        case "--script": if (args[++i] == "staged") script = MatchScript.Staged(); break;
    }
}

MatchConfig MakeConfig()
{
    var c = MatchConfig.CreateDefault();
    if (motion.HasValue) c.MotionSpeed = motion.Value;
    c.Conditions = conditions.Clone();
    c.Script = script;
    return c;
}

TeamSetup Home() => WithTactics(DemoTeams.Red(), homeTactics);
TeamSetup Away() => WithTactics(DemoTeams.Blue(), awayTactics);

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
            case "aggression":
                float a = float.Parse(v);
                t.Tactics.PressingIntensity = 0.5f + 0.6f * (a - 0.5f);
                t.Tactics.Tackling = a < 0.34f ? TacklingStyle.StayOnFeet : a > 0.66f ? TacklingStyle.GetStuckIn : TacklingStyle.Normal;
                break;
        }
    }
    return t;
}

if (args.Contains("--shotorigins"))
{
    var origins = new Dictionary<string, int>();
    int total = 0;
    for (int m = 0; m < matches; m++)
    {
        var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed + (ulong)m, MakeConfig());
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
    var engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, MakeConfig());
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
    Console.WriteLine("Per match (both teams)        sim     real-world reference (top leagues)");
    void Row(string name, double v, string reference) => Console.WriteLine($"  {name,-26}{v,7:0.00}   {reference}");
    Row("Goals", Avg(x => x.Goals), "2.7-3.0 (PL 24/25: 2.97)");
    Row("xG", Avg(x => x.Xg), "~ goals");
    Row("Shots", Avg(x => x.Shots), "PL 24/25: 25.9");
    Row("Shots on target", Avg(x => x.ShotsOnTarget), "8-9");
    Row("Shots blocked", Avg(x => x.ShotsBlocked), "6-7");
    Row("Corners", Avg(x => x.Corners), "PL 24/25: 10.2");
    Row("Fouls", Avg(x => x.Fouls), "PL 24/25: 22.1");
    Row("Yellow cards", Avg(x => x.Yellows), "3.5-4.3 (PL 24/25: 4.19)");
    Row("Red cards", Avg(x => x.Reds), "0.10-0.15");
    Row("Offsides", Avg(x => x.Offsides), "PL 24/25: 3.38");
    Row("Throw-ins", Avg(x => x.ThrowIns), "39-43");
    Row("Goal kicks", Avg(x => x.GoalKicks), "14-18");
    Row("Free kicks", Avg(x => x.FreeKicks), "~ fouls + offsides");
    Row("Penalties", Avg(x => x.Penalties), "0.25-0.35");
    Row("Penalty conversion %", r.Sum(x => x.Penalties) > 0 ? 100.0 * r.Sum(x => x.PenaltiesScored) / r.Sum(x => x.Penalties) : 0, "75-80");
    Row("Saves", Avg(x => x.Saves), "5-6");
    Row("Keeper sorties", Avg(x => x.Sorties), "n/a");
    Row("Headed shots", Avg(x => x.Headers), "n/a");
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
    Row("One-twos (completed)", Avg(x => x.OneTwos), Avg(x => x.OneTwosCompleted).ToString("0.00"));
    Row("Home: passes / long balls", Avg(x => x.HomePasses), Avg(x => x.HomeLongBalls).ToString("0.0"));
    Row("Home: possession % / goals", Avg(x => x.HomePossession), Avg(x => x.HomeGoals).ToString("0.00") + " (away " + Avg(x => x.AwayGoals).ToString("0.00") + ")");
    Row("Home: fouls / yellows", Avg(x => x.HomeFouls), Avg(x => x.HomeYellows).ToString("0.00"));
    Row("Home: avg back-line depth (m from own goal)", Avg(x => x.HomeLineDepth), "width of outfield spread " + Avg(x => x.HomeSpread).ToString("0.0") + " m");
    Row("Home: midfield / forwards depth (m)", Avg(x => x.HomeMidDepth), Avg(x => x.HomeAttackDepth).ToString("0.0"));
    Row("Real minutes per match", Avg(x => x.RealMinutes), "");
    Row("Crosses by full-backs", Avg(x => x.FullBackCrosses), "~35-45% of crosses");
    Row("Crosses by wingers", Avg(x => x.WingerCrosses), "~35-45% of crosses");
    Row("Shots by full-backs", Avg(x => x.FullBackShots), "~1-2");
    Row("Shots by wingers", Avg(x => x.WingerShots), "~5-7");
    Row("Cut-backs from the by-line", Avg(x => x.CutBacks), "n/a");
    Row("Crosses (all)", Avg(x => x.CrossesAll), "~18-22 (open play)");
    Row("Injuries: light (played on)", Avg(x => x.LightInjuries), "~0.5-0.8 (time-loss ~0.7-1 per match)");
    Row("Injuries: serious (had to go off)", Avg(x => x.SeriousInjuries), "~0.2-0.4");
    Row("Substitutions for injury", Avg(x => x.Substitutions), "~0.2-0.4");
    Row("Goals home / away", Avg(x => x.HomeGoals), Avg(x => x.AwayGoals).ToString("0.00"));
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

sealed class MatchResult
{
    public bool Finished;
    public int RecoveredErrors;
    public double WallMs;
    public int Headers, Volleys, CutIns, FreeKickShots, HeaderGoals, VolleyGoals, FreeKickGoals, Woodwork, Catches, Parries, Punches, Sorties, Goals, Shots, ShotsOnTarget, ShotsBlocked, Corners, Fouls, Yellows, Reds, Offsides, ThrowIns, GoalKicks, FreeKicks, Penalties, PenaltiesScored, Saves, Passes, Tackles, StandingTackles, SlideTackles, SlideTacklesWon, TacklesWon, HomeSwitches, AwaySwitches, HomeGoals, AwayGoals, HomeShots, AwayShots, LightInjuries, SeriousInjuries, Substitutions, Switches, SwitchesCompleted, FullBackCrosses, WingerCrosses, FullBackShots, WingerShots, CutBacks, CrossesAll, Interceptions, AnticipatedInterceptions, ThroughBalls, ThroughBallsCompleted, PassesIntoSpace, ShieldedOut, ShoulderDuelsWon, OneTwos, OneTwosCompleted, HomePasses, HomeLongBalls, HomeFouls, HomeYellows;
    public double HomeLineDepth, HomeSpread, RealMinutes, HomeMidDepth, HomeAttackDepth;
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
            HomePasses = h.Passes,
            HomeLongBalls = e.EventLog.Count(x => x.Type == MatchEventType.LongBall && x.Team == 0),
            HomeFouls = h.Fouls,
            HomeYellows = h.YellowCards,
            HomeLineDepth = e.HomeLineSamples > 0 ? e.HomeLineSum / e.HomeLineSamples : 0,
            HomeSpread = e.HomeLineSamples > 0 ? e.HomeSpreadSum / e.HomeLineSamples : 0,
            HomeMidDepth = e.HomeLineSamples > 0 ? e.HomeMidSum / e.HomeLineSamples : 0,
            HomeAttackDepth = e.HomeLineSamples > 0 ? e.HomeAttackSum / e.HomeLineSamples : 0,
            RealMinutes = e.SimTime / e.Config.MotionSpeed / 60.0,
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
