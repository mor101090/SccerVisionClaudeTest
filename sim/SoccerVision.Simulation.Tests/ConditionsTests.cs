using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Weather, wind and the state of the pitch.</summary>
    public class ConditionsTests
    {
        private static MatchConfig With(Weather weather, float maintenance = 1f, float wind = 0f, float windAngle = 0f)
        {
            var c = MatchConfig.CreateDefault();
            c.Conditions = new MatchConditions { Weather = weather, Maintenance = maintenance, WindSpeed = wind, WindAngle = windAngle };
            return c;
        }

        private static float RollDistance(MatchConfig c, float speed)
        {
            var b = new BallMotion { Z = Pitch.BallRadius, Vx = speed };
            for (int i = 0; i < 5000 && b.HorizontalSpeed > 0.01f; i++) BallPhysics.Step(ref b, c, c.FixedStep);
            return b.X;
        }

        [Fact]
        public void ADryPerfectPitchInStillAirPlaysExactlyAsBefore()
        {
            var plain = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4100);
            var cloudyNight = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4100, With(Weather.Cloudy));
            cloudyNight.Conditions.Night = true;
            plain.RunToEnd();
            cloudyNight.RunToEnd();
            Assert.Null(plain.Surface);
            Assert.Same(plain.Config, plain.Config.StillAir);
            Assert.Equal(plain.EventLog.Count, cloudyNight.EventLog.Count);
            Assert.Equal(plain.Home.Score, cloudyNight.Home.Score);
            Assert.Equal(plain.Away.Score, cloudyNight.Away.Score);
            Assert.DoesNotContain(plain.EventLog, e => e.Type == MatchEventType.Slip);
        }

        [Fact]
        public void AWetPitchLetsTheBallSkidOnAndSnowStopsIt()
        {
            var dry = With(Weather.Sunny);
            var wet = With(Weather.Rain);
            var snow = With(Weather.Snow);
            dry.ApplyConditions(); wet.ApplyConditions(); snow.ApplyConditions();
            float d = RollDistance(dry, 15f), w = RollDistance(wet, 15f), s = RollDistance(snow, 15f);
            Assert.True(w > d * 1.15f, $"wet {w:0.0} m vs dry {d:0.0} m");
            Assert.True(s < d * 0.75f, $"snow {s:0.0} m vs dry {d:0.0} m");
        }

        [Fact]
        public void TheWindCarriesALoftedBallAndBlowsItSideways()
        {
            float Carry(MatchConfig c, out float drift)
            {
                var b = new BallMotion { Z = Pitch.BallRadius + 0.01f, Vx = 20f, Vz = 12f };
                for (int i = 0; i < 1000; i++)
                {
                    BallPhysics.Step(ref b, c, c.FixedStep);
                    if (b.Z <= Pitch.BallRadius + 0.0005f && i > 5) break;
                }
                drift = b.Y;
                return b.X;
            }
            var still = With(Weather.Sunny); still.ApplyConditions();
            var behind = With(Weather.Sunny, wind: 12f, windAngle: 0f); behind.ApplyConditions();
            var against = With(Weather.Sunny, wind: 12f, windAngle: MathF.PI); against.ApplyConditions();
            var across = With(Weather.Sunny, wind: 12f, windAngle: MathF.PI / 2); across.ApplyConditions();
            float c0 = Carry(still, out float d0), c1 = Carry(behind, out _), c2 = Carry(against, out _);
            Carry(across, out float d3);
            Assert.True(c1 > c0 + 3f, $"with the wind {c1:0.0} m vs {c0:0.0} m");
            Assert.True(c2 < c0 - 3f, $"into the wind {c2:0.0} m vs {c0:0.0} m");
            Assert.True(MathF.Abs(d0) < 0.01f && d3 > 2f, $"crosswind drift {d3:0.0} m");
        }

        [Fact]
        public void PitchUpkeepDecidesTheMudAndRainFillsThePuddles()
        {
            float Share(float[] a) { int n = 0; foreach (var v in a) if (v > 0.3f) n++; return n / (float)a.Length; }
            var perfect = new PitchSurface(new MatchConditions { Weather = Weather.Rain, Maintenance = 1f }, 7);
            var average = new PitchSurface(new MatchConditions { Weather = Weather.Sunny, Maintenance = 0.5f }, 7);
            var neglected = new PitchSurface(new MatchConditions { Weather = Weather.Sunny, Maintenance = 0f }, 7);
            var neglectedWet = new PitchSurface(new MatchConditions { Weather = Weather.Rain, Maintenance = 0f }, 7);
            Assert.False(perfect.HasMud);
            Assert.True(Share(perfect.Water) < 0.01f);
            Assert.False(average.HasWater);
            Assert.True(Share(average.Mud) > 0.01f && Share(neglected.Mud) > Share(average.Mud) * 2f, $"mud {Share(average.Mud):P1} / {Share(neglected.Mud):P1}");
            Assert.InRange(Share(neglectedWet.Water), 0.03f, 0.2f);
            // the goalmouth wears first
            Assert.True(average.MudAt(50f, 0f) > average.MudAt(20f, 25f));
        }

        [Fact]
        public void APuddleStopsARollingBallDead()
        {
            var c = With(Weather.Rain, 0f);
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4200, c);
            var s = e.Surface;
            Assert.NotNull(s);
            // find a deep puddle and a dry spot
            Vec2 puddle = Vec2.Zero, dry = Vec2.Zero;
            bool foundWet = false, foundDry = false;
            for (float x = -45f; x <= 45f && !(foundWet && foundDry); x += 1f)
                for (float y = -28f; y <= 28f; y += 1f)
                {
                    bool wetRow = true, dryRow = true;
                    for (float k = 0f; k <= 4f; k += 1f)
                    {
                        if (s.WaterAt(x + k, y) < 0.6f) wetRow = false;
                        if (s.WaterAt(x + k, y) > 0f || s.MudAt(x + k, y) > 0f) dryRow = false;
                    }
                    if (wetRow && !foundWet) { puddle = new Vec2(x, y); foundWet = true; }
                    if (dryRow && !foundDry) { dry = new Vec2(x, y); foundDry = true; }
                }
            Assert.True(foundWet, "no puddle found");
            Assert.True(foundDry, "no dry spot found");
            float Roll(Vec2 from)
            {
                var b = new BallMotion { X = from.X, Y = from.Y, Z = Pitch.BallRadius, Vx = 6f };
                for (int i = 0; i < 3000 && b.HorizontalSpeed > 0.01f; i++) BallPhysics.Step(ref b, e.Config, e.Config.FixedStep);
                return b.X - from.X;
            }
            float inWater = Roll(puddle), onGrass = Roll(dry);
            Assert.True(inWater < onGrass * 0.4f, $"in the puddle {inWater:0.0} m, on wet grass {onGrass:0.0} m");
        }

        [Fact]
        public void PlayersSlipOnWetAndSnowyPitchesAndEveryConditionPlaysAFullMatch()
        {
            var configs = new[] { With(Weather.Rain, 0f, 12f, 1f), With(Weather.Snow, 0.5f), With(Weather.Rain, 1f), With(Weather.Sunny, 0f, 12f, 2.5f) };
            var engines = new MatchEngine[configs.Length * 2];
            Parallel.For(0, engines.Length, i =>
            {
                engines[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4300 + (ulong)i, configs[i % configs.Length]);
                engines[i].RunToEnd();
            });
            foreach (var e in engines)
            {
                Assert.True(e.IsFinished);
                Assert.Equal(0, e.RecoveredErrors);
                Assert.Equal(0, e.RuleViolations);
            }
            int slips = engines.Where(e => e.Conditions.IsWet || e.Conditions.IsSnow).Sum(e => e.EventLog.Count(x => x.Type == MatchEventType.Slip));
            Assert.True(slips >= 6 * 2, "slips " + slips);
            Assert.Contains(engines.SelectMany(e => e.EventLog), x => x.Type == MatchEventType.Slip && x.Text.Contains("slips on the"));
        }
    }
}
