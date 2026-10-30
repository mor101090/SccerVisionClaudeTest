using System;
using System.Runtime.InteropServices.JavaScript;
using SoccerVision.Replay;
using SoccerVision.Simulation;

/// <summary>
/// The match engine as a browser module. JavaScript starts a match and then asks for frames, a batch at a time (10
/// frames = one real second of the match); what comes back is the same replay data the replay files hold.
/// </summary>
public static partial class LiveEngine
{
    private static MatchEngine _engine;
    private static ReplayRecorder _rec;
    private static ReplayChunk _last;
    private static ComputerManager _computer, _homeComputer;

    public static void Main() { }

    /// <summary>Starts a match; returns the replay header (teams, conditions, mud map...) as JSON object text.</summary>
    /// <param name="mode">staged (the scripted incidents) | random | live</param>
    /// <param name="homePlan">Live only: your plan for the home team (see TeamPlan): formation, starting eleven, roles, instructions, takers, tactics. The staged match plays the squads as they are; in a random match both managers pick their own.</param>
    [JSExport]
    public static string Start(int seed, string mode, string weather, double maintenance, double wind, double windAngle, bool night, string homePlan)
    {
        var config = MatchConfig.CreateDefault();
        config.Conditions = new MatchConditions
        {
            Weather = (Weather)Enum.Parse(typeof(Weather), weather, true),
            Maintenance = (float)maintenance,
            WindSpeed = (float)wind,
            WindAngle = (float)windAngle,
            Night = night,
        };
        if (mode == "staged") config.Script = MatchScript.Staged();
        config.WalkOut = true; // (the teams and the officials come out of the tunnel first)
        var home = DemoTeams.Red();
        var away = DemoTeams.Blue();
        if (mode == "random")
        {
            // a random match: both managers pick a random formation, eleven, roles and instructions
            TeamPlan.Random(home, seed * 7 + 1).ApplyBeforeKickOff(home);
            TeamPlan.Random(away, seed * 7 + 2).ApplyBeforeKickOff(away);
        }
        else if (mode == "live") TeamPlan.Parse(homePlan).ApplyBeforeKickOff(home); // (only in a live match is the home team yours)
        _engine = new MatchEngine(home, away, (ulong)seed, config);
        // live: you are the home team, the computer manages the away team and answers you
        _computer = mode == "live" || mode == "random" ? new ComputerManager(_engine, 1) : null;
        // random: both teams have a manager who changes the tactics during the match (live: the home team is yours)
        _homeComputer = mode == "random" ? new ComputerManager(_engine, 0) : null;
        _rec = new ReplayRecorder(_engine, 10);
        _last = null;
        return "{" + _rec.Header(false) + "}";
    }

    /// <summary>Plays up to <paramref name="frames"/> frames; returns them (FrameLength numbers each). Events and stats: see below.</summary>
    [JSExport]
    public static int[] Step(int frames)
    {
        _last = _rec.Step(frames);
        _computer?.Update();
        _homeComputer?.Update();
        return _last.Frames;
    }

    /// <summary>JSON array of the events of the last Step.</summary>
    [JSExport]
    public static string Events() => _last == null ? "[]" : _last.EventsJson;

    /// <summary>Stats rows of the last Step (ReplayRecorder.StatsLength numbers each: time, team stats, player energy).</summary>
    [JSExport]
    public static int[] Stats() => _last == null ? new int[0] : _last.Stats;

    [JSExport]
    public static bool Finished() => _engine != null && _engine.IsFinished;

    /// <summary>The header again, with each team's full-time statistics.</summary>
    [JSExport]
    public static string FinalHeader() => "{" + _rec.Header(true) + "}";

    /// <summary>The home manager's new plan during the match (tactics, formation, positions, roles, instructions, takers).</summary>
    [JSExport]
    public static void SetPlan(string plan)
    {
        if (_engine == null) return;
        TeamPlan.Parse(plan).Submit(_engine, 0);
    }

    /// <summary>The home manager asks for a change: shirt <paramref name="outNumber"/> off, bench player <paramref name="inNumber"/> on (at the next stoppage).</summary>
    [JSExport]
    public static void Substitute(int outNumber, int inNumber)
    {
        _engine?.Submit(new SubstituteCommand(0, outNumber, inNumber));
    }

    [JSExport]
    public static int FrameLength() => ReplayRecorder.FrameLength;
}
