using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Runtime team. Team-local coordinates: Forward = X * AttackDir (own goal line at -52.5),
    /// Lateral = Y * AttackDir (negative = the team's right flank). Formations are written in local space.
    /// </summary>
    public sealed class TeamAgent
    {
        public readonly int Index; // 0 = home, 1 = away
        public readonly TeamSetup Setup;
        public readonly List<PlayerAgent> Players = new List<PlayerAgent>();
        public readonly List<PlayerAgent> Bench = new List<PlayerAgent>();
        public Formation Formation;
        public TeamTactics Tactics;
        public int AttackDir; // +1 attacks the goal at +X
        public int Score;
        public readonly TeamStats Stats = new TeamStats();
        public TeamAgent Opponent;

        // Tactical state maintained by the team AI
        public float LineForward = -33f;     // current back line (local forward)
        public float LastAttackSeconds;      // sim time of the last possession change in favour of this team
        public PlayerAgent Presser, SecondPresser, Chaser;
        public readonly List<PlayerAgent> Supporters = new List<PlayerAgent>();
        public int KickOffsTaken;

        public TeamAgent(int index, TeamSetup setup, int attackDir)
        {
            Index = index;
            Setup = setup;
            Formation = setup.Formation;
            Tactics = (setup.Tactics ?? new TeamTactics()).Clone();
            AttackDir = attackDir;
        }

        public string Name => Setup.Name;
        public string ShortName => Setup.ShortName;

        /// <summary>The goal this team attacks (+1 or -1).</summary>
        public int AttackGoalSign => AttackDir;
        /// <summary>The goal this team defends.</summary>
        public int OwnGoalSign => -AttackDir;
        public Vec2 OwnGoal => Pitch.GoalCentre(OwnGoalSign);
        public Vec2 AttackGoal => Pitch.GoalCentre(AttackGoalSign);

        public Vec2 ToWorld(float forward, float lateral) => new Vec2(forward * AttackDir, lateral * AttackDir);
        public float Forward(Vec2 world) => world.X * AttackDir;
        public float Lateral(Vec2 world) => world.Y * AttackDir;
        public Vec2 ToLocal(Vec2 world) => new Vec2(world.X * AttackDir, world.Y * AttackDir);
        public Vec2 DirToWorld(Vec2 localDir) => new Vec2(localDir.X * AttackDir, localDir.Y * AttackDir);

        public PlayerAgent Goalkeeper
        {
            get
            {
                foreach (var p in Players) if (p.IsGoalkeeper && !p.SentOff) return p;
                return null;
            }
        }

        public IEnumerable<PlayerAgent> Active
        {
            get
            {
                foreach (var p in Players) if (!p.SentOff) yield return p;
            }
        }

        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var p in Players) if (!p.SentOff) n++;
                return n;
            }
        }
    }

    [Serializable]
    public sealed class TeamStats
    {
        public int Goals;
        public int Shots;
        public int ShotsOnTarget;
        public int ShotsBlocked;
        public int Passes;
        public int PassesCompleted;
        public int Crosses;
        public int Tackles;
        public int TacklesWon;
        public int Interceptions;
        public int Fouls;
        public int YellowCards;
        public int RedCards;
        public int Corners;
        public int Offsides;
        public int ThrowIns;
        public int GoalKicks;
        public int FreeKicks;
        public int Penalties;
        public int PenaltiesScored;
        public int Saves;
        /// <summary>Goalkeeper rushes out of the penalty area to reach a loose ball.</summary>
        public int Sorties;
        /// <summary>Shots hit first time from the air.</summary>
        public int Volleys;
        /// <summary>Shots by a wide player after cutting inside.</summary>
        public int CutInShots;
        public int Headers;
        /// <summary>Direct free kicks struck at goal.</summary>
        public int FreeKickShots;
        public int HeaderGoals;
        public int VolleyGoals;
        public int FreeKickGoals;
        /// <summary>Goalkeeper handling: shots and crosses held, shots parried (over, round the post or out), crosses punched.</summary>
        public int KeeperCatches;
        public int KeeperParries;
        public int KeeperPunches;
        public int WoodworkHits;
        public float ExpectedGoals;
        public float PossessionSeconds;
        public float DistanceCoveredKm;

        public TeamStats Clone() => (TeamStats)MemberwiseClone();
    }
}
