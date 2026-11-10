using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Decisions by the touchline (polish round 1): no deliberate kick into touch in the attacking third; a player pinned by a line can
    /// play the ball off his marker for a corner or a throw-in, and what decides it is the contact (the stage 8 body impact), not a draw.
    /// Accidental outs - deflections, heavy touches, tackles - stay.
    /// </summary>
    public class TouchlineTests
    {
        private readonly ITestOutputHelper _out;
        public TouchlineTests(ITestOutputHelper output) { _out = output; }

        private static System.Collections.Generic.Dictionary<string, int> Run(int matches, ulong seed0)
        {
            var total = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            Parallel.For(0, matches, i =>
            {
                ulong seed = seed0 + (ulong)i;
                var e = new MatchEngine(DemoTeams.Red(seed), DemoTeams.Blue(seed), seed, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                while (!e.IsFinished) e.Advance(0.5f);
                Assert.Equal(0, e.RecoveredErrors);
                foreach (var kv in e.Diagnostics) total.AddOrUpdate(kv.Key, kv.Value, (_, v) => v + kv.Value);
            });
            return total.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private static int Sum(System.Collections.Generic.Dictionary<string, int> d, string prefix) => d.Where(kv => kv.Key.StartsWith(prefix)).Sum(kv => kv.Value);

        [Fact]
        public void NobodyKicksItOutOnPurposeInTheAttackingThirdButTheBallStillGoesOutByAccident()
        {
            var d = Run(24, 7100);
            int attacking = Sum(d, "kickout.att."), middle = Sum(d, "kickout.mid."), own = Sum(d, "kickout.own.");
            int throwIns = Sum(d, "out.ThrowIn."), deflected = d.Where(kv => kv.Key.StartsWith("out.ThrowIn.") && kv.Key.EndsWith(".deflect")).Sum(kv => kv.Value);
            int accidental = throwIns - middle - own;
            _out.WriteLine($"deliberate kick-outs: attacking third {attacking}, middle {middle}, own half {own}; throw-ins {throwIns}, not on purpose {accidental} (off a deflection {deflected})");
            // before this round about 5 a match were deliberate kicks into touch in the attacking third (69% of all of them)
            // (counted where the ball is when it is struck: a kick decided at the very edge of the middle third can land on this side of it -
            // 0.01 a match measured over 400)
            Assert.True(attacking <= 1, "deliberate kick-outs in the attacking third: " + attacking);
            // passes that run out, heavy touches, tackles and deflections still put it out (about 16 a match measured)
            Assert.True(accidental > 24 * 12, "the ball still goes out by accident: " + accidental);
            Assert.True(deflected > 0, "deflections still put it out");
        }

        [Fact]
        public void PlayingItOffTheDefenderWinsTheRestartThroughTheContact()
        {
            var d = Run(24, 7200);
            int tries = Sum(d, "playoff.corner.") + Sum(d, "playoff.throw."), contacts = d.GetValueOrDefault("playoff.contact");
            int won = Sum(d, "playoff.result.") - d.Where(kv => kv.Key.StartsWith("playoff.result.") && kv.Key.EndsWith(".lost")).Sum(kv => kv.Value);
            _out.WriteLine($"play-offs tried {tries}, clean contacts {contacts}, restarts won after a contact {won}");
            Assert.True(tries > 24, "the option is used: " + tries);
            Assert.True(contacts > 0 && contacts < tries, "a clean strike reaches the leg, a poor one does not: " + contacts + " of " + tries);
            // the restart comes from the impact and the roll: most clean contacts carry over the line, not all
            Assert.InRange(won / (double)contacts, 0.5, 1.0);
        }

        [Fact]
        public void TheLegsFaceSendsABallDrivenAcrossHimTowardsTheLine()
        {
            // a ball driven along x at a defender with the touchline behind him along +y: the face between the two
            var u = new Vec2(1f, 0f);
            var outward = new Vec2(0f, 1f);
            float offset = MatchEngine.PlayOffFaceOffset(u, outward);
            Assert.InRange(offset, 0.70f, 0.71f); // the bisector of where it came from and the line: 45 degrees
            var c = MatchConfig.CreateDefault();
            float r = Pitch.BallRadius;
            var m = new BallMotion { X = 0f, Y = 0f, Z = r, Vx = 15f, Vy = 0f, Wx = 0f, Wy = 15f / r, HasWorldSpin = true };
            BallPhysics.Impact(ref m, c, MatchEngine.BodyContactNormal(new Vec2(15f, 0f), Vec2.Zero, offset, 0.125f), 0.4f, c.Collisions.BodyFriction, new Vec3(0f, 0f, 0f));
            _out.WriteLine($"after the leg: ({m.Vx:0.0}, {m.Vy:0.0}, {m.Vz:0.0}) m/s");
            Assert.True(m.Vy > 3f, "it comes off him towards the line: " + m.Vy);
            Assert.True(m.Vy > MathF.Abs(m.Vx), "more towards the line than along its old path");
        }
    }
}
