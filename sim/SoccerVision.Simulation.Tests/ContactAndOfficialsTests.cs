using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>The ball only moves when something touches it; the officials stand where they should and show their cards.</summary>
    public class ContactAndOfficialsTests
    {
        [Fact]
        public void TheBallIsOnlyKickedOrDeflectedByAPlayerWhoCanReachIt()
        {
            var engines = new MatchEngine[12];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 8000 + (ulong)i) { CollectDiagnostics = true };
                engines[i].RunToEnd();
            });
            var far = engines.SelectMany(e => e.Diagnostics).Where(kv => kv.Key.StartsWith("contact.far.")).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
            int all = engines.Sum(e => e.Diagnostics.GetValueOrDefault("contact.all"));
            // Nothing from more than a metre beyond reach, no kick at all from out of reach, and only a handful of
            // borderline touches (a moving ball measured at the end of the step).
            Assert.DoesNotContain(far, kv => kv.Key.EndsWith(".gt1m"));
            Assert.DoesNotContain(far, kv => kv.Key.StartsWith("contact.far.kick."));
            int borderline = far.Values.Sum();
            Assert.True(borderline < all / 500, $"{borderline} borderline touches in {all}: " + string.Join(", ", far.Select(kv => kv.Key + "=" + kv.Value)));
        }

        [Fact]
        public void AssistantsStayOnTheirOwnTouchlineInTheirOwnHalf()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 8100);
            int corners = 0;
            while (!e.IsFinished)
            {
                e.Advance(0.25f);
                if (e.Phase == MatchPhase.HalfTimeBreak) continue;
                foreach (var a in new[] { e.Assistant1, e.Assistant2 })
                {
                    Assert.InRange(a.Pos.Y * a.TouchlineSign, Pitch.HalfWidth - 0.1f, Pitch.HalfWidth + 1.3f);
                    Assert.True(a.Pos.X * a.CoverSign > -1.5f, "assistant in the other half: x " + a.Pos.X);
                    Assert.True(MathF.Abs(a.Pos.X) < Pitch.HalfLength + 0.5f, "assistant behind the goal line");
                }
                var r = e.CurrentRestart;
                if (e.Phase == MatchPhase.Stoppage && r != null && r.Type == RestartType.CornerKick && r.Timer > 4f)
                {
                    corners++;
                    var near = MathF.Sign(r.Spot.X) == e.Assistant1.CoverSign ? e.Assistant1 : e.Assistant2;
                    Assert.True(Vec2.Distance(near.Pos, r.Spot) > 3f, "assistant next to the ball at a corner");
                }
            }
            Assert.True(corners > 0);
        }

        [Fact]
        public void ARefereeShowsTheCardAndASecondYellowIsFollowedByTheRed()
        {
            int shown = 0, sequences = 0;
            for (ulong seed = 8200; seed < 8260 && sequences == 0; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed);
                float secondYellowAt = -1f;
                bool sawYellowAfter = false;
                while (!e.IsFinished)
                {
                    e.Advance(0.05f);
                    foreach (var ev in e.DrainEvents())
                    {
                        if (ev.Type == MatchEventType.YellowCard) { Assert.Equal(1, e.Referee.CardShown); shown++; }
                        if (ev.Type == MatchEventType.YellowCard && ev.Value >= 2f) { secondYellowAt = e.RealTimeElapsed; sawYellowAfter = false; }
                        if (ev.Type == MatchEventType.RedCard)
                        {
                            Assert.Equal(2, e.Referee.CardShown);
                            Assert.Equal(CommentaryTone.Red, Commentary.ToneOf(ev.Type));
                            if (ev.Value > 0.5f)
                            {
                                Assert.True(secondYellowAt >= 0f && sawYellowAfter, "the yellow is shown before the red");
                                Assert.True(e.RealTimeElapsed - secondYellowAt > 1f, "red " + (e.RealTimeElapsed - secondYellowAt) + " s after the yellow");
                                sequences++;
                            }
                        }
                    }
                    if (secondYellowAt >= 0f && e.Referee.CardShown == 1) sawYellowAfter = true;
                }
            }
            Assert.True(shown > 0 && sequences > 0, $"cards shown {shown}, second-yellow sequences {sequences}");
        }
    }
}
