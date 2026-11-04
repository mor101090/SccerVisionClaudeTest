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

        private static (double nutmegs, double won) Nutmegs(Func<TeamSetup> red, int n)
        {
            var es = new MatchEngine[n];
            Parallel.For(0, n, i =>
            {
                es[i] = new MatchEngine(red(), DemoTeams.Blue(), 9500 + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            return (es.Average(e => e.Home.Stats.Nutmegs), es.Average(e => e.Home.Stats.NutmegsWon));
        }

        [Fact]
        public void NutmegsHappenAMatchAndMoreWithFlairPlayers()
        {
            // both teams, plain squads: one or two a match at least, and the dribbler gets his ball back most of the time
            // (48 matches: the dummy for a shot is a rare thing - 0.06 to 0.19 a match on 16, 0.31 on 64)
            var both = new MatchEngine[48];
            Parallel.For(0, both.Length, i =>
            {
                both[i] = new MatchEngine(DemoTeams.Red(9400 + (ulong)i), DemoTeams.Blue(9400 + (ulong)i), 9400 + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                both[i].RunToEnd();
            });
            double perMatch = both.Average(e => e.Home.Stats.Nutmegs + e.Away.Stats.Nutmegs), won = both.Average(e => e.Home.Stats.NutmegsWon + e.Away.Stats.NutmegsWon);
            Assert.InRange(perMatch, 1.5, 8);
            // the dummy - opening his legs and leaving the ball to a team-mate in a better spot: at least two a match, some of them
            // for a better angle at goal
            double dummies = both.Average(e => e.Home.Stats.Dummies + e.Away.Stats.Dummies);
            double forShot = both.Average(e => e.Diagnostics.GetValueOrDefault("skill.dummy.forShot"));
            Assert.True(dummies >= 2.5, $"dummies a match {dummies:0.0}");
            Assert.True(forShot > 0.15, $"dummies for a shot a match {forShot:0.00}");
            Assert.True(won >= perMatch * 0.3, $"nutmegs {perMatch:0.0} a match, got the ball back {won:0.0}");
            // the flair players do it more: a squad of showboats against a squad that has no special ability
            Func<TeamSetup> flair = () => { var t = DemoTeams.Red(); foreach (var p in t.Starters) { p.Traits = PlayerTrait.Showboat | PlayerTrait.LovesToDribble; p.Attributes.Dribbling = 85; p.Attributes.Creativity = 80; } return t; };
            Func<TeamSetup> plain = () => { var t = DemoTeams.Red(); foreach (var p in t.Starters) { p.Traits = PlayerTrait.None; p.Attributes.Dribbling = 50; p.Attributes.Creativity = 50; } return t; };
            var f = Nutmegs(flair, 12); var q = Nutmegs(plain, 12);
            _out.WriteLine($"dummies {dummies:0.0} a match ({forShot:0.00} for a shot); nutmegs a match: both teams {perMatch:0.0} (ball back {won:0.0}); flair squad {f.nutmegs:0.0}, plain squad {q.nutmegs:0.0}");
            Assert.True(f.nutmegs > q.nutmegs * 2 + 0.5, $"flair {f.nutmegs:0.0} v plain {q.nutmegs:0.0}");
        }
    }
}
