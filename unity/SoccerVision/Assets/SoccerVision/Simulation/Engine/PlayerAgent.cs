using System;

namespace SoccerVision.Simulation
{
    /// <summary>What a player is currently trying to do (mostly for debugging/visualisation).</summary>
    public enum InjuryLevel { None, Light, Serious }

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
        Cover,          // covering diagonally behind the presser
        Shield,         // body between an opponent and a ball running out of play
    }

    /// <summary>Runtime state of one player on the pitch.</summary>
    public sealed class PlayerAgent
    {
        public readonly int Index;          // 0..21, unique in the match
        /// <summary>Who plays in this shirt now (a substitute takes over the agent of the player he replaces).</summary>
        public PlayerProfile Profile;
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
        /// <summary>Time before this player can be in another shoulder-to-shoulder duel.</summary>
        public float DuelCooldown;
        /// <summary>Sim time at which this player last read a pass and went to intercept it.</summary>
        public float AnticipatedAt = -100f;
        public float StunTimer;
        public float DecisionTimer;
        /// <summary>Keeper's run-up before a long kick from the hands: 0 none, 1 walking back, 2 running up.</summary>
        public int RunUpPhase;
        /// <summary>Sim seconds left to show the offside flag above this player (he was caught offside).</summary>
        public float OffsideMarkTimer;
        public Vec2 RunUpFrom;
        public float RunUpTimer;
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
        /// <summary>The opponent this player marks right now (either system).</summary>
        public PlayerAgent MarkTarget;
        /// <summary>Man-to-man: the opponent given to this player for the whole defensive phase (kept until possession changes).</summary>
        public PlayerAgent ManAssignment;
        internal PlayerAgent PrevMarkTarget;

        // Sliding tackle: a short lunge along SlideDir, then a moment on the ground.
        public float SlideTimer;
        public float GroundTimer;
        public Vec2 SlideDir;
        public float SlideSpeed;
        public PlayerAgent SlideVictim;
        public bool SlideResolved;
        public bool IsDown => SlideTimer > 0f || GroundTimer > 0f || (Injury == InjuryLevel.Serious && !LeavingInjured);

        // Injuries: a light knock (plays on, a little slower and sloppier) or a serious one (treated, then substituted).
        public InjuryLevel Injury;
        /// <summary>Treatment time left for a seriously injured player lying on the pitch.</summary>
        public float TreatmentTimer;
        /// <summary>A seriously injured player walking off to be replaced.</summary>
        public bool LeavingInjured;
        /// <summary>Off the pitch injured with no substitute available (the team plays on a man short).</summary>
        public bool InjuredOff;
        /// <summary>Speed / control penalty of a light injury.</summary>
        public float InjuryFactor => Injury == InjuryLevel.Light ? 0.9f : 1f;

        // Per-match stats
        public int Touches, PassesAttempted, PassesCompleted, Shots, Goals, Tackles, SlideTackles, Saves, Interceptions;
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
        /// <summary>Taller players are a little slower over the ground (-0.12% per cm above 180), shorter ones a little quicker.</summary>
        public float HeightAgility => 1f - MathUtil.Clamp((Profile.HeightCm - 180) * 0.0012f, -0.025f, 0.03f);
        public float MaxSpeed => (7.0f + 2.6f * MathUtil.A(Attr.Speed)) * FatigueFactor * HeightAgility * InjuryFactor * (IsGoalkeeper ? 0.93f : 1f);

        public float MaxAccel => (3.2f + 2.6f * MathUtil.A(Attr.Acceleration)) * (0.9f + 0.1f * FatigueFactor) * (0.5f + 0.5f * InjuryFactor);

        public float MaxDecel => 6.5f + 1.5f * MathUtil.A(Attr.Acceleration);

        public float DribbleSpeed => MaxSpeed * (0.72f + 0.16f * MathUtil.A(Attr.Dribbling));

        /// <summary>Reaction time used by interception and pressure maths (s).</summary>
        public float ReactionTime => 0.34f - 0.14f * MathUtil.A((Attr.Decisions + Attr.Positioning) / 2);

        /// <summary>Horizontal radius in which the player can touch a ball at foot height.</summary>
        public float FootReach => 0.75f;

        /// <summary>Highest ball centre the player can head (with a jump).</summary>
        public float HeadReach => 2.15f + 0.55f * MathUtil.A(Attr.Jumping) + (Profile.HeightCm - 180) * 0.009f;

        public float GoalkeeperReach => 2.55f + 0.35f * MathUtil.A(Attr.Jumping) + (Profile.HeightCm - 185) * 0.006f;

        /// <summary>Accuracy multiplier (&gt;= 1 means larger errors) from fatigue.</summary>
        public float FatigueError => (1f + (1f - MathUtil.Clamp01(Energy)) * 0.6f) * (Injury == InjuryLevel.Light ? 1.15f : 1f);

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

        /// <summary>A substitute takes over this shirt slot: fresh legs, no cards or injuries, his own stats.</summary>
        internal void Substitute(PlayerProfile incoming)
        {
            Profile = incoming;
            Energy = incoming.Attributes.Fitness == FitnessLevel.Low ? 0.9f : 1f;
            Injury = InjuryLevel.None;
            TreatmentTimer = 0f;
            LeavingInjured = false;
            YellowCards = 0;
            FoulsCommitted = 0;
            StunTimer = 0f;
            GroundTimer = 0f;
            SlideTimer = 0f;
            TackleCooldown = 0f;
            DuelCooldown = 0f;
            MarkTarget = null;
            ManAssignment = null;
            HasPendingPlan = false;
            RunTimer = 0f;
            Task = PlayerTask.Shape;
            Touches = PassesAttempted = PassesCompleted = Shots = Goals = Tackles = SlideTackles = Saves = Interceptions = 0;
            DistanceCovered = 0f;
        }

        public override string ToString() => Team.ShortName + " " + Profile.Label;
    }
}
