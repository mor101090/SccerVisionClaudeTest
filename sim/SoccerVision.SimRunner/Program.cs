using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
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
    }
}

MatchConfig MakeConfig()
{
    var c = MatchConfig.CreateDefault();
    if (motion.HasValue) c.MotionSpeed = motion.Value;
    return c;
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
    var engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed + (ulong)i, MakeConfig());
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
        var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed + (ulong)i, MakeConfig());
        e.RunToEnd(0.25f);
        var log = e.EventLog;
        Console.WriteLine($"seed {seed + (ulong)i,6}  {e.Home.Score}-{e.Away.Score}  pens {log.Count(x => x.Type == MatchEventType.PenaltyAwarded)}"
            + $"  fkShots {log.Count(x => x.Type == MatchEventType.Shot && x.Detail == 3)}  wallFks {log.Count(x => x.Type == MatchEventType.FreeKick && x.Detail == 1)}"
            + $"  cards {log.Count(x => x.Type == MatchEventType.YellowCard)}/{log.Count(x => x.Type == MatchEventType.RedCard)}  woodwork {log.Count(x => x.Type == MatchEventType.HitWoodwork)}");
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
    Row("Shots", Avg(x => x.Shots), "24-27");
    Row("Shots on target", Avg(x => x.ShotsOnTarget), "8-9");
    Row("Shots blocked", Avg(x => x.ShotsBlocked), "6-7");
    Row("Corners", Avg(x => x.Corners), "9.3-10.7");
    Row("Fouls", Avg(x => x.Fouls), "20-24");
    Row("Yellow cards", Avg(x => x.Yellows), "3.5-4.3 (PL 24/25: 4.19)");
    Row("Red cards", Avg(x => x.Reds), "0.10-0.15");
    Row("Offsides", Avg(x => x.Offsides), "3.5-4.5");
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
    Row("Cut-inside shots", Avg(x => x.CutIns), "n/a");
    Row("Passes", Avg(x => x.Passes), "850-1000 (in 57 min ball-in-play)");
    Row("Pass completion %", Avg(x => x.PassPct), "80-85");
    Row("Tackles", Avg(x => x.Tackles), "30-40");
    Row("Home possession %", Avg(x => x.HomePossession), "50 +/- 15");
    Row("Added time 1st half (min)", Avg(x => x.Added1), "1-3");
    Row("Added time 2nd half (min)", Avg(x => x.Added2), "3-7");
    Row("Real 1st half (s)", Avg(x => x.Real1), "600 + added");
    Row("Real half-time break (s)", Avg(x => x.RealHt), "120");
    Row("Real 2nd half (s)", Avg(x => x.Real2), "600 + added");
    Row("Final clock (min)", Avg(x => x.FinalClock / 60.0), ">= 90");
    Row("Distance per team (km)", Avg(x => x.DistanceKm), "105-115");
    Row("Avg final energy", Avg(x => x.AvgEnergy), "fatigue model");
    Console.WriteLine();
    var hist = r.GroupBy(x => x.Goals).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}");
    Console.WriteLine("Goals distribution: " + string.Join("  ", hist));
    Console.WriteLine("Scores: " + string.Join(" ", r.Take(30).Select(x => x.Score)));
}

static void ExportReplay(string path, ulong seed, MatchConfig config, int fps)
{
    var engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, config);
    var frames = new List<int[]>();
    var events = new List<object>();
    float dt = 1f / fps;
    int guard = 0;
    while (!engine.IsFinished && guard++ < 200000)
    {
        engine.Advance(dt);
        foreach (var e in engine.DrainEvents())
        {
            if (e.Type == MatchEventType.NetHit) continue;
            events.Add(new
            {
                t = Math.Round(engine.RealTimeElapsed, 2),
                minute = e.Minute,
                type = e.Type.ToString(),
                text = e.Text,
                team = e.Team,
                whistle = e.Type == MatchEventType.Whistle ? e.Whistle.ToString() : null,
                tone = Commentary.ToneOf(e.Type).ToString(),
                detail = e.Detail,
            });
        }
        if (engine.Phase == MatchPhase.HalfTimeBreak) continue;
        var f = new List<int>(90)
        {
            (int)MathF.Round(engine.RealTimeElapsed * 100f),
            (int)engine.Clock.Seconds,
            (int)engine.Phase,
            engine.Home.Score,
            engine.Away.Score,
            Q(engine.Ball.Pos.X), Q(engine.Ball.Pos.Y), (int)MathF.Round(engine.Ball.Z * 100f),
            engine.Ball.Owner?.Index ?? -1,
        };
        foreach (var p in engine.AllPlayers)
        {
            f.Add(Q(p.Pos.X));
            f.Add(Q(p.Pos.Y));
            f.Add((int)MathF.Round(MathUtil.WrapAngle(p.Facing) * 100f));
            f.Add((int)MathF.Round(p.JumpZ * 100f));
            f.Add((p.SentOff ? 1 : 0) | (p.IsGoalkeeper ? 2 : 0) | (p.YellowCards > 0 ? 4 : 0));
        }
        foreach (var o in engine.Officials)
        {
            f.Add(Q(o.Pos.X));
            f.Add(Q(o.Pos.Y));
            f.Add(o.FlagRaised ? 1 : 0);
        }
        frames.Add(f.ToArray());
    }

    object Team(TeamAgent t) => new
    {
        name = t.Name,
        shortName = t.ShortName,
        kit = new { primary = t.Setup.Kit.Primary, secondary = t.Setup.Kit.Secondary, number = t.Setup.Kit.Number, gk = t.Setup.Kit.GoalkeeperPrimary, gkNumber = t.Setup.Kit.GoalkeeperNumber },
        players = t.Players.Select(p => new
        {
            index = p.Index,
            number = p.Number,
            name = p.Profile.FullName,
            shortName = p.Profile.ShortName,
            role = Formation.RoleAbbreviation(p.Slot.Role),
            passing = p.Attr.Passing,
            speed = p.Attr.Speed,
            stamina = p.Attr.Stamina,
        }),
        stats = t.Stats,
    };

    var replay = new
    {
        version = 1,
        seed,
        fps,
        scale = 10,
        halfRealSeconds = config.HalfRealSeconds,
        halftimeRealSeconds = config.HalftimeRealSeconds,
        home = Team(engine.Home),
        away = Team(engine.Away),
        frameLayout = "t*100, clockSec, phase, homeScore, awayScore, ballX*10, ballY*10, ballZ*100, ballOwner, then per player [x*10, y*10, facing*100, jumpZ*100, flags(1=sentOff,2=gk,4=booked)] x22, then officials [x*10, y*10, flag] x3",
        frames,
        events,
    };
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    File.WriteAllText(path, JsonSerializer.Serialize(replay, new JsonSerializerOptions { IncludeFields = true }));
    Console.WriteLine($"Replay written: {path}  frames={frames.Count}  events={events.Count}  score {engine.Home.Score}-{engine.Away.Score}  size={new FileInfo(path).Length / 1024} KB");

    static int Q(float v) => (int)MathF.Round(v * 10f);
}

sealed class MatchResult
{
    public bool Finished;
    public int RecoveredErrors;
    public double WallMs;
    public int Headers, Volleys, CutIns, FreeKickShots, Sorties, Goals, Shots, ShotsOnTarget, ShotsBlocked, Corners, Fouls, Yellows, Reds, Offsides, ThrowIns, GoalKicks, FreeKicks, Penalties, PenaltiesScored, Saves, Passes, Tackles;
    public double Xg, PassPct, HomePossession, Added1, Added2, Real1, Real2, RealHt, FinalClock, DistanceKm, AvgEnergy;
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
            CutIns = h.CutInShots + a.CutInShots,
            Passes = passes,
            Tackles = h.Tackles + a.Tackles,
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
            Score = $"{e.Home.Score}-{e.Away.Score}",
        };
    }
}
