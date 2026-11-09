using System;

namespace SoccerVision.Simulation
{
    public sealed partial class MatchEngine
    {
        // Ball physics stage 9: every contact (a bounce, a kick, the frame, a body, a catch excepted) shows as a jump in the ball's velocity
        // between two steps far larger than flight alone gives. The engine notes its size and direction, so the viewers can draw the ball
        // flattened against what it hit. It only reads the ball: the match is not changed.
        private Vec3 _impactPrevVelocity;
        private bool _impactPrevValid, _impactPrevHands;

        /// <summary>The sim time of the latest contact (-1: none yet).</summary>
        public float LastImpactTime { get; private set; } = -1f;
        /// <summary>The latest contact's direction: the way it pushed the ball (unit, world axes; the contact normal for a frictionless hit).</summary>
        public Vec3 LastImpactNormal { get; private set; }
        /// <summary>The latest contact's peak flattening, m (see <see cref="DeformationModel.Compression"/>).</summary>
        public float LastImpactCompression { get; private set; }
        /// <summary>The latest contact's velocity change, m/s.</summary>
        public float LastImpactVelocityChange { get; private set; }

        private void TrackImpact()
        {
            var d = Config.Deformation;
            if (d == null) return;
            var v = new Vec3(Ball.M.Vx, Ball.M.Vy, Ball.M.Vz);
            bool hands = Ball.Owner != null && Ball.InHands;
            bool live = Phase == MatchPhase.Live;
            // (into or out of the keeper's hands is a catch or a throw: soft hands, no measurement - not shown)
            if (_impactPrevValid && live && !hands && !_impactPrevHands)
            {
                var dv = v - _impactPrevVelocity;
                float size = dv.Length;
                if (size > d.ContactThreshold)
                {
                    LastImpactTime = SimTime;
                    LastImpactNormal = dv * (1f / size);
                    LastImpactVelocityChange = size;
                    LastImpactCompression = d.Compression(Config.Ball, size);
                    if (CollectDiagnostics) { Count("impact.contact"); Diagnostics.TryGetValue("impact.compressionMm", out int mm); Diagnostics["impact.compressionMm"] = mm + (int)MathF.Round(LastImpactCompression * 1000f); }
                }
            }
            _impactPrevVelocity = v;
            _impactPrevValid = live;
            _impactPrevHands = hands;
        }
    }
}
