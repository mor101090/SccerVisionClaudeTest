using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The referee goes to the player to show him a card (he does not hold it up from wherever he is), and at a goal kick he waits where the ball
    /// will come down, in midfield, not on the edge of the box. (IFAB, Guidelines for match officials - positioning: "close enough to see play
    /// without interfering with play", a wide diagonal.)
    /// </summary>
    public class RefereeTests
    {
        [Fact]
        public void TheRefereeShowsTheCardBesideThePlayer()
        {
            int shown = 0, beside = 0;
            var lockObj = new object();
            Parallel.For(0, 8, i =>
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 6100UL + (ulong)i, MatchConfig.CreateDefault());
                int last = 0, s = 0, b = 0;
                while (!e.IsFinished)
                {
                    e.Advance(0.1f);
                    int now = e.Referee.CardShown;
                    if (now != 0 && last == 0)
                    {
                        s++;
                        var p = e.Referee.CardFor;
                        if (p != null && Vec2.Distance(p.Pos, e.Referee.Pos) < 4f) b++;
                    }
                    last = now;
                }
                lock (lockObj) { shown += s; beside += b; }
            });
            Assert.True(shown >= 10, $"cards shown: {shown}");
            Assert.True(beside >= shown * 0.9, $"the card shown beside the player {beside} of {shown}");
        }

        [Fact]
        public void AtAGoalKickTheRefereeWaitsInMidfield()
        {
            int seen = 0, inMidfield = 0;
            for (ulong seed = 6200; seed < 6204; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
                float since = -1f;
                while (!e.IsFinished)
                {
                    e.Advance(0.1f);
                    bool goalKick = e.Phase == MatchPhase.Stoppage && e.CurrentRestart != null && e.CurrentRestart.Type == RestartType.GoalKick;
                    if (!goalKick) { since = -1f; continue; }
                    if (since < 0f) since = e.SimTime;
                    if (e.SimTime - since > 3.5f * e.Config.MotionSpeed && e.SimTime - since < 3.6f * e.Config.MotionSpeed)
                    {
                        seen++;
                        if (MathF.Abs(e.Referee.Pos.X) < 25f) inMidfield++;
                    }
                }
            }
            Assert.True(seen >= 10, $"goal kicks seen: {seen}");
            Assert.True(inMidfield >= seen * 0.8, $"referee in midfield (|x| < 25 m) at {inMidfield} of {seen} goal kicks");
        }
    }
}
