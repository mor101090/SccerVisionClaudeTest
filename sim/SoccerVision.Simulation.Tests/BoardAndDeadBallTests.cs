using System;
using System.Linq;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Positions moved by hand on the board, the ball that rolls on beyond the line, and shooting at an open goal.</summary>
    public class BoardAndDeadBallTests
    {
        [Theory]
        [InlineData(-33f, 0f, PositionKind.CentreBack, "CB")]
        [InlineData(-31f, -23f, PositionKind.FullBack, "RB")]
        [InlineData(-23f, 0f, PositionKind.DefensiveMidfielder, "DM")]
        [InlineData(-15f, 6f, PositionKind.CentralMidfielder, "CM")]
        [InlineData(-8f, 0f, PositionKind.AttackingMidfielder, "AM")]
        [InlineData(-6f, 24f, PositionKind.Winger, "LW")]
        [InlineData(-1f, 0f, PositionKind.Striker, "ST")]
        public void ASpotOnThePitchMeansARole(float forward, float lateral, PositionKind kind, string abbr)
        {
            var f = Formation.F442().WithPositions(Positions(5, forward, lateral), Positions(5, lateral, lateral));
            Assert.True(f.IsCustom);
            Assert.Equal(kind, f.KindAt(5));
            Assert.Equal(abbr, Formation.RoleAbbreviation(f.Slots[5].Role));
        }

        private static float[] Positions(int slot, float v, float _)
        {
            var a = Enumerable.Repeat(float.NaN, 11).ToArray();
            a[slot] = v;
            return a;
        }

        [Fact]
        public void APlanMovesAHoldingMidfielderUpAndHePlaysThere()
        {
            var probe = DemoTeams.Red();
            TeamPlan.Parse("formation=4-4-2;pos=6:-7:0").ApplyBeforeKickOff(probe);
            Assert.Equal(PositionKind.AttackingMidfielder, probe.Formation.KindAt(6));
            // where the man in slot 6 plays (his average depth over half an hour) - over four matches: in one match alone it
            // depends on how much of the ball his team has
            static double Depth(Func<TeamSetup> red, ulong seed)
            {
                var e = new MatchEngine(red(), DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
                double sum = 0; int n = 0;
                while (!e.IsFinished && e.Clock.Seconds < 30 * 60)
                {
                    e.Advance(0.5f);
                    var p = e.Home.Players.FirstOrDefault(q => q.SlotIndex == 6);
                    if (p != null && e.Phase == MatchPhase.Live) { sum += e.Home.Forward(p.Pos); n++; }
                }
                return sum / Math.Max(1, n);
            }
            TeamSetup Moved() { var r = DemoTeams.Red(); TeamPlan.Parse("formation=4-4-2;pos=6:-7:0").ApplyBeforeKickOff(r); return r; }
            var seeds = new ulong[] { 4, 5, 6, 7 };
            double moved = seeds.Average(s => Depth(Moved, s)), plain = seeds.Average(s => Depth(() => DemoTeams.Red(), s));
            Assert.True(moved > plain + 3, $"moved up: {moved:0.0} m v {plain:0.0} m");
        }

        [Fact]
        public void TheBallThatGoesOutRollsOnThenIsGone()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 8, MatchConfig.CreateDefault());
            bool seen = false, outside = false, gone = false;
            float seenAt = -1f;
            while (!e.IsFinished && !(seen && gone))
            {
                e.Advance(0.1f);
                if (e.DeadBallActive)
                {
                    if (!seen) seenAt = e.SimTime;
                    seen = true;
                    var b = e.DeadBall;
                    if (MathF.Abs(b.X) > Pitch.HalfLength || MathF.Abs(b.Y) > Pitch.HalfWidth) outside = true;
                    // never inside a goal from outside
                    Assert.False(MathF.Abs(b.X) > Pitch.HalfLength + 0.2f && MathF.Abs(b.X) < Pitch.HalfLength + Pitch.GoalDepth - 0.2f
                                 && MathF.Abs(b.Y) < Pitch.GoalHalfWidth - 0.3f && b.Z < Pitch.GoalHeight - 0.3f, "the dead ball went into the net");
                }
                else if (seen) gone = true;
            }
            Assert.True(seen && outside && gone, "the dead ball was seen beyond the line and then collected");
            Assert.Equal(0, e.RecoveredErrors);
        }

        [Fact]
        public void ABallOnTheRoofOfTheNetRollsOffAndNoDeadBallLiesForever()
        {
            // a shot over the bar dropping onto the roof of the net (the bug: it stayed there for the rest of the match)
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 8, MatchConfig.CreateDefault());
            e.DeadBall = new BallMotion { X = Pitch.HalfLength + 0.6f, Y = 1f, Z = Pitch.GoalHeight + 0.8f, Vx = 0.5f, Vy = 0f, Vz = -2f };
            e.DeadBallActive = true;
            bool onRoof = false;
            for (int i = 0; i < 120 && e.DeadBallActive; i++)
            {
                e.Advance(0.1f);
                if (e.DeadBallActive && e.DeadBall.Z > Pitch.GoalHeight) onRoof = true;
            }
            Assert.True(onRoof, "the ball never landed on the roof");
            Assert.False(e.DeadBallActive, "the ball is still lying on the roof of the net");
            // and in whole matches no ball that went out is shown for long
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var m = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
                float since = -1f;
                while (!m.IsFinished)
                {
                    m.Advance(0.25f);
                    if (m.DeadBallActive) { if (since < 0f) since = m.SimTime; Assert.True(m.SimTime - since < 12f, "a dead ball lay there for good"); }
                    else since = -1f;
                }
            }
        }

        [Fact]
        public void APlayerShootsAtAnOpenGoal()
        {
            int considered = 0;
            for (ulong seed = 60; seed < 66; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                e.RunToEnd();
                considered += e.Diagnostics.GetValueOrDefault("shot.openGoal.considered");
            }
            Assert.True(considered > 0, "no open goal ever seen in six matches");
        }
    }
}
