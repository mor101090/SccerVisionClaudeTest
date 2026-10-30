using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SoccerVision.Simulation;

namespace SoccerVision.Replay
{
    /// <summary>One batch of recorded frames.</summary>
    public sealed class ReplayChunk
    {
        /// <summary>Frames, one after the other (<see cref="ReplayRecorder.FrameLength"/> numbers each).</summary>
        public int[] Frames;
        /// <summary>JSON array of the events of this batch.</summary>
        public string EventsJson;
        /// <summary>Team stats rows (<see cref="ReplayRecorder.StatsLength"/> numbers each).</summary>
        public int[] Stats;
        public int FrameCount;
        public bool Finished;
    }

    /// <summary>
    /// Records a running match as replay data. Each <see cref="Step"/> advances the engine in steps of 1/fps real
    /// seconds and writes one frame per step (none during the half-time break): the clock, phase, score, ball and the
    /// players and officials, in the compact integer form the viewers read. JSON is written by hand (no reflection), so
    /// it also works where code is trimmed (WebAssembly).
    /// </summary>
    public sealed class ReplayRecorder
    {
        /// <summary>Stats row: the time, 14 team stats, the energy left of each of the 22 players (0-100), then each one's rating x10 (10-100).</summary>
        public const int StatsLength = 15 + 22 + 22;
        private const int PlayerBase = 9;
        private const int PlayerStride = 5;
        public static readonly int FrameLength = PlayerBase + 22 * PlayerStride + 3 * 3;

        private readonly MatchEngine _engine;
        private readonly int _fps;
        private float _nextStats;
        private readonly List<int> _frames = new List<int>();
        private readonly List<int> _stats = new List<int>();
        private readonly StringBuilder _events = new StringBuilder();
        private int _eventCount;

        public ReplayRecorder(MatchEngine engine, int fps)
        {
            _engine = engine;
            _fps = fps;
        }

        public MatchEngine Engine => _engine;

        private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private static void Str(StringBuilder b, string s)
        {
            b.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else b.Append(c);
                        break;
                }
            }
            b.Append('"');
        }

        /// <summary>A player's card: height, feet, age, fitness and the skill categories (0-100) as the profile has them.</summary>
        private static void Profile(StringBuilder b, PlayerProfile pr)
        {
            var a = pr.Attributes;
            b.Append(",\"height\":").Append(pr.HeightCm).Append(",\"age\":").Append(pr.Age);
            b.Append(",\"foot\":\"").Append(a.LeftFoot == 3 && a.RightFoot == 3 ? "Both" : a.StrongFoot == Foot.Left ? "Left" : "Right").Append('"');
            b.Append(",\"weakFoot\":").Append(a.WeakFootRating);
            b.Append(",\"fitness\":\"").Append(a.Fitness == FitnessLevel.Low ? "Low" : a.Fitness == FitnessLevel.High ? "Peak" : "Moderate").Append('"');
            // every skill, in the order of "attrNames" in the header (the player profile page shows them all)
            b.Append(",\"attrs\":[").Append(a.Aggression).Append(',').Append(a.Teamwork).Append(',').Append(a.Composure).Append(',').Append(a.Decisions)
             .Append(',').Append(a.Tackling).Append(',').Append(a.Marking).Append(',').Append(a.Positioning)
             .Append(',').Append(a.Creativity).Append(',').Append(a.Passing).Append(',').Append(a.Crossing)
             .Append(',').Append(a.Finishing).Append(',').Append(a.LongShots).Append(',').Append(a.OffTheBall)
             .Append(',').Append(a.PhysicalPower).Append(',').Append(a.BallControl).Append(',').Append(a.Dribbling).Append(',').Append(a.Acceleration)
             .Append(',').Append(a.Speed).Append(',').Append(a.Jumping).Append(',').Append(a.Heading)
             .Append(',').Append(a.FreeKicks).Append(',').Append(a.Penalty)
             .Append(',').Append(a.AerialAbility).Append(',').Append(a.Handling).Append(',').Append(a.Kicking).Append(',').Append(a.OneOnOne).Append(',').Append(a.Reflexes).Append(']');
            b.Append(",\"leftFoot\":").Append(a.LeftFoot).Append(",\"rightFoot\":").Append(a.RightFoot);
            b.Append(",\"group\":\"").Append(pr.NaturalPosition).Append('"');
            b.Append(",\"nation\":"); Str(b, pr.Nationality);
            b.Append(",\"cats\":[").Append((int)MathF.Round(a.Mental)).Append(',').Append((int)MathF.Round(a.Defence)).Append(',').Append((int)MathF.Round(a.Playmaking))
             .Append(',').Append((int)MathF.Round(a.Attacking)).Append(',').Append((int)MathF.Round(a.Athletic)).Append(',').Append((int)MathF.Round(a.Goalkeeping)).Append(']');
        }

        /// <summary>
        /// The fields of the replay file apart from the frames, events and stats: seed, timing, conditions, the mud and
        /// water map, and both teams with their players. A JSON object body (no braces), so a file writer can continue it.
        /// With <paramref name="finalStats"/> each team also carries its full-time statistics.
        /// </summary>
        public string Header(bool finalStats)
        {
            var e = _engine;
            e.UpdateRatings();
            var b = new StringBuilder(8192);
            b.Append("\"version\":1,\"seed\":").Append(e.Seed.ToString(CultureInfo.InvariantCulture));
            b.Append(",\"fps\":").Append(_fps).Append(",\"scale\":10");
            b.Append(",\"halfRealSeconds\":").Append(N(e.Config.HalfRealSeconds));
            b.Append(",\"halftimeRealSeconds\":").Append(N(e.Config.HalftimeRealSeconds));
            var c = e.Conditions;
            b.Append(",\"conditions\":{\"weather\":\"").Append(c.Weather).Append("\",\"maintenance\":").Append(N(c.Maintenance));
            b.Append(",\"wind\":").Append(N(c.WindSpeed)).Append(",\"windAngle\":").Append(c.WindAngle.ToString("0.###", CultureInfo.InvariantCulture));
            b.Append(",\"night\":").Append(c.Night ? "true" : "false").Append('}');
            b.Append(",\"surface\":");
            var s = e.Surface;
            if (s == null) b.Append("null");
            else
            {
                b.Append("{\"minX\":").Append(N(PitchSurface.MinX)).Append(",\"minY\":").Append(N(PitchSurface.MinY));
                b.Append(",\"cell\":").Append(N(PitchSurface.Cell)).Append(",\"columns\":").Append(PitchSurface.Columns).Append(",\"rows\":").Append(PitchSurface.Rows);
                b.Append(",\"mud\":");
                Bytes(b, s.HasMud ? s.Mud : null, s);
                b.Append(",\"water\":");
                Bytes(b, s.HasWater ? s.Water : null, s);
                b.Append('}');
            }
            b.Append(",\"attrNames\":[\"Aggression\",\"Teamwork\",\"Composure\",\"Decisions\",\"Tackling\",\"Marking\",\"Positioning\",\"Creative\",\"Passing\",\"Crossing\",\"Finishing\",\"Long Shots\",\"Off The Ball\",\"Physical Power\",\"Ball Control\",\"Dribbling\",\"Acceleration\",\"Speed\",\"Jumping\",\"Heading\",\"Free Kicks\",\"Penalty\",\"Aerial Ability\",\"Handling\",\"Kicking\",\"One on One\",\"Reflexes\"]");
            b.Append(",\"rules\":{\"maxSubs\":").Append(e.Config.MaxSubstitutions).Append(",\"windows\":").Append(e.Config.MaxSubstitutionWindows).Append('}');
            b.Append(",\"formations\":[");
            for (int i = 0; i < Formation.Names.Length; i++)
            {
                var f = Formation.ByName(Formation.Names[i]);
                if (i > 0) b.Append(',');
                b.Append("{\"name\":"); Str(b, f.Name);
                b.Append(",\"slots\":[");
                for (int j = 0; j < 11; j++)
                {
                    var sl = f.Slots[j];
                    if (j > 0) b.Append(',');
                    b.Append("{\"f\":").Append(N(sl.BaseForward)).Append(",\"l\":").Append(N(sl.BaseLateral)).Append(",\"line\":").Append((int)sl.Line)
                     .Append(",\"kind\":").Append((int)f.KindAt(j)).Append(",\"abbr\":"); Str(b, SlotAbbreviation(f, j)); b.Append('}');
                }
                b.Append("]}");
            }
            b.Append(']');
            b.Append(",\"home\":");
            Team(b, e.Home, finalStats);
            b.Append(",\"away\":");
            Team(b, e.Away, finalStats);
            b.Append(",\"frameLayout\":\"t*100, clockSec, phase, homeScore, awayScore, ballX*10, ballY*10, ballZ*100, ballOwner, then per player [x*10, y*10, facing*100, jumpZ*100, flags(1=sentOff,2=gk,4=booked,8=caught offside,16=sliding,32=on the ground,64=injured)] x22, then officials [x*10, y*10, flag] x3\"");
            b.Append(",\"statsLayout\":\"t*100, possession home/away (s*10), shots h/a, on target h/a, tackles h/a, tackles won h/a, dribbles h/a, dribbles won h/a, then the energy left of the 22 players (0-100)\"");
            return b.ToString();
        }

        private static void Bytes(StringBuilder b, float[] a, PitchSurface s)
        {
            if (a == null) { b.Append("null"); return; }
            b.Append('[');
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) b.Append(',');
                b.Append((int)MathF.Round(MathUtil.Clamp01(a[i]) * 255f));
            }
            b.Append(']');
        }

        private static void Team(StringBuilder b, TeamAgent t, bool finalStats)
        {
            b.Append("{\"name\":"); Str(b, t.Name);
            b.Append(",\"shortName\":"); Str(b, t.ShortName);
            var k = t.Setup.Kit;
            b.Append(",\"kit\":{\"primary\":"); Str(b, k.Primary);
            b.Append(",\"secondary\":"); Str(b, k.Secondary);
            b.Append(",\"number\":"); Str(b, k.Number);
            b.Append(",\"gk\":"); Str(b, k.GoalkeeperPrimary);
            b.Append(",\"gkNumber\":"); Str(b, k.GoalkeeperNumber);
            b.Append(",\"gkTrim\":"); Str(b, string.IsNullOrEmpty(k.GoalkeeperSecondary) ? k.Secondary : k.GoalkeeperSecondary);
            b.Append("},\"players\":[");
            for (int i = 0; i < t.Players.Count; i++)
            {
                var p = t.Players[i];
                if (i > 0) b.Append(',');
                b.Append("{\"index\":").Append(p.Index).Append(",\"number\":").Append(p.Number);
                b.Append(",\"name\":"); Str(b, p.Profile.FullName);
                b.Append(",\"shortName\":"); Str(b, p.Profile.ShortName);
                b.Append(",\"role\":"); Str(b, SlotAbbreviation(t.Formation, p.SlotIndex));
                b.Append(",\"slot\":").Append(p.SlotIndex);
                b.Append(",\"passing\":").Append(p.Attr.Passing).Append(",\"speed\":").Append(p.Attr.Speed).Append(",\"stamina\":").Append(p.Attr.Stamina);
                Profile(b, p.Profile);
                b.Append('}');
            }
            b.Append("],\"formation\":"); Str(b, t.Formation.Name);
            // the manager's set-up as the page's panel shows it: per slot role.longShots.dribbling.aggression, and the team instructions
            var tt = t.Tactics;
            b.Append(",\"orders\":[");
            for (int i = 0; i < 11; i++)
            {
                var o = tt.OrdersFor(i);
                if (i > 0) b.Append(',');
                b.Append('"').Append((int)o.Role).Append('.').Append((int)o.LongShots).Append('.').Append((int)o.Dribbling).Append('.').Append((int)o.Aggression).Append('"');
            }
            b.Append("],\"tactics\":{\"line\":").Append((int)MathF.Round(tt.DefensiveLine * 100f)).Append(",\"width\":").Append((int)MathF.Round(tt.Width * 100f));
            b.Append(",\"passing\":").Append(tt.Passing == PassingStyle.Short ? 0 : tt.Passing == PassingStyle.Direct ? 2 : 1);
            b.Append(",\"tempo\":").Append(tt.Tempo < 0.33f ? 0 : tt.Tempo > 0.67f ? 2 : 1);
            b.Append(",\"style\":").Append(tt.Tackling == TacklingStyle.StayOnFeet ? 0 : tt.Tackling == TacklingStyle.GetStuckIn ? 2 : 1);
            b.Append(",\"defence\":").Append((int)tt.Defence).Append(",\"attack\":").Append((int)tt.Attack).Append(",\"special\":").Append((int)tt.Special).Append('}');
            b.Append(",\"bench\":[");
            for (int i = 0; i < t.Setup.Bench.Count; i++)
            {
                var q = t.Setup.Bench[i];
                if (i > 0) b.Append(',');
                b.Append("{\"number\":").Append(q.ShirtNumber);
                b.Append(",\"name\":"); Str(b, q.FullName);
                b.Append(",\"shortName\":"); Str(b, q.ShortName);
                b.Append(",\"role\":"); Str(b, q.PositionLabel);
                b.Append(",\"passing\":").Append(q.Attributes.Passing).Append(",\"speed\":").Append(q.Attributes.Speed).Append(",\"stamina\":").Append(q.Attributes.Stamina);
                Profile(b, q);
                b.Append('}');
            }
            b.Append(']');
            if (finalStats)
            {
                var s = t.Stats;
                b.Append(",\"stats\":{\"PossessionSeconds\":").Append(N(s.PossessionSeconds));
                b.Append(",\"Shots\":").Append(s.Shots).Append(",\"ShotsOnTarget\":").Append(s.ShotsOnTarget);
                b.Append(",\"Tackles\":").Append(s.Tackles).Append(",\"TacklesWon\":").Append(s.TacklesWon);
                b.Append(",\"Dribbles\":").Append(s.Dribbles).Append(",\"DribblesWon\":").Append(s.DribblesWon);
                b.Append(",\"ExpectedGoals\":").Append(N(s.ExpectedGoals)).Append(",\"Corners\":").Append(s.Corners).Append(",\"Fouls\":").Append(s.Fouls);
                b.Append(",\"Passes\":").Append(s.Passes).Append(",\"PassesCompleted\":").Append(s.PassesCompleted).Append('}');
                // everyone who played: rating, goals, assists, man of the match (the ratings are brought up to date by Header)
                b.Append(",\"records\":[");
                for (int i = 0; i < t.Records.Count; i++)
                {
                    var r = t.Records[i];
                    if (i > 0) b.Append(',');
                    b.Append("{\"number\":").Append(r.Profile.ShirtNumber).Append(",\"name\":"); Str(b, r.Profile.ShortName);
                    b.Append(",\"rating\":").Append(N(r.Rating)).Append(",\"goals\":").Append(r.Goals).Append(",\"assists\":").Append(r.Assists);
                    b.Append(",\"on\":").Append((int)(r.OnClock / 60f)).Append(",\"off\":").Append(r.OffClock < 0f ? -1 : (int)(r.OffClock / 60f));
                    if (r.ManOfTheMatch) b.Append(",\"motm\":true");
                    b.Append('}');
                }
                b.Append(']');
            }
            b.Append('}');
        }

        /// <summary>The position as the team sheet shows it: GK, RB, CB, LB, DM, CM, AM, RW / RM, LW / LM, ST (a 4-2-3-1's holding pair are DMs).</summary>
        public static string SlotAbbreviation(Formation f, int slot)
        {
            var k = f.KindAt(slot);
            if (k == PositionKind.DefensiveMidfielder) return "DM";
            if (k == PositionKind.FullBack && f.Slots[slot].BaseForward > -29f) return f.Slots[slot].BaseLateral < 0f ? "RWB" : "LWB";
            return Formation.RoleAbbreviation(f.Slots[slot].Role);
        }

        /// <summary>Advances up to <paramref name="frames"/> frames (fewer if the match ends) and returns what was recorded.</summary>
        public ReplayChunk Step(int frames)
        {
            _frames.Clear();
            _stats.Clear();
            _events.Clear();
            _events.Append('[');
            _eventCount = 0;
            float dt = 1f / _fps;
            int written = 0;
            for (int n = 0; n < frames && !_engine.IsFinished; n++)
            {
                _engine.Advance(dt);
                foreach (var ev in _engine.DrainEvents()) AppendEvent(ev);
                if (_engine.RealTimeElapsed >= _nextStats)
                {
                    _nextStats = _engine.RealTimeElapsed + 1f;
                    var hs = _engine.Home.Stats;
                    var aw = _engine.Away.Stats;
                    _stats.Add((int)MathF.Round(_engine.RealTimeElapsed * 100f));
                    _stats.Add((int)(hs.PossessionSeconds * 10f)); _stats.Add((int)(aw.PossessionSeconds * 10f));
                    _stats.Add(hs.Shots); _stats.Add(aw.Shots); _stats.Add(hs.ShotsOnTarget); _stats.Add(aw.ShotsOnTarget);
                    _stats.Add(hs.Tackles); _stats.Add(aw.Tackles); _stats.Add(hs.TacklesWon); _stats.Add(aw.TacklesWon);
                    _stats.Add(hs.Dribbles); _stats.Add(aw.Dribbles); _stats.Add(hs.DribblesWon); _stats.Add(aw.DribblesWon);
                    foreach (var p in _engine.AllPlayers) _stats.Add((int)MathF.Round(p.Energy * 100f));
                    _engine.UpdateRatings();
                    foreach (var p in _engine.AllPlayers) _stats.Add(p.Record != null ? (int)MathF.Round(p.Record.Rating * 10f) : 0);
                }
                if (_engine.Phase == MatchPhase.HalfTimeBreak) continue;
                AppendFrame();
                written++;
            }
            _events.Append(']');
            return new ReplayChunk
            {
                Frames = _frames.ToArray(),
                EventsJson = _events.ToString(),
                Stats = _stats.ToArray(),
                FrameCount = written,
                Finished = _engine.IsFinished,
            };
        }

        private static int Q(float v) => (int)MathF.Round(v * 10f);

        private void AppendFrame()
        {
            var e = _engine;
            var f = _frames;
            f.Add((int)MathF.Round(e.RealTimeElapsed * 100f));
            f.Add((int)e.Clock.Seconds);
            f.Add((int)e.Phase);
            f.Add(e.Home.Score);
            f.Add(e.Away.Score);
            f.Add(Q(e.Ball.Pos.X)); f.Add(Q(e.Ball.Pos.Y)); f.Add((int)MathF.Round(e.Ball.Z * 100f));
            f.Add(e.Ball.Owner != null ? e.Ball.Owner.Index : -1);
            foreach (var p in e.AllPlayers)
            {
                f.Add(Q(p.Pos.X));
                f.Add(Q(p.Pos.Y));
                f.Add((int)MathF.Round(MathUtil.WrapAngle(p.Facing) * 100f));
                f.Add((int)MathF.Round(p.JumpZ * 100f));
                f.Add((p.SentOff ? 1 : 0) | (p.IsGoalkeeper ? 2 : 0) | (p.YellowCards > 0 ? 4 : 0) | (p.OffsideMarkTimer > 0f ? 8 : 0) | (p.SlideTimer > 0f ? 16 : 0)
                      | (p.IsDown && p.SlideTimer <= 0f ? 32 : 0) | (p.Injury != InjuryLevel.None ? 64 : 0)
                      | (p.KickPhase << 7) | (p.KickLeft ? 512 : 0));
            }
            foreach (var o in e.Officials)
            {
                f.Add(Q(o.Pos.X));
                f.Add(Q(o.Pos.Y));
                f.Add(o.FlagRaised ? (o.FlagDir < 0 ? 4 : 1) : o.CardShown == 1 ? 2 : o.CardShown == 2 ? 3 : 0); // flag up, or the referee showing a yellow (2) or red (3) card
            }
        }

        private void AppendEvent(MatchEvent e)
        {
            var b = _events;
            if (_eventCount++ > 0) b.Append(',');
            b.Append("{\"t\":").Append(N(Math.Round(_engine.RealTimeElapsed, 2)));
            b.Append(",\"minute\":"); Str(b, e.Minute);
            b.Append(",\"type\":"); Str(b, e.Type.ToString());
            b.Append(",\"text\":"); Str(b, e.Text);
            b.Append(",\"team\":").Append(e.Team);
            if (e.Type == MatchEventType.Whistle) { b.Append(",\"whistle\":"); Str(b, e.Whistle.ToString()); }
            b.Append(",\"tone\":"); Str(b, Commentary.ToneOf(e.Type).ToString());
            b.Append(",\"detail\":").Append(e.Detail);
            b.Append(",\"x\":").Append(N(Math.Round(e.X, 1))).Append(",\"y\":").Append(N(Math.Round(e.Y, 1))).Append(",\"v\":").Append(N(Math.Round(e.Value, 1)));
            b.Append(",\"p\":").Append(e.Player);
            if (e.OtherPlayer >= 0) b.Append(",\"o\":").Append(e.OtherPlayer);
            if (!string.IsNullOrEmpty(e.Info)) { b.Append(",\"s\":"); Str(b, e.Info); }
            b.Append('}');
        }
    }
}
