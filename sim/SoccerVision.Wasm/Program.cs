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
    /// <param name="homeStrength">weak | medium | strong: how good the home squad is (medium: the squads the game always had).</param>
    /// <param name="awayStrength">the same for the away squad.</param>
    /// <param name="homePlan">Live only: your plan for the home team (see TeamPlan): formation, starting eleven, roles, instructions, takers, tactics. The staged match plays the squads as they are; in a random match both managers pick their own.</param>
    [JSExport]
    public static string Start(int seed, string mode, string weather, double maintenance, double wind, double windAngle, bool night, string homePlan, string competition, string homeStrength, string awayStrength)
    {
        var config = MatchConfig.CreateDefault();
        // league match, cup tie (level: penalties) or league cup tie (level: extra time, then penalties)
        if (!string.IsNullOrEmpty(competition) && Enum.TryParse<Competition>(competition, true, out var comp)) config.Competition = comp;
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
        // the players' names are drawn anew for every match (from the Israeli name database)
        ulong names = (ulong)seed * 2654435761UL + 17UL;
        // how good each team is: weak | medium | strong (anything else: medium, the squads the game always had)
        // a manager's own squad (see UseHomeTeam) plays at home instead of the red demo team, for this match only
        var home = _homeTeam != null ? ParseTeam(_homeTeam) : DemoTeams.Red(names, ParseStrength(homeStrength));
        _homeTeam = null;
        var away = DemoTeams.Blue(names, ParseStrength(awayStrength));
        if (mode == "random")
        {
            // a random match: both managers pick a random formation, eleven, roles and instructions
            TeamPlan.Random(home, seed * 7 + 1).ApplyBeforeKickOff(home);
            TeamPlan.Random(away, seed * 7 + 2).ApplyBeforeKickOff(away);
        }
        else if (mode == "live" || mode == "online") TeamPlan.Parse(homePlan).ApplyBeforeKickOff(home); // (only in a live match is the home team yours; online the away team is the friend's, see SetPlanFor)
        _engine = new MatchEngine(home, away, (ulong)seed, config);
        // live: you are the home team, the computer manages the away team and answers you
        _computer = mode == "live" || mode == "random" ? new ComputerManager(_engine, 1) : null;
        // random: both teams have a manager who changes the tactics during the match (live: the home team is yours)
        _homeComputer = mode == "random" ? new ComputerManager(_engine, 0) : null;
        _rec = new ReplayRecorder(_engine, 10);
        _last = null;
        return "{" + _rec.Header(false) + "}";
    }

    private static TeamStrength ParseStrength(string s) => Enum.TryParse<TeamStrength>(s, true, out var v) ? v : TeamStrength.Medium;

    private static string _homeTeam;

    /// <summary>
    /// The next <see cref="Start"/> fields this squad at home instead of the red demo team (a manager's own club, made on the
    /// website). Plain text, one line each, fields split by '|':
    /// line 1: name|shortName|manager|kitPrimary|kitSecondary|kitNumber|gkPrimary|gkNumber|gkSecondary|seed;
    /// then one line per player, the 11 starters first in 4-4-2 slot order (GK, RB, CB right, CB left, LB, RM, CM right,
    /// CM left, LM, ST right, ST left), then the bench:
    /// first|last|shirt|Goalkeeper/Defender/Midfielder/Forward|positionLabel|heightCm|stamina|Low/Normal/High|leftFoot|rightFoot (stars 1-5, the strong foot 5)|age|nationality|27 attributes
    /// (comma separated, in the order of GeneratedTeams.P). Null or empty: the demo team again.
    /// </summary>
    [JSExport]
    public static void UseHomeTeam(string team) => _homeTeam = string.IsNullOrEmpty(team) ? null : team;

    private static TeamSetup ParseTeam(string text)
    {
        var lines = text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var h = lines[0].Split('|');
        var t = new TeamSetup
        {
            Name = h[0],
            ShortName = h[1],
            Manager = h[2],
            Kit = new KitColors { Primary = h[3], Secondary = h[4], Number = h[5], GoalkeeperPrimary = h[6], GoalkeeperNumber = h[7], GoalkeeperSecondary = h[8] },
            Formation = Formation.F442(),
            Tactics = new TeamTactics(),
        };
        int seed = int.Parse(h[9], System.Globalization.CultureInfo.InvariantCulture);
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split('|');
            int N(int k) => int.Parse(f[k], System.Globalization.CultureInfo.InvariantCulture);
            var v = Array.ConvertAll(f[12].Split(','), s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            var a = new PlayerAttributes
            {
                Stamina = N(6), Fitness = Enum.Parse<FitnessLevel>(f[7]), LeftFoot = N(8), RightFoot = N(9),
                Aggression = v[0], Teamwork = v[1], Composure = v[2], Decisions = v[3],
                Tackling = v[4], Marking = v[5], Positioning = v[6],
                Creativity = v[7], Passing = v[8], Crossing = v[9],
                Finishing = v[10], LongShots = v[11], OffTheBall = v[12],
                PhysicalPower = v[13], BallControl = v[14], Dribbling = v[15], Acceleration = v[16], Speed = v[17], Jumping = v[18], Heading = v[19],
                FreeKicks = v[20], Penalty = v[21],
                AerialAbility = v[22], Handling = v[23], Kicking = v[24], OneOnOne = v[25], Reflexes = v[26],
            };
            var p = new PlayerProfile
            {
                Id = 9000 + i, // (apart from the demo squads' ids)
                FirstName = f[0], LastName = f[1], ShirtNumber = N(2), NaturalPosition = Enum.Parse<PositionGroup>(f[3]), Position = f[4],
                HeightCm = N(5), Age = N(10), Nationality = f[11], Attributes = a,
            };
            if (t.Starters.Count < 11) t.Starters.Add(p); else t.Bench.Add(p);
        }
        PlayerTraits.Assign(t, seed);
        PlayerAppearances.Assign(t, seed);
        return t;
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

    /// <summary>A manager's new plan for team 0 (home) or 1 (away - the friend in an online match).</summary>
    [JSExport]
    public static void SetPlanFor(int team, string plan)
    {
        if (_engine == null || team < 0 || team > 1) return;
        TeamPlan.Parse(plan).Submit(_engine, team);
    }

    /// <summary>A manager asks for a change for team 0 (home) or 1 (away - the friend in an online match).</summary>
    [JSExport]
    public static void SubstituteFor(int team, int outNumber, int inNumber)
    {
        if (team < 0 || team > 1) return;
        _engine?.Submit(new SubstituteCommand(team, outNumber, inNumber));
    }

    [JSExport]
    public static int FrameLength() => ReplayRecorder.FrameLength;
}
