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
    private static ComputerManager _computer;

    public static void Main() { }

    /// <summary>Starts a match; returns the replay header (teams, conditions, mud map...) as JSON object text.</summary>
    /// <param name="mode">staged (the scripted incidents) | random | live</param>
    [JSExport]
    public static string Start(int seed, string mode, string weather, double maintenance, double wind, double windAngle, bool night,
        double line, double width, int passing, double tempo, double aggression)
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
        var home = DemoTeams.Red();
        home.Tactics = MakeTactics(home.Tactics, line, width, passing, tempo, aggression);
        _engine = new MatchEngine(home, DemoTeams.Blue(), (ulong)seed, config);
        // live: you are the home team, the computer manages the away team and answers you
        _computer = mode == "live" ? new ComputerManager(_engine, 1) : null;
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
        return _last.Frames;
    }

    /// <summary>JSON array of the events of the last Step.</summary>
    [JSExport]
    public static string Events() => _last == null ? "[]" : _last.EventsJson;

    /// <summary>Stats rows of the last Step (15 numbers each).</summary>
    [JSExport]
    public static int[] Stats() => _last == null ? new int[0] : _last.Stats;

    [JSExport]
    public static bool Finished() => _engine != null && _engine.IsFinished;

    /// <summary>The header again, with each team's full-time statistics.</summary>
    [JSExport]
    public static string FinalHeader() => "{" + _rec.Header(true) + "}";

    /// <summary>The home team's new tactics, from the next step (live: the human manager's instructions).</summary>
    [JSExport]
    public static void SetTactics(double line, double width, int passing, double tempo, double aggression)
    {
        if (_engine == null) return;
        _engine.Submit(new ChangeTacticsCommand(0, MakeTactics(_engine.Home.Tactics, line, width, passing, tempo, aggression)));
    }

    /// <summary>The away team's tactics in words (what the computer is doing), for the page.</summary>
    [JSExport]
    public static string AwayTactics() => _engine == null ? "" : _engine.Away.Tactics.Describe();

    /// <summary>Line, width, tempo 0..1; passing 0 short, 1 mixed, 2 direct; aggression 0 gentle .. 1 aggressive.</summary>
    private static TeamTactics MakeTactics(TeamTactics from, double line, double width, int passing, double tempo, double aggression)
    {
        var t = from.Clone();
        t.DefensiveLine = (float)line;
        t.Width = (float)width;
        t.Passing = passing == 0 ? PassingStyle.Short : passing == 2 ? PassingStyle.Direct : PassingStyle.Mixed;
        t.Tempo = (float)tempo;
        t.PressingIntensity = (float)(0.5 + 0.6 * (aggression - 0.5));
        t.Tackling = aggression < 0.34 ? TacklingStyle.StayOnFeet : aggression > 0.66 ? TacklingStyle.GetStuckIn : TacklingStyle.Normal;
        return t;
    }

    [JSExport]
    public static int FrameLength() => ReplayRecorder.FrameLength;
}
