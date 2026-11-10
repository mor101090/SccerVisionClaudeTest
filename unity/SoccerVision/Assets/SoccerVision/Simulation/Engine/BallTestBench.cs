using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The ball-physics test bench (stage 11): the specification's TESTS 2-14 run on the engine's own physics, each scenario recording the
    /// initial and final velocity, the spin, the flight time, the distance, the bounce height, the energy lost and the collision's response.
    /// TEST 1 is <c>BallSpecTests</c>; TEST 9 (knuckleball) is not built, there is no model for it. Deterministic: the same physics gives the same
    /// table, so docs/BALL_TEST_BENCH.md is regenerated from it (`SimRunner --bench`) and a test fails when the two disagree.
    /// </summary>
    public static class BallTestBench
    {
        /// <summary>One scenario's measurements (NaN: not applicable).</summary>
        public sealed class Row
        {
            public string Test, Case;
            public float SpeedIn, SpeedOut, SpinIn, SpinOut; // m/s; rev/s
            public float FlightTime = float.NaN, Distance = float.NaN, BounceHeight = float.NaN, EnergyLoss = float.NaN; // s; m; m; % of the energy
            public string Response = "";
        }

        private const float R = Pitch.BallRadius;
        private static float Rev(Vec3 w) => w.Length / (2f * MathF.PI);
        private static Vec3 V(BallMotion b) => new Vec3(b.Vx, b.Vy, b.Vz);

        /// <summary>The ball's whole energy (J): motion, spin, height above the grass.</summary>
        private static float Energy(BallMotion b, MatchConfig c)
            => c.Ball.TranslationalEnergy(V(b)) + c.Ball.RotationalEnergy(b.AngularVelocity) + c.Ball.MassKg * c.Gravity * (b.Z - R);

        /// <summary>Flies a ball until it first lands, then on to the top of its first bounce; the flight's measurements.</summary>
        private static Row Flight(string test, string name, BallMotion b, MatchConfig c, bool toApex = true)
        {
            var row = new Row { Test = test, Case = name, SpeedIn = b.Speed, SpinIn = Rev(b.AngularVelocity) };
            float e0 = Energy(b, c), dt = c.FixedStep, t = 0f, x0 = b.X, y0 = b.Y, apex = b.Z;
            bool landed = false;
            for (int i = 0; i < 3000; i++)
            {
                var prev = b;
                BallPhysics.Step(ref b, c, dt);
                t += dt;
                if (!landed)
                {
                    apex = MathF.Max(apex, b.Z);
                    if (b.Landed)
                    {
                        landed = true;
                        row.FlightTime = t;
                        row.Distance = MathF.Sqrt((b.LandX - x0) * (b.LandX - x0) + (b.LandY - y0) * (b.LandY - y0));
                        row.SpeedOut = b.LandSpeed;
                        row.SpinOut = Rev(prev.AngularVelocity);
                        row.Response = $"apex {apex - R:0.00} m, lands {b.LandY - y0:+0.00;-0.00} m off the line, leaves the grass at {b.Speed:0.0} m/s";
                        row.BounceHeight = 0f;
                        if (!toApex) break;
                    }
                }
                else
                {
                    row.BounceHeight = MathF.Max(row.BounceHeight, b.Z - R);
                    if (b.Vz <= 0f) break;
                }
            }
            row.EnergyLoss = 100f * (1f - Energy(b, c) / MathF.Max(1e-6f, e0));
            return row;
        }

        public static List<Row> Run()
        {
            var c = MatchConfig.CreateDefault().StillAir;
            var rows = new List<Row>();

            // TEST 2: vertical drops onto natural dry grass
            foreach (float h in new[] { 0.5f, 1f, 2f, 3f, 5f })
            {
                var b = new BallMotion { Z = R + h };
                var row = Flight("2 Vertical drop", $"from {h:0.0} m", b, c);
                float vIn = MathF.Sqrt(2f * c.Gravity * h);
                row.Response = $"rebound ratio sqrt(h1/h0) {MathF.Sqrt(row.BounceHeight / h):0.000} (the effective e, with the drag)";
                row.SpeedIn = vIn;
                rows.Add(row);
            }

            // TEST 3: repeated bouncing from 2 m: each bounce's height and the share of the energy it keeps
            {
                var b = new BallMotion { Z = R + 2f };
                float prevTop = 2f, t = 0f;
                int n = 0;
                bool rising = false;
                float peak = 0f;
                for (int i = 0; i < 4000 && n < 6; i++)
                {
                    BallPhysics.Step(ref b, c, c.FixedStep);
                    t += c.FixedStep;
                    if (b.Vz > 0f) { rising = true; peak = MathF.Max(peak, b.Z - R); }
                    else if (rising)
                    {
                        rising = false;
                        n++;
                        rows.Add(new Row
                        {
                            Test = "3 Repeated bouncing", Case = $"bounce {n}", SpeedIn = MathF.Sqrt(2f * c.Gravity * prevTop), SpeedOut = MathF.Sqrt(2f * c.Gravity * peak),
                            FlightTime = t, BounceHeight = peak, EnergyLoss = 100f * (1f - peak / prevTop),
                            Response = $"keeps {peak / prevTop:P0} of the height (e {MathF.Sqrt(peak / prevTop):0.000})",
                        });
                        prevTop = peak;
                        peak = 0f;
                        if (prevTop < 0.01f) break;
                    }
                }
            }

            // TEST 4: rolling from a pass's speed until it stops (natural dry grass, normal length)
            {
                // FIFA's ball roll test: off a 1 m, 45 degree ramp the ball reaches the grass at 3.2 m/s (Gabrielsen); "excellent": 4.0-10.0 m
                var fifa = Roll("4 Ground rolling", "FIFA ramp roll (3.2 m/s)", 3.2f, c);
                fifa.Response += "; FIFA \"excellent\" natural pitch: 4.0-10.0 m";
                rows.Add(fifa);
            }
            foreach (float v in new[] { 2f, 5f, 10f, 15f, 20f })
                rows.Add(Roll("4 Ground rolling", $"{v:0} m/s", v, c));

            // TEST 5: a struck ball at several speeds and elevations
            foreach (float v in new[] { 20f, 25f, 30f })
                foreach (float deg in new[] { 5f, 15f, 30f, 45f })
                {
                    float a = deg * MathF.PI / 180f;
                    rows.Add(Flight("5 Powerful shot", $"{v:0} m/s at {deg:0} deg", new BallMotion { Z = R, Vx = v * MathF.Cos(a), Vz = v * MathF.Sin(a), HasWorldSpin = true }, c));
                }

            // TESTS 6-8: 25 m/s at 20 degrees with backspin, topspin, sidespin (spin parameter Sp = R w / v, the quantity the lift depends on)
            float sv = 25f, sa = 20f * MathF.PI / 180f;
            foreach (float sp in new[] { 0f, 0.1f, 0.2f, 0.3f })
            {
                float w = sp * sv / c.Ball.RadiusM;
                rows.Add(Flight("6 Backspin", $"Sp {sp:0.0} ({w / (2f * MathF.PI):0.0} rev/s)", new BallMotion { Z = R, Vx = sv * MathF.Cos(sa), Vz = sv * MathF.Sin(sa), Wy = -w, HasWorldSpin = true }, c));
            }
            foreach (float sp in new[] { 0f, 0.1f, 0.2f, 0.3f })
            {
                float w = sp * sv / c.Ball.RadiusM;
                rows.Add(Flight("7 Topspin", $"Sp {sp:0.0} ({w / (2f * MathF.PI):0.0} rev/s)", new BallMotion { Z = R, Vx = sv * MathF.Cos(sa), Vz = sv * MathF.Sin(sa), Wy = w, HasWorldSpin = true }, c));
            }
            foreach (float rps in new[] { 0f, 4f, 7f, 10f })
            {
                float w = rps * 2f * MathF.PI, cv = 26f, ca = 15f * MathF.PI / 180f;
                rows.Add(Flight("8 Sidespin", $"26 m/s at 15 deg, {rps:0} rev/s", new BallMotion { Z = R, Vx = cv * MathF.Cos(ca), Vz = cv * MathF.Sin(ca), Wz = w, HasWorldSpin = true }, c, toApex: false));
            }

            rows.Add(new Row { Test = "9 Knuckleball", Case = "not built", Response = "no model of the seam-driven side force on a non-spinning ball was found (class E); see stage 3" });

            // TESTS 10-11, 14: the goal frame
            var full = MatchConfig.CreateDefault();
            var engine = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 3, full);
            float postX = Pitch.HalfLength + Pitch.PostRadius, postY = Pitch.GoalHalfWidth + Pitch.PostRadius, top = Pitch.GoalHeight + Pitch.PostRadius;
            foreach (float off in new[] { -0.12f, -0.06f, 0f, 0.06f, 0.12f })
                rows.Add(Frame(engine, full, "10 Goalpost", $"25 m/s square on, {off * 100f:+0;-0;0} cm off its centre", new Vec3(postX - 3f, postY + off, 1f), new Vec3(25f, 0f, 0f)));
            foreach (float deg in new[] { 30f, 60f })
            {
                float a = deg * MathF.PI / 180f;
                rows.Add(Frame(engine, full, "10 Goalpost", $"25 m/s at {deg:0} deg to the goal line, at its centre", new Vec3(postX - 3f * MathF.Cos(a), postY - 3f * MathF.Sin(a), 1f), new Vec3(25f * MathF.Cos(a), 25f * MathF.Sin(a), 0f)));
            }
            foreach (float dz in new[] { -0.12f, -0.06f, 0f, 0.06f, 0.12f })
                rows.Add(Frame(engine, full, "11 Crossbar", $"25 m/s level, {dz * 100f:+0;-0;0} cm off the bar's centre", new Vec3(postX - 0.6f, 0f, top + dz), new Vec3(25f, 0f, 0f)));
            rows.Add(Frame(engine, full, "11 Crossbar", "dropped on top of it at 6 m/s", new Vec3(postX, 0f, top + 1.5f), new Vec3(0f, 0f, -6f)));
            rows.Add(Frame(engine, full, "11 Crossbar", "25 m/s with 8 rev/s topspin, centre", new Vec3(postX - 0.6f, 0f, top), new Vec3(25f, 0f, 0f), new Vec3(0f, 50f, 0f)));

            // TEST 12: the foot and the body. The foot: the fastest ball a striker of each power sends from a still ball, and what an oncoming ball
            // adds; the body: a 20 m/s shot striking a still defender square on and towards his edge
            var kick = full.Kick;
            foreach (int power in new[] { 0, 50, 100 })
            {
                float foot = kick.FootSpeed(power), ball = kick.MaxBallSpeed(power);
                rows.Add(new Row
                {
                    Test = "12 Player impact", Case = $"foot of power {power}: {foot:0.0} m/s", SpeedIn = 0f, SpeedOut = ball,
                    Response = $"ball {ball:0.0} m/s (x{kick.BallFootSpeedRatio:0.00}); a 10 m/s ball coming back at him adds {kick.IncomingBonus(new Vec2(1f, 0f), new Vec2(-10f, 0f)):0.0} m/s",
                });
            }
            foreach (float off in new[] { 0f, 0.4f, 0.8f })
                foreach (float lift in new[] { 0f, 1f })
                    rows.Add(Body(full, $"20 m/s shot, {off:0.0} of the way to his edge, lift {lift:0}", off, lift));

            // TEST 13: the same drop, roll and landing on every ground
            foreach (var g in new[] { GroundModel.NaturalDryShort, GroundModel.NaturalDry, GroundModel.NaturalDryLong, GroundModel.NaturalWet, GroundModel.ArtificialDry, GroundModel.ArtificialWet })
            {
                var gc = MatchConfig.CreateDefault().StillAir;
                gc.Ground = g;
                var drop = Flight("13 Surfaces", $"{g.Name}: drop from 2 m", new BallMotion { Z = R + 2f }, gc);
                drop.Response = $"rebound {drop.BounceHeight:0.00} m";
                rows.Add(drop);
                rows.Add(Roll("13 Surfaces", $"{g.Name}: rolled at 10 m/s", 10f, gc));
                // FIFA's angle ball rebound: fired at 50 km/h, 30 degrees down, under 3 rev/s; speed after / before: 45-70% dry, 45-80% wet (turf)
                float fv = 50f / 3.6f, fa = 30f * MathF.PI / 180f;
                var ab = new BallMotion { Z = R + 0.002f, Vx = fv * MathF.Cos(fa), Vz = -fv * MathF.Sin(fa), HasWorldSpin = true };
                float before = ab.Speed;
                BallPhysics.Step(ref ab, gc, gc.FixedStep);
                bool wet = g == GroundModel.NaturalWet || g == GroundModel.ArtificialWet;
                rows.Add(new Row
                {
                    Test = "13 Surfaces", Case = $"{g.Name}: FIFA angle rebound (50 km/h, 30 deg)", SpeedIn = before, SpeedOut = ab.Speed, SpinOut = Rev(ab.AngularVelocity),
                    EnergyLoss = 100f * (1f - ab.Speed * ab.Speed / (before * before)),
                    Response = $"keeps {ab.Speed / before:P0} of its speed (FIFA turf: {(wet ? "45-80%" : "45-70%")} {(wet ? "wet" : "dry")}), leaves {MathF.Atan2(ab.Vz, ab.HorizontalSpeed) * 180f / MathF.PI:0} deg up",
                });
            }

            // TEST 14: fast shots at the post's centre from 20 phases of the step: how many pass through it
            foreach (float v in new[] { 20f, 30f, 40f, 50f })
            {
                int caught = 0;
                for (int i = 0; i < 20; i++)
                {
                    var start = new BallMotion { X = postX - 6f - v * full.FixedStep * i / 20f, Y = postY, Z = 1f, Vx = v };
                    var b = engine.TestHitFrame(start, 0.6f, out int w);
                    if (w > 0 && b.Vx < 0f) caught++;
                }
                rows.Add(new Row
                {
                    Test = "14 High-speed collision", Case = $"{v:0} m/s at the post, 20 step phases", SpeedIn = v,
                    Response = $"{caught} of 20 caught, {20 - caught} through the post (a step moves it {v * full.FixedStep:0.00} m against a 0.17 m contact distance)",
                });
            }
            return rows;
        }

        private static Row Roll(string test, string name, float v, MatchConfig c)
        {
            var b = new BallMotion { Z = R, Vx = v };
            float e0 = Energy(b, c), t = 0f;
            for (int i = 0; i < 6000 && b.HorizontalSpeed > 0.01f; i++) { BallPhysics.Step(ref b, c, c.FixedStep); t += c.FixedStep; }
            return new Row
            {
                Test = test, Case = name, SpeedIn = v, SpeedOut = b.Speed, SpinIn = Rev(new Vec3(0f, v / c.Ball.RadiusM, 0f)), SpinOut = Rev(b.AngularVelocity),
                FlightTime = t, Distance = b.X, EnergyLoss = 100f * (1f - Energy(b, c) / e0), Response = $"stops after {t:0.0} s, mean deceleration {v / t:0.00} m/s2",
            };
        }

        private static Row Frame(MatchEngine engine, MatchConfig c, string test, string name, Vec3 from, Vec3 v, Vec3 spin = default)
        {
            var start = new BallMotion { X = from.X, Y = from.Y, Z = from.Z, Vx = v.X, Vy = v.Y, Vz = v.Z, Wx = spin.X, Wy = spin.Y, Wz = spin.Z, HasWorldSpin = true };
            var row = new Row { Test = test, Case = name, SpeedIn = start.Speed, SpinIn = Rev(start.AngularVelocity) };
            var b = start;
            int hits = 0;
            float e0 = 0f;
            for (int i = 0; i < 100 && hits == 0; i++)
            {
                var before = b;
                b = engine.TestHitFrame(b, c.FixedStep * 0.5f, out hits);
                if (hits > 0) { e0 = Energy(before, c); row.SpeedIn = before.Speed; row.SpinIn = Rev(before.AngularVelocity); }
            }
            if (hits == 0) { row.Response = "missed the frame"; return row; }
            row.SpeedOut = b.Speed;
            row.SpinOut = Rev(b.AngularVelocity);
            row.EnergyLoss = 100f * (1f - Energy(b, c) / e0);
            var o = V(b);
            float az = MathF.Atan2(o.Y, -o.X) * 180f / MathF.PI, el = MathF.Atan2(o.Z, MathF.Sqrt(o.X * o.X + o.Y * o.Y)) * 180f / MathF.PI;
            var w = b.AngularVelocity;
            row.Response = $"out at {b.Speed / row.SpeedIn:0.00} of its speed, {az:+0;-0;0} deg across, {el:+0;-0;0} deg up; spin ({w.X / 6.2832f:0.0}, {w.Y / 6.2832f:0.0}, {w.Z / 6.2832f:0.0}) rev/s";
            return row;
        }

        private static Row Body(MatchConfig c, string name, float offset, float lift)
        {
            var b = new BallMotion { Z = 0.6f, Vx = 20f, HasWorldSpin = true };
            var n = MatchEngine.BodyContactNormal(new Vec2(b.Vx, b.Vy), Vec2.Zero, offset, lift);
            float e0 = Energy(b, c);
            var before = b;
            BallPhysics.Impact(ref b, c, n, 0.4f, c.Collisions.BodyFriction, Vec3.Zero);
            var o = V(b);
            float az = MathF.Atan2(o.Y, -o.X) * 180f / MathF.PI, el = MathF.Atan2(o.Z, MathF.Sqrt(o.X * o.X + o.Y * o.Y)) * 180f / MathF.PI;
            return new Row
            {
                Test = "12 Player impact", Case = name, SpeedIn = before.Speed, SpeedOut = b.Speed, SpinIn = 0f, SpinOut = Rev(b.AngularVelocity),
                EnergyLoss = 100f * (1f - Energy(b, c) / e0),
                Response = $"e 0.4 (the middle of a block's 0.2-0.55): out at {b.Speed / before.Speed:0.00} of its speed, {az:+0;-0;0} deg across, {el:+0;-0;0} deg up",
            };
        }

        /// <summary>The bench as the Markdown of docs/BALL_TEST_BENCH.md.</summary>
        public static string Markdown(List<Row> rows)
        {
            var ci = CultureInfo.InvariantCulture;
            string F(float v, string fmt) => float.IsNaN(v) ? "" : v.ToString(fmt, ci);
            var sb = new StringBuilder();
            sb.Append("# Ball physics test bench\n\n");
            sb.Append("Generated by `dotnet sim/SoccerVision.SimRunner/bin/Release/net8.0/SoccerVision.SimRunner.dll --bench > docs/BALL_TEST_BENCH.md` from `BallTestBench` (ball physics stage 11):\n");
            sb.Append("the specification's TESTS 2-14 run on the engine's own physics, still air, the reference ball, natural dry grass unless a row says otherwise.\n");
            sb.Append("TEST 1 is `BallSpecTests`. A test (`BallTestBenchTests`) fails when this file and the physics disagree. See `docs/BALL_PHYSICS.md`.\n\n");
            sb.Append("Columns: speed in and out (m/s; for a flight, out = the speed it comes down at; for a collision, in = just before it and out = just after; for a roll, at rest),\n");
            sb.Append("spin in and out (rev/s; a flight's at the moment it lands),\n");
            sb.Append("flight time (s; to the first landing, or the roll's duration), distance (m; to the first landing, or the roll's length), bounce height (m; the top of the first bounce,\n");
            sb.Append("above the grass), energy lost (% of the motion, spin and height energy), and the response.\n");
            string test = null;
            foreach (var r in rows)
            {
                if (r.Test != test)
                {
                    test = r.Test;
                    sb.Append("\n## TEST ").Append(test).Append("\n\n");
                    sb.Append("| Case | Speed in | Speed out | Spin in | Spin out | Flight time | Distance | Bounce height | Energy lost % | Response |\n");
                    sb.Append("|---|---|---|---|---|---|---|---|---|---|\n");
                }
                sb.Append("| ").Append(r.Case)
                  .Append(" | ").Append(F(r.SpeedIn, "0.00")).Append(" | ").Append(F(r.SpeedOut, "0.00"))
                  .Append(" | ").Append(F(r.SpinIn, "0.0")).Append(" | ").Append(F(r.SpinOut, "0.0"))
                  .Append(" | ").Append(F(r.FlightTime, "0.00")).Append(" | ").Append(F(r.Distance, "0.00"))
                  .Append(" | ").Append(F(r.BounceHeight, "0.00")).Append(" | ").Append(F(r.EnergyLoss, "0.0"))
                  .Append(" | ").Append(r.Response.Replace("|", "/")).Append(" |\n");
            }
            return sb.ToString();
        }
    }
}
