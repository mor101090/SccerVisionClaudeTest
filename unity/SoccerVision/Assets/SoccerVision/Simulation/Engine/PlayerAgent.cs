using System;

namespace SoccerVision.Simulation
{
    /// <summary>What a player is currently trying to do (mostly for debugging/visualisation).</summary>
    public enum PlayerTask
    {
        Idle,
        Shape,          // holding the formation position
        Support,        // offering a passing option to the ball carrier
        Run,            // run in behind / overlap
        Press,          // closing down the ball carrier
        Mark,           // goal-side of an opponent
        Chase,          // going for a loose ball
        Receive,        // intended receiver of a pass
        OnBall,         // in possession
        Goalkeeping,    // positioning, saving, claiming
        GoalkeeperHold, // goalkeeper holding the ball in the hands
        SetPiece,       // walking to a restart position
        Celebrate,
        Leave,          // sent off, walking off the pitch
    }

    /// <summary>Runtime state of one player on the pitch.</summary>
    public sealed class PlayerAgent
    {
        public readonly int Index;          // 0..21, unique in the match
        public readonly PlayerProfile Profile;
        public readonly TeamAgent Team;
        public PlayerAttributes Attr => Profile.Attributes;

        public int SlotIndex;
        public bool IsGoalkeeper;

        // Kinematics (world metres / seconds). Prev* values are for render interpolation.
        public Vec2 Pos, PrevPos, Vel;
        public float Facing, PrevFacing;
        public float JumpZ, PrevJumpZ, JumpVz;

        // Condition and discipline
        public float Energy = 1f;
        public bool SentOff;
        public int YellowCards;
        public int FoulsCommitted;

        // AI intent
        public PlayerTask Task = PlayerTask.Shape;
        public Vec2 Target;
        /// <summary>0 = walk, 0.5 = jog, 1 = sprint.</summary>
        public float Urgency;
        public Vec2 ShapeTarget;
        public bool FaceBall = true;

        // Timers (sim seconds)
        public float TouchCooldown;
        public float TackleCooldown;
        public float StunTimer;
        public float DecisionTimer;
        public float KickCooldown;
        public float RunTimer;
        public float RunCooldown;
        public float DiveTimer;

        // Interception cache
        public int InterceptVersion = -1;
        public float InterceptRecalc;
        public Vec2 InterceptPoint;
        public float InterceptTime = float.PositiveInfinity;

        public Vec2 SupportSpot;
        /// <summary>Run in behind: (overshoot beyond the offside line, lateral) in team-local metres.</summary>
        public Vec2 RunPlan;
        public float NoisePhaseA, NoisePhaseB;

        // A kick the player is turning his body for (executed when the turn timer ends).
        internal KickPlan PendingPlan;
        internal bool HasPendingPlan;
        public float HoldTime;
        public float LastSortieTime = -100f;
        /// <summary>Sim time until which this player is on a cut-inside run (a shot then counts as one).</summary>
        public float CutInUntil = -1f;
        public PlayerAgent MarkTarget;

        // Per-match stats
        public int Touches, PassesAttempted, PassesCompleted, Shots, Goals, Tackles, Saves;
        public float DistanceCovered;

        public PlayerAgent(int index, PlayerProfile profile, TeamAgent team, int slotIndex)
        {
            Index = index;
            Profile = profile;
            Team = team;
            SlotIndex = slotIndex;
            IsGoalkeeper = slotIndex == 0;
            switch (profile.Attributes.Fitness)
            {
                case FitnessLevel.Low: Energy = 0.88f; break;
                case FitnessLevel.High: Energy = 1f; break;
                default: Energy = 0.96f; break;
            }
        }

        public FormationSlot Slot => Team.Formation.Slots[SlotIndex];
        public bool IsActive => !SentOff;
        public int Number => Profile.ShirtNumber;
        public Vec2 FacingDir => Vec2.FromAngle(Facing);

        // ---- Physical capabilities ------------------------------------------------------------------

        /// <summary>0.86..1 multiplier from fatigue; tired players are slower and sloppier (design doc).</summary>
        public float FatigueFactor => 0.86f + 0.14f * MathF.Pow(MathUtil.Clamp01(Energy), 0.8f);

        /// <summary>Top sprint speed: 7.0..9.6 m/s (25-35 km/h, elite tracking data range).</summary>
        public float MaxSpeed => (7.0f + 2.6f * MathUtil.A(Attr.Speed)) * FatigueFactor * (IsGoalkeeper ? 0.93f : 1f);

        public float MaxAccel => (3.2f + 2.6f * MathUtil.A(Attr.Acceleration)) * (0.9f + 0.1f * FatigueFactor);

        public float MaxDecel => 6.5f + 1.5f * MathUtil.A(Attr.Acceleration);

        public float DribbleSpeed => MaxSpeed * (0.72f + 0.16f * MathUtil.A(Attr.Dribbling));

        /// <summary>Reaction time used by interception and pressure maths (s).</summary>
        public float ReactionTime => 0.34f - 0.14f * MathUtil.A((Attr.Decisions + Attr.Positioning) / 2);

        /// <summary>Horizontal radius in which the player can touch a ball at foot height.</summary>
        public float FootReach => 0.75f;

        /// <summary>Highest ball centre the player can head (with a jump).</summary>
        public float HeadReach => 2.15f + 0.55f * MathUtil.A(Attr.Jumping) + (Profile.HeightCm - 180) * 0.005f;

        public float GoalkeeperReach => 2.55f + 0.35f * MathUtil.A(Attr.Jumping) + (Profile.HeightCm - 185) * 0.006f;

        /// <summary>Accuracy multiplier (&gt;= 1 means larger errors) from fatigue.</summary>
        public float FatigueError => 1f + (1f - MathUtil.Clamp01(Energy)) * 0.6f;

        /// <summary>
        /// Time (s) to reach a point, with the classic pitch-control assumptions: keep the current velocity
        /// for the reaction time, then accelerate towards the target up to top speed.
        /// </summary>
        public float TimeToReach(Vec2 point, float extraReaction = 0f)
        {
            float react = ReactionTime + extraReaction;
            if (StunTimer > 0f) react += StunTimer;
            Vec2 start = Pos + Vel * MathF.Min(react, 0.25f);
            Vec2 delta = point - start;
            float dist = delta.Length - 0.4f;
            if (dist <= 0f) return react * 0.5f;
            float vmax = MaxSpeed;
            float a = MaxAccel;
            float along = dist > 1e-4f ? Vec2.Dot(Vel, delta / delta.Length) : 0f;
            along = MathUtil.Clamp(along, 0f, vmax);
            float accelPenalty = (vmax - along) * (vmax - along) / (2f * a * vmax);
            return react + dist / vmax + accelPenalty;
        }

        public string Label => Profile.Label;

        public override string ToString() => Team.ShortName + " " + Profile.Label;
    }
}
