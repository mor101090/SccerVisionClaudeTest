using System;
using SoccerVision.Replay;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 9: deformation. The ball is a gas spring (k = 2 pi R P) bent to the measured impacts of Iga, Nunome, Inoue and Ikegami
    /// (a match ball at 0.9 bar fired at a rigid force plate); the engine notes each contact's flattening and the replay frame carries it to the
    /// viewers. See docs/BALL_PHYSICS.md.
    /// </summary>
    public class BallDeformationTests
    {
        private static readonly DeformationModel D = DeformationModel.Reference;
        // the measured ball: a size 5 Molten Pelada 405, 22 cm across, 426 g, 0.9 bar
        private static readonly BallSpec Iga = new BallSpec(0.426f, 0.22f * MathF.PI, 0.9e5f, BallSpec.Reference.InertiaFactor);
        private static readonly float[] VIn = { 9.05f, 11.36f, 13.68f, 16.27f, 19.46f };
        private static readonly float[] VOut = { 7.21f, 8.90f, 10.56f, 12.25f, 14.40f };
        private static readonly float[] Flattening = { 0.0245f, 0.0313f, 0.0357f, 0.0403f, 0.0464f };
        private static readonly float[] Contact = { 0.0085f, 0.0083f, 0.0079f, 0.0077f, 0.0074f };

        [Fact]
        public void TheGasSpringGivesTheMeasuredContactTime()
        {
            // pi sqrt(m / 2 pi R P): 8.2 ms for the measured ball (measured 7.4-8.5 ms), 8.45 ms for the reference ball
            float iga = DeformationModel.LinearContactTime(Iga), reference = DeformationModel.LinearContactTime(BallSpec.Reference);
            Assert.InRange(iga, 0.0081f, 0.0083f);
            Assert.InRange(reference, 0.0084f, 0.0085f);
            // with the measured shortening, every one of the five impacts within 0.3 ms
            for (int i = 0; i < VIn.Length; i++) Assert.InRange(D.ContactTime(Iga, VIn[i]), Contact[i] - 0.0003f, Contact[i] + 0.0003f);
        }

        [Fact]
        public void TheFlatteningIsTheMeasuredOne()
        {
            // the five measured impacts, from their velocity changes (in + out), each within 1.5 mm
            for (int i = 0; i < VIn.Length; i++)
                Assert.InRange(D.Compression(Iga, VIn[i] + VOut[i]), Flattening[i] - 0.0015f, Flattening[i] + 0.0015f);
            // nothing for no change, capped for an impossible one
            Assert.Equal(0f, D.Compression(Iga, 0f));
            Assert.Equal(D.MaxCompressionFraction * Iga.RadiusM, D.Compression(Iga, 500f), 4);
        }

        [Fact]
        public void AHarderBallFlattensLessAndTouchesShorter()
        {
            var soft = new BallSpec(0.43f, 0.69f, BallSpec.IfabMinGaugePressurePa, BallSpec.Reference.InertiaFactor);
            var hard = new BallSpec(0.43f, 0.69f, BallSpec.IfabMaxGaugePressurePa, BallSpec.Reference.InertiaFactor);
            Assert.True(DeformationModel.Stiffness(hard) > DeformationModel.Stiffness(soft));
            Assert.True(DeformationModel.LinearContactTime(hard) < DeformationModel.LinearContactTime(soft));
            Assert.True(D.Compression(hard, 30f) < D.Compression(soft, 30f));
        }

        [Fact]
        public void TheFrameLosesWhatTheMeasuredBallLost()
        {
            // the measured restitution falls 0.797 -> 0.740 from 9.05 to 19.46 m/s; the frame's slope gives the same fall within 0.005
            var cm = CollisionModel.Reference;
            float fall = cm.FrameRestitutionAt(VIn[0]) - cm.FrameRestitutionAt(VIn[4]);
            float measured = VOut[0] / VIn[0] - VOut[4] / VIn[4];
            Assert.InRange(fall, measured - 0.005f, measured + 0.005f);
        }

        [Fact]
        public void TheEngineNotesTheContactsAndTheReplayCarriesThem()
        {
            var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4243, MatchConfig.CreateDefault());
            var rec = new ReplayRecorder(e, 10);
            int n = ReplayRecorder.FrameLength, frames = 0, contacts = 0, sideways = 0;
            float deepest = 0f;
            for (int i = 0; i < 3000 && !e.IsFinished; i++)
            {
                float before = e.LastImpactTime;
                var chunk = rec.Step(1);
                if (chunk.FrameCount != 1) continue;
                frames++;
                int mm = chunk.Frames[n - 4];
                if (e.LastImpactTime > before)
                {
                    // the frame carries the latest contact: its flattening and its direction (a unit vector)
                    Assert.Equal((int)MathF.Round(e.LastImpactCompression * 1000f), mm);
                    float nx = chunk.Frames[n - 3] / 100f, ny = chunk.Frames[n - 2] / 100f, nz = chunk.Frames[n - 1] / 100f;
                    if (mm > 0) Assert.InRange(MathF.Sqrt(nx * nx + ny * ny + nz * nz), 0.97f, 1.03f);
                    contacts++;
                    if (MathF.Abs(nx) + MathF.Abs(ny) > 0.7f) sideways++;
                    deepest = MathF.Max(deepest, e.LastImpactCompression);
                }
                else Assert.Equal(0, mm);
            }
            Assert.True(contacts > frames / 50, $"contacts {contacts} in {frames} frames");
            Assert.True(sideways > contacts / 3, "kicks and traps push the ball sideways: " + sideways + " of " + contacts);
            // a shot of about 30 m/s: up to the measured range's top and a little beyond, never past the cap
            Assert.InRange(deepest, 0.03f, D.MaxCompressionFraction * e.Config.Ball.RadiusM + 1e-4f);
        }

        [Fact]
        public void TheDeformationDoesNotChangeTheMatch()
        {
            static MatchEngine Run(bool deform)
            {
                var c = MatchConfig.CreateDefault();
                if (!deform) c.Deformation = null;
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 12, c);
                e.RunToEnd();
                return e;
            }
            var a = Run(true);
            var b = Run(false);
            Assert.Equal(a.Home.Score, b.Home.Score);
            Assert.Equal(a.Away.Score, b.Away.Score);
            Assert.Equal(a.EventLog.Count, b.EventLog.Count);
            Assert.True(a.LastImpactTime > 0f);
            Assert.Equal(-1f, b.LastImpactTime);
        }
    }
}
