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
        /// <summary>The manager's tactics (the sliders and the chosen styles).</summary>
        public TeamTactics Tactics { get => _tactics; set { _tactics = value; Eff = value.Effective(); } }
        private TeamTactics _tactics;
        /// <summary>The tactics as played (the styles applied to the sliders, see <see cref="TeamTactics.Effective"/>): what the AI reads.</summary>
        public TeamTactics Eff { get; private set; }
        public int AttackDir; // +1 attacks the goal at +X
        public int Score;
        public readonly TeamStats Stats = new TeamStats();
        public TeamAgent Opponent;

        // Tactical state maintained by the team AI
        public float LineForward = -33f;     // current back line (local forward)
        /// <summary>Counter-attack tactic: until this sim time the team has just won the ball in its own half and goes forward fast.</summary>
        public float CounterUntil;
        /// <summary>Offside trap: the defender who misses the step up for a moment (the trap fails when someone does).</summary>
        public PlayerAgent TrapBreaker;
        public float TrapBrokenUntil;
        public float LastAttackSeconds;      // sim time of the last possession change in favour of this team
        public PlayerAgent Presser, SecondPresser, Chaser;
        /// <summary>
        /// The holding midfielder ("anchor"): of the central midfielders, the one built for the dirty work (tackling,
        /// positioning, marking, aggression). He screens the back four, wins the ball back, breaks up counter-attacks
        /// (with a foul if he must) and is the first pass out of defence. The other central midfielder runs the game.
        /// </summary>
        public PlayerAgent Anchor
        {
            get
            {
                PlayerAgent best = null;
                int bestScore = int.MinValue;
                foreach (var p in Players)
                {
                    if (p.SentOff || p.IsGoalkeeper || !p.Slot.IsCentralMidfielder) continue;
                    int score = p.Attr.Tackling + p.Attr.Positioning + p.Attr.Marking + p.Attr.Aggression;
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                return best;
            }
        }

        /// <summary>Substitutions made so far (IFAB: at most five in a match).</summary>
        public int SubstitutionsUsed;
        /// <summary>Covering defender: diagonally behind the presser, inside him (pressure - cover - balance).</summary>
        public PlayerAgent Cover;
        public Vec2 CoverSpot;
        /// <summary>Marker who reads a pass to his man and steps in front to intercept it.</summary>
        public PlayerAgent Anticipator;
        /// <summary>Player escorting a ball that is running out of play for our restart, body between it and the chaser.</summary>
        public PlayerAgent Shielder;
        /// <summary>
        /// How well the back line plays as a unit (0..1), from the defenders' teamwork, positioning and decisions:
        /// a high value keeps the line flat, steps up together and leaves attackers offside.
        /// </summary>
        public float DefensiveChemistry
        {
            get
            {
                float sum = 0f;
                int n = 0;
                foreach (var p in Players)
                {
                    if (p.SentOff || p.IsGoalkeeper || p.Slot.Line != Line.Defence) continue;
                    sum += 0.4f * MathUtil.A(p.Attr.Teamwork) + 0.35f * MathUtil.A(p.Attr.Positioning) + 0.25f * MathUtil.A(p.Attr.Decisions);
                    n++;
                }
                // a line missing a man (sent off) is never quite in sync
                return n == 0 ? 0f : sum / n * (n >= 4 ? 1f : 0.8f);
            }
        }
        public readonly List<PlayerAgent> Supporters = new List<PlayerAgent>();
        public int KickOffsTaken;

        public TeamAgent(int index, TeamSetup setup, int attackDir)
        {
            Index = index;
            Setup = setup;
            Formation = setup.Formation;
            Tactics = (setup.Tactics ?? new TeamTactics()).Clone();
            Eff = Tactics.Effective();
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
        public int StandingTackles;
        /// <summary>Dribbles: take-ons (running at a defender to beat him) and how many beat their man.</summary>
        public int Dribbles;
        public int DribblesWon;
        public int SlideTackles;
        public int SlideTacklesWon;
        /// <summary>Times a marker changed the opponent he follows (hand-overs in zonal, switches in man-to-man).</summary>
        public int MarkingSwitches;
        public int Interceptions;
        /// <summary>Injuries: light knocks (played on) and serious ones (the player had to go off).</summary>
        public int LightInjuries;
        public int SeriousInjuries;
        public int Substitutions;
        /// <summary>Interceptions where a marker read the pass to his man and stepped in front of him.</summary>
        public int AnticipatedInterceptions;
        /// <summary>Balls shielded out of play for our own restart (goal kick / throw-in).</summary>
        public int ShieldedOut;
        /// <summary>Shoulder-to-shoulder races for a loose ball won.</summary>
        public int ShoulderDuelsWon;
        /// <summary>Through balls (into space behind the line) and passes into space ahead of a running teammate.</summary>
        public int ThroughBalls;
        public int ThroughBallsCompleted;
        public int PassesIntoSpace;
        /// <summary>Switches of play (a ball to the other flank to relieve pressure) and how many reached a team-mate.</summary>
        public int Switches;
        /// <summary>One-twos: return balls into a team-mate's give-and-go run, and how many reached him.</summary>
        public int OneTwos;
        public int OneTwosCompleted;
        public int SwitchesCompleted;
        /// <summary>Crosses and shots by role (full-backs / wide players).</summary>
        public int FullBackCrosses, WingerCrosses, FullBackShots, WingerShots, CutBacks;
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
