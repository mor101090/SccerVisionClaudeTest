using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// A card is shown with the play stopped - a foul, the ball out, a goal, the end of the half - never while the play goes on:
    /// not for a foul played on with advantage, not for a booking for simulation, not the red card after a second yellow.
    /// </summary>
    public class CardTimingTests
    {
        private readonly ITestOutputHelper _out;
        public CardTimingTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void CardsAreNeverShownWhileThePlayGoesOn()
        {
            int cards = 0, live = 0;
            var late = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, 40, i =>
            {
                ulong seed = 5000 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                int seen = 0;
                while (!e.IsFinished)
                {
                    e.Advance(0.1f);
                    var log = e.EventLog;
                    for (; seen < log.Count; seen++)
                    {
                        var x = log[seen];
                        if (x.Type != MatchEventType.YellowCard && x.Type != MatchEventType.RedCard) continue;
                        System.Threading.Interlocked.Increment(ref cards);
                        if (e.Phase == MatchPhase.Live) { System.Threading.Interlocked.Increment(ref live); late.Add($"seed {seed} {x.Minute} {x.Text}"); }
                    }
                }
                Assert.Equal(0, e.RecoveredErrors);
            });
            _out.WriteLine($"cards {cards}, shown while the play went on {live}");
            Assert.True(cards >= 40, "too few cards to say anything: " + cards);
            Assert.True(live == 0, "a card was shown while the play went on: " + string.Join("; ", late.Take(5)));
        }
    }
}
