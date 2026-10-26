namespace SoccerVision.Simulation
{
    public enum KickKind
    {
        None,
        Pass,
        ThroughBall,
        LongPass,
        Cross,
        Shot,
        Header,
        Clearance,
        Deflection,
        GoalkeeperDistribution,
        Restart,
    }

    /// <summary>The ball: free physics body or attached to a player (feet or goalkeeper's hands).</summary>
    public sealed class BallState
    {
        public BallMotion M;
        public Vec2 PrevPos;
        public float PrevZ = Pitch.BallRadius;

        /// <summary>Player in control (dribbling or holding). Null while the ball is free.</summary>
        public PlayerAgent Owner;
        public bool InHands;

        public PlayerAgent LastToucher;
        public TeamAgent LastTouchTeam;
        public PlayerAgent LastKicker;
        public KickKind LastKickKind;
        public float LastKickTime;
        /// <summary>Law 13: the restart taker may not touch the ball again until another player has.</summary>
        public bool KickerMustWait;
        /// <summary>Law 12: the goalkeeper may not handle a ball deliberately kicked to him by a team-mate.</summary>
        public TeamAgent HandlingForbiddenFor;

        public PlayerAgent IntendedReceiver;
        public Vec2 IntendedTarget;
        public PlayerAgent PendingPassFrom;
        public PlayerAgent LastPasser;
        public float LastPassTime;

        public bool IsShot;
        public bool ShotOnTarget;
        public PlayerAgent Shooter;
        public float ShotSpeed;
        public float ShotGoalkeeperDistance;

        public bool InGoal;

        public Vec2 Pos => M.Position2D;
        public float Z => M.Z;
        public Vec2 Velocity => M.Velocity2D;
        public bool IsFree => Owner == null;

        public void Place(Vec2 p, float z = Pitch.BallRadius)
        {
            M = new BallMotion { X = p.X, Y = p.Y, Z = z < Pitch.BallRadius ? Pitch.BallRadius : z };
            PrevPos = p;
            PrevZ = M.Z;
        }

        public void Stop()
        {
            M.Vx = 0f;
            M.Vy = 0f;
            M.Vz = 0f;
            M.SideSpin = 0f;
            M.TopSpin = 0f;
        }

        public void ClearFlight()
        {
            IntendedReceiver = null;
            IsShot = false;
            ShotOnTarget = false;
            Shooter = null;
        }
    }

    /// <summary>Referee or assistant referee (moves with play, never touches the ball).</summary>
    public sealed class Official
    {
        public Vec2 Pos, PrevPos, Vel;
        public float Facing;
        public float FlagTimer;
        public bool IsAssistant;
        /// <summary>Touchline side for assistants (+1 or -1 in Y); assistants cover the half X*CoverSign &gt; 0.</summary>
        public int TouchlineSign;
        public int CoverSign;
        public bool FlagRaised => FlagTimer > 0f;
        /// <summary>Assistant stands still in line with the offence while the flag is up (offside).</summary>
        public bool HoldPosition;
        public float HoldX;
    }
}
