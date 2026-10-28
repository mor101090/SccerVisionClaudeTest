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

    public static void Main() { }

    /// <summary>Starts a match; returns the replay header (teams, conditions, mud map...) as JSON object text.</summary>
    /// <param name="mode">staged (the scripted incidents) | random | live</param>
    [JSExport]
    public static string Start(int seed, string mode, string weather, double maintenance, double wind, double windAngle, bool night)
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
        _engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), (ulong)seed, config);
        _rec = new ReplayRecorder(_engine, 10);
        _last = null;
        return "{" + _rec.Header(false) + "}";
    }

    /// <summary>Plays up to <paramref name="frames"/> frames; returns them (FrameLength numbers each). Events and stats: see below.</summary>
    [JSExport]
    public static int[] Step(int frames)
    {
        _last = _rec.Step(frames);
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

    [JSExport]
    public static int FrameLength() => ReplayRecorder.FrameLength;
}
