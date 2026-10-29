using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// All tunable numbers of a match. Defaults follow the design document (10 real minutes per half,
    /// 2 minute break) and the research notes in docs/RESEARCH.md.
    ///
    /// Three time bases are used:
    ///  - real seconds: wall-clock time of the viewer (half = 600 s, break = 120 s),
    ///  - sim seconds:  physics time players and ball move in (real * MotionSpeed),
    ///  - clock seconds: the match clock on the scoreboard (45:00 per half).
    /// </summary>
    [Serializable]
    public sealed class MatchConfig
    {
        // ---- Time -------------------------------------------------------------------------------------
        /// <summary>Real seconds for 45 clock minutes (added time comes on top).</summary>
        public float HalfRealSeconds = 600f;
        /// <summary>Real seconds of the half-time break.</summary>
        public float HalftimeRealSeconds = 120f;
        public float RegulationHalfMinutes = 45f;
        /// <summary>
        /// Sim seconds played per real second. 1 = players move at true speed; above 1 looks like a gently
        /// fast-forwarded broadcast and packs more football into the 10 real minutes.
        /// </summary>
        public float MotionSpeed = 2.025f; // 90% of the original 2.25: the "right" match pace (playtested)
        /// <summary>Fixed physics step (sim seconds).</summary>
        public float FixedStep = 0.02f;

        /// <summary>Minimum added time (minutes) shown by the fourth official.</summary>
        public int MinAddedMinutesFirstHalf = 1;
        public int MinAddedMinutesSecondHalf = 3;
        public int MaxAddedMinutes = 10;
        /// <summary>IFAB Law 3: up to five substitutes (competition rules).</summary>
        public int MaxSubstitutions = 5;
        /// <summary>Home advantage: 0 none (neutral ground), 1 as in the top leagues (a third of a goal, kinder whistle, fresher legs at home).</summary>
        public float HomeAdvantage = 1f;
        /// <summary>How much goals and chances swing the momentum of the match (0: none).</summary>
        public float MomentumStrength = 1f;
        /// <summary>Time constant of momentum in match minutes (it fades with time).</summary>
        public float MomentumMinutes = 8f;
        /// <summary>IFAB Law 3: the five changes in at most three opportunities during play (half-time does not count).</summary>
        public int MaxSubstitutionWindows = 3;
        /// <summary>Sim seconds a seriously injured player is treated on the pitch before he is helped off.</summary>
        public float InjuryTreatmentSeconds = 9f;
        /// <summary>
        /// After added time expires the referee waits for a neutral moment, at most this many clock seconds
        /// (an attack in progress is allowed to finish).
        /// </summary>
        public float EndOfHalfGraceClockSeconds = 30f;

        // ---- Rules ------------------------------------------------------------------------------------
        /// <summary>Design document rule: the kick-off taker plays the ball backwards to a team-mate
        /// (IFAB allows any direction since 2016; set false for the pure Law 8 behaviour).</summary>
        public bool KickOffMustGoBackwards = true;
        /// <summary>Law 12 (2025/26): goalkeeper may hold the ball for 8 seconds, otherwise a corner kick.</summary>
        public float GoalkeeperHoldLimitSeconds = 8f;
        public bool AllowAdvantage = true;

        // ---- Set piece pacing (sim seconds; 1 sim second = 2 clock seconds) ------------------------------
        // Minimum time before a restart; it is also held until (nearly) everyone is back in position. Real
        // broadcasts show roughly: throw-in 10-20 s, goal kick 20-30 s, corner 25-40 s, free kick 15-40 s,
        // goal to kick-off 45-75 s, penalty 60-90 s of match clock.
        public float GoalCelebrationSeconds = 12f;
        public float KickOffSetupSeconds = 8f;
        public float ThrowInSetupSeconds = 5.5f;
        public float GoalKickSetupSeconds = 10f;
        public float CornerSetupSeconds = 12f;
        public float FreeKickSetupSeconds = 7f;
        public float FreeKickWithWallSetupSeconds = 15f;
        public float PenaltySetupSeconds = 18f;
        public float CardShowSeconds = 3f;
        /// <summary>Real seconds between the referee's whistle and the kick at kick-offs, penalties and ceremonial free kicks.</summary>
        public float WhistleToKickRealSeconds = 1.5f;
        /// <summary>Pause after the ball goes out, a foul or an offside, before the restart is set up (real seconds; the clock waits).</summary>
        public float RestartPauseRealSeconds = 1.5f;
        /// <summary>Real seconds an assistant keeps the flag raised for offside (standing in line with the offence).</summary>
        public float OffsideFlagRealSeconds = 4f;
        /// <summary>Minimum real seconds between the offside whistle and the indirect free kick, so viewers see what happened.</summary>
        public float OffsideRestartRealSeconds = 4.5f;
        /// <summary>Upper bound for any restart; after it the restart is forced so a match can never stall.</summary>
        public float MaxSetupSeconds = 40f;

        // ---- Ball physics (SI units) ------------------------------------------------------------------
        public float Gravity = 9.81f;
        /// <summary>0.5*rho*Cd*A/m with rho=1.2, Cd=0.25, r=0.11 m, m=0.43 kg.</summary>
        public float AirDragK = 0.0133f;
        /// <summary>Constant rolling resistance on grass (m/s^2). Natural turf roll tests: 0.45-1.5 m/s^2.</summary>
        public float RollingDecel = 0.9f;
        /// <summary>Speed proportional grass drag while rolling (1/s).</summary>
        public float GrassLinearDrag = 0.14f;
        /// <summary>Vertical coefficient of restitution of a ball on natural grass.</summary>
        public float GroundRestitution = 0.55f;
        /// <summary>Horizontal speed retained on each bounce.</summary>
        public float BounceFriction = 0.86f;
        public float PostRestitution = 0.7f;
        /// <summary>Magnus coefficient: lateral acceleration = k * spin * speed.</summary>
        public float MagnusK = 0.0042f;
        public float SpinDecayPerSecond = 0.25f;
        public float NetRestitution = 0.12f;

        // ---- Staged match -----------------------------------------------------------------------------
        /// <summary>Incidents forced at fixed moments (null: an ordinary match). See <see cref="MatchScript"/>.</summary>
        public MatchScript Script;

        // ---- Weather and pitch ------------------------------------------------------------------------
        /// <summary>Weather, wind and the state of the pitch (see <see cref="MatchConditions"/>).</summary>
        public MatchConditions Conditions = new MatchConditions();
        /// <summary>Wind velocity (m/s, world axes) the ball's air drag works against. Set by the engine from the conditions.</summary>
        [NonSerialized] public float WindX, WindY;
        /// <summary>Mud and standing water under a rolling or bouncing ball (null: a uniform pitch). Set by the engine.</summary>
        [NonSerialized] public PitchSurface Surface;
        [NonSerialized] private MatchConfig _still;

        /// <summary>
        /// The same physics in still air on a uniform pitch: what the kick solvers aim with (they work in the kick's own
        /// frame, where a world wind direction means nothing). Kicks then allow for the wind separately.
        /// </summary>
        public MatchConfig StillAir
        {
            get
            {
                if (WindX == 0f && WindY == 0f && Surface == null) return this;
                if (_still == null)
                {
                    _still = (MatchConfig)MemberwiseClone();
                    _still.WindX = _still.WindY = 0f;
                    _still.Surface = null;
                    _still._still = null;
                }
                return _still;
            }
        }

        /// <summary>
        /// Turns the weather into ball physics. Rain: the ball skids on (less rolling resistance, a lower, skidding bounce).
        /// Snow: it dies in the snow. A neglected pitch has longer, uneven grass (a little more resistance everywhere).
        /// Local mud and water come on top through <see cref="Surface"/>. Nothing changes for a dry, perfect pitch.
        /// </summary>
        public void ApplyConditions()
        {
            var c = Conditions;
            if (c == null) return;
            c.Sanitize();
            if (c.IsWet)
            {
                RollingDecel *= 0.65f;
                GrassLinearDrag *= 0.75f;
                GroundRestitution *= 0.82f;
                BounceFriction = 0.93f;
            }
            else if (c.IsSnow)
            {
                RollingDecel = 1.8f;
                GrassLinearDrag = 0.35f;
                GroundRestitution = 0.3f;
                BounceFriction = 0.72f;
            }
            RollingDecel += 0.35f * (1f - c.Maintenance);
            Vec2 w = c.Wind;
            WindX = w.X;
            WindY = w.Y;
            _still = null;
        }

        // ---- Derived ---------------------------------------------------------------------------------
        public float RegulationHalfClockSeconds => RegulationHalfMinutes * 60f;

        /// <summary>Clock seconds per real second (4.5 by default: 45 minutes in 10 real minutes).</summary>
        public float ClockSecondsPerRealSecond => RegulationHalfClockSeconds / HalfRealSeconds;

        /// <summary>Clock seconds per sim second.</summary>
        public float ClockSecondsPerSimSecond => ClockSecondsPerRealSecond / MotionSpeed;

        public static MatchConfig CreateDefault() => new MatchConfig();

        public MatchConfig Clone()
        {
            var c = (MatchConfig)MemberwiseClone();
            c.Conditions = (Conditions ?? new MatchConditions()).Clone();
            c._still = null;
            return c;
        }

        public void Validate()
        {
            if (HalfRealSeconds <= 0f) throw new ArgumentException("HalfRealSeconds must be positive");
            if (HalftimeRealSeconds < 0f) throw new ArgumentException("HalftimeRealSeconds must be >= 0");
            if (MotionSpeed <= 0f) throw new ArgumentException("MotionSpeed must be positive");
            if (FixedStep <= 0f || FixedStep > 0.05f) throw new ArgumentException("FixedStep must be in (0, 0.05]");
            if (RegulationHalfMinutes <= 0f) throw new ArgumentException("RegulationHalfMinutes must be positive");
        }
    }
}
