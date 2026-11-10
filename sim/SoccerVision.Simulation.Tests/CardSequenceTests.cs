using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The card as a sequence (polish round 3): the referee never chases a player; the card counts - the event the indicator follows -
    /// from the moment he holds it up; a sent-off player takes it in and walks off before play restarts; every restart waits for it.
    /// </summary>
    public class CardSequenceTests
    {
        private readonly ITestOutputHelper _out;
        public CardSequenceTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void TheCardCountsWhenItIsHeldUpTheRefereeNeverChasesAndPlayWaitsForTheSentOffPlayer()
        {
            int cards = 0, shownAtEvent = 0, reds = 0, sentOffBeforeShown = 0, restartedWithHimOn = 0, refereeFast = 0;
            var how = new ConcurrentDictionary<string, int>();
            var problems = new ConcurrentBag<string>();
            Parallel.For(0, 48, i =>
            {
                ulong seed = 9300 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                int seen = 0;
                PlayerAgent leaving = null;
                while (!e.IsFinished)
                {
                    int whistleBefore = e.Diagnostics.GetValueOrDefault("card.atWhistle");
                    e.Advance(0.05f);
                    bool atWhistle = e.Diagnostics.GetValueOrDefault("card.atWhistle") != whistleBefore;
                    var log = e.EventLog;
                    for (; seen < log.Count; seen++)
                    {
                        var x = log[seen];
                        if (x.Type != MatchEventType.YellowCard && x.Type != MatchEventType.RedCard) continue;
                        // (a card still on its way at the whistle is shown at once where they are, the red after a second yellow with it: not a sequence)
                        if (atWhistle || (e.Phase != MatchPhase.Live && e.Phase != MatchPhase.Stoppage)) continue;
                        Interlocked.Increment(ref cards);
                        int want = x.Type == MatchEventType.RedCard ? 2 : 1;
                        // the event and the card in the referee's hand come in the same moment
                        if (e.Referee.CardShown == want) Interlocked.Increment(ref shownAtEvent);
                        else problems.Add($"seed {seed} {x.Minute}: {x.Type} with the referee holding {e.Referee.CardShown}");
                        if (x.Type == MatchEventType.RedCard)
                        {
                            Interlocked.Increment(ref reds);
                            var p = e.PlayerByIndex(x.Player);
                            if (p != null && p.SentOff) Interlocked.Increment(ref sentOffBeforeShown);
                            leaving = p;
                        }
                    }
                    // play does not restart while the sent-off player is still on the pitch (the wait has a limit: counted apart)
                    if (leaving != null && leaving.SentOff)
                    {
                        bool off = MathF.Abs(leaving.Pos.Y) > Pitch.HalfWidth + 0.5f;
                        if (off) leaving = null;
                        else if (e.Phase == MatchPhase.Live && !e.Diagnostics.ContainsKey("card.red.leaveTimeout"))
                        {
                            Interlocked.Increment(ref restartedWithHimOn);
                            problems.Add($"seed {seed}: play restarted with the sent-off player on the pitch");
                            leaving = null;
                        }
                    }
                }
                Assert.Equal(0, e.RecoveredErrors);
                Interlocked.Add(ref refereeFast, e.Diagnostics.GetValueOrDefault("card.refereeFast"));
                foreach (var kv in e.Diagnostics) if (kv.Key.StartsWith("card.how.")) how.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            _out.WriteLine($"cards {cards}, shown as the event came {shownAtEvent}; reds {reds}, sent off before the card {sentOffBeforeShown}, restarted with him still on {restartedWithHimOn}; referee faster than a jog {refereeFast} steps");
            _out.WriteLine(string.Join(", ", how.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")));
            foreach (var pr in problems.Take(5)) _out.WriteLine(pr);
            Assert.True(cards > 100, "too few cards to say anything: " + cards);
            Assert.Equal(cards, shownAtEvent);
            Assert.True(reds > 0, "no red card to look at");
            Assert.Equal(0, sentOffBeforeShown);
            Assert.Equal(0, restartedWithHimOn);
            Assert.Equal(0, refereeFast);
            // the three ways the referee and the player come together all happen
            Assert.True(how.GetValueOrDefault("card.how.Approach") > 0 && how.GetValueOrDefault("card.how.Summon") > 0 && how.GetValueOrDefault("card.how.WalkAway") > 0,
                "approach, summon and walk-away all occur");
        }
    }
}
