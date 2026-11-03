using System;
using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// The tricks the viewer can see: a heel flick goes back or to the side while the player keeps looking forward, and the foot
    /// goes the other way; a fake shot swings the foot as if kicking, nudges the ball sideways and holds the defender for a second.
    /// </summary>
    public class SkillMovesTests
    {
        private readonly ITestOutputHelper _out;
        public SkillMovesTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void AHeelFlickGoesBackAndTheFakeShotMovesTheBall()
        {
            int heels = 0, heelsFacingForward = 0, fakes = 0, nudged = 0, heldDefenders = 0;
            object gate = new object();
            Parallel.For(0, 16, i =>
            {
                var red = DemoTeams.Red(9300 + (ulong)i);
                foreach (var p in red.Starters) p.Traits |= PlayerTrait.Showboat;   // (the flair players: they try it more often)
                var e = new MatchEngine(red, DemoTeams.Blue(9300 + (ulong)i), 9300 + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                int h = 0, hf = 0, f = 0, n = 0, d = 0;
                bool inHeel = false, inFake = false; float maxNudge = 0f;
                while (!e.IsFinished && e.Clock.Seconds < 60 * 60)
                {
                    e.Advance(0.1f);
                    foreach (var p in e.Home.Players)
                    {
                        if (p.KickHeel && p.KickTimer > 0.3f && !inHeel && e.Ball.Owner == null)
                        {
                            inHeel = true; h++;
                            // he does not turn to the ball: the ball goes off at an angle to where he looks
                            float a = MathF.Abs(MathUtil.AngleDiff(p.Facing, e.Ball.Velocity.Angle));
                            if (a > MathF.PI * 0.4f) hf++;
                        }
                        if (p.KickTimer <= 0f) inHeel = false;
                        if (p.FakeUntil > e.SimTime && e.Ball.Owner == p)
                        {
                            if (!inFake) { inFake = true; f++; maxNudge = 0f; }
                            // how far the ball is from where it would sit with a plain carry (just ahead of the feet)
                            float plain = 0.45f + MathUtil.Clamp(p.Vel.Length * 0.06f, 0f, 0.45f);
                            float away = Vec2.Distance(e.Ball.Pos, p.Pos + p.FacingDir * plain);
                            maxNudge = MathF.Max(maxNudge, away);
                        }
                        else if (inFake && p.FakeUntil <= e.SimTime) { inFake = false; if (maxNudge > 0.3f) n++; }
                    }
                }
                d = e.Diagnostics.GetValueOrDefault("skill.fakeShot");
                lock (gate) { heels += h; heelsFacingForward += hf; fakes += f; nudged += n; heldDefenders += d; }
            });
            _out.WriteLine($"heel flicks {heels} (looking away from the ball {heelsFacingForward}), fake shots {fakes} (ball nudged {nudged})");
            Assert.True(heels >= 3, "no heel flicks");
            Assert.True(heelsFacingForward >= heels * 0.6, $"he turns to watch the heel flick: {heelsFacingForward} of {heels}");
            Assert.True(fakes >= 3, "no fake shots");
            Assert.True(nudged >= fakes * 0.5, $"the ball does not move with the fake: {nudged} of {fakes}");
        }
    }
}
