using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The long-throw specialist (polish round 2): a throw-in is released at a speed inside the measured 12-19 m/s, at the measured angle and
    /// backspin, and flies by the ball's physics; only a man who can reach the six-yard box throws it long, and the specialist is that man.
    /// </summary>
    public class LongThrowTests
    {
        private readonly ITestOutputHelper _out;
        public LongThrowTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void TheFlightReproducesTheMeasuredThrow()
        {
            // Linthorne and Everett 2006: 13.4 m/s at 32.1 degrees from 2.23 m carried 17.0 +- 0.8 m
            float carry = MatchEngine.ThrowCarry(13.4f, 32.1f * MathF.PI / 180f, Pitch.BallRadius, MatchConfig.CreateDefault());
            _out.WriteLine($"13.4 m/s at 32.1 degrees: {carry:0.0} m (measured 17.0)");
            Assert.InRange(carry, 16.2f, 17.8f);
        }

        [Fact]
        public void ReleaseSpeedsStayInsideTheMeasuredRangeAndTheSpecialistIsAtTheTop()
        {
            int squads = 0, withOne = 0;
            foreach (ulong seed in Enumerable.Range(0, 300).Select(i => (ulong)i))
            {
                foreach (var t in new[] { DemoTeams.Red(seed), DemoTeams.Blue(seed) })
                {
                    squads++;
                    var all = t.Starters.Concat(t.Bench).ToList();
                    var specialists = all.Where(p => p.Has(PlayerTrait.LongThrowSpecialist)).ToList();
                    Assert.True(specialists.Count <= 1, "one long thrower a squad at most");
                    if (specialists.Count == 1) withOne++;
                    foreach (var p in all)
                    {
                        float v = MatchEngine.MaxThrowSpeed(p);
                        Assert.InRange(v, 12f, 19f); // measured: 12-19 m/s
                        if (p.Has(PlayerTrait.LongThrowSpecialist))
                        {
                            Assert.True(v >= MatchEngine.ThrowSpeedOrdinaryMax, "a specialist throws at the top of the range");
                            Assert.NotEqual(PositionGroup.Goalkeeper, p.NaturalPosition);
                            Assert.True(p.Attributes.PhysicalPower >= PlayerTraits.LongThrowerPower);
                        }
                        else Assert.True(v <= MatchEngine.ThrowSpeedOrdinaryMax);
                    }
                }
            }
            _out.WriteLine($"squads with a long-throw specialist: {withOne} of {squads}");
            Assert.InRange(withOne / (double)squads, 0.1, 0.5); // a rare weapon, not every side's
        }

        [Fact]
        public void OnlyTheSpecialistReachesTheSixYardBoxFromTheTouchline()
        {
            var c = MatchConfig.CreateDefault();
            float toBox = MathF.Sqrt(25f * 25f + 1f); // from the touchline level with the six-yard box's near corner (9 m from the middle)
            float best = MatchEngine.ThrowCarry(MatchEngine.ThrowSpeedMax, MatchEngine.LongThrowReleaseAngle, MatchEngine.LongThrowTargetHeight, c);
            float ordinary = MatchEngine.ThrowCarry(MatchEngine.ThrowSpeedOrdinaryMax, MatchEngine.LongThrowReleaseAngle, MatchEngine.LongThrowTargetHeight, c);
            _out.WriteLine($"to a man's head: the best specialist {best:0.0} m, the best ordinary thrower {ordinary:0.0} m; the box is {toBox:0.0} m away");
            Assert.True(best > toBox);
            Assert.True(ordinary < toBox);
        }

        [Fact]
        public void InMatchesOnlySpecialistsThrowItLongAndTheyDo()
        {
            var total = new ConcurrentDictionary<string, int>();
            Parallel.For(0, 120, i =>
            {
                ulong seed = 8800 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                e.RunToEnd();
                Assert.Equal(0, e.RecoveredErrors);
                foreach (var kv in e.Diagnostics) if (kv.Key.StartsWith("throw.long")) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            int specialist = total.GetValueOrDefault("throw.long.specialist") + total.GetValueOrDefault("throw.long.specialist.short");
            int other = total.GetValueOrDefault("throw.long.other") + total.GetValueOrDefault("throw.long.other.short");
            _out.WriteLine($"long throws in 120 matches: by a specialist {specialist}, by anyone else {other}");
            Assert.Equal(0, other);
            Assert.True(specialist > 0, "the specialists throw it long");
        }
    }
}
