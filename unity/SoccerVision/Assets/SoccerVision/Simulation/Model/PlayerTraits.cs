using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// A player's character on the ball: habits that shape his style of play (about three per player, never two that
    /// contradict each other). They tip his choices - they do not change his skills.
    /// </summary>
    [Flags]
    public enum PlayerTrait
    {
        None = 0,
        /// <summary>Likes to cut inside and shoot.</summary>
        CutsInsideToShoot = 1 << 0,
        /// <summary>Likes to dribble.</summary>
        LovesToDribble = 1 << 1,
        /// <summary>Likes to set up his team-mates.</summary>
        Unselfish = 1 << 2,
        /// <summary>Likes to strike with the outside of the foot (the trivela).</summary>
        OutsideOfTheFoot = 1 << 3,
        /// <summary>Likes to use his weaker foot.</summary>
        UsesWeakFoot = 1 << 4,
        /// <summary>Likes tricks: backheels, dummies, fake shots.</summary>
        Showboat = 1 << 5,
        /// <summary>Likes to go in hard (only for an aggressive player).</summary>
        HardTackler = 1 << 6,
        /// <summary>Likes to shoot from distance.</summary>
        ShootsFromDistance = 1 << 7,
        /// <summary>Likes to switch the play to the other flank.</summary>
        SwitchesPlay = 1 << 8,
        /// <summary>Likes to play with his back to goal (holds it up, lays it off).</summary>
        BackToGoal = 1 << 9,
    }

    public static class PlayerTraits
    {
        public static readonly PlayerTrait[] All =
        {
            PlayerTrait.CutsInsideToShoot, PlayerTrait.LovesToDribble, PlayerTrait.Unselfish, PlayerTrait.OutsideOfTheFoot,
            PlayerTrait.UsesWeakFoot, PlayerTrait.Showboat, PlayerTrait.HardTackler, PlayerTrait.ShootsFromDistance,
            PlayerTrait.SwitchesPlay, PlayerTrait.BackToGoal,
        };

        /// <summary>The aggression a player needs before he can be a hard tackler.</summary>
        public const int HardTacklerAggression = 70;

        /// <summary>Pairs that contradict each other and are never given together.</summary>
        public static readonly (PlayerTrait, PlayerTrait)[] Conflicts =
        {
            (PlayerTrait.CutsInsideToShoot, PlayerTrait.BackToGoal),   // the winger running at goal v the target man facing away from it
            (PlayerTrait.OutsideOfTheFoot, PlayerTrait.UsesWeakFoot),  // the trivela is how he avoids his weaker foot
            (PlayerTrait.LovesToDribble, PlayerTrait.Unselfish),       // takes everyone on v gives it to the better-placed man
            (PlayerTrait.ShootsFromDistance, PlayerTrait.Unselfish),   // a shot on sight v the extra pass
        };

        public static bool Has(this PlayerProfile p, PlayerTrait t) => (p.Traits & t) != 0;

        public static int Count(PlayerTrait set)
        {
            int n = 0;
            for (int v = (int)set; v != 0; v &= v - 1) n++;
            return n;
        }

        public static bool Compatible(PlayerTrait set, PlayerTrait t, PlayerProfile p)
        {
            if ((set & t) != 0) return false;
            foreach (var (a, b) in Conflicts)
                if ((t == a && (set & b) != 0) || (t == b && (set & a) != 0)) return false;
            return true;
        }

        /// <summary>Does the set break a rule (a conflict, or a hard tackler without the aggression)?</summary>
        public static bool IsValid(PlayerTrait set, PlayerProfile p)
        {
            if ((set & PlayerTrait.HardTackler) != 0 && p.Attributes.Aggression < HardTacklerAggression) return false;
            foreach (var (a, b) in Conflicts) if ((set & a) != 0 && (set & b) != 0) return false;
            return true;
        }

        /// <summary>
        /// Gives the player about three traits (two for a keeper; a hard tackler is made aggressive, 70+), weighted by what suits his position and skills:
        /// a technical winger cuts in and dribbles, a target man plays with his back to goal, a playmaker sets others up.
        /// </summary>
        public static PlayerTrait Generate(PlayerProfile p, Random rng)
        {
            var a = p.Attributes;
            bool gk = p.NaturalPosition == PositionGroup.Goalkeeper;
            string pos = p.PositionLabel;
            bool wide = pos == "RW" || pos == "LW" || pos == "RM" || pos == "LM";
            bool fb = pos == "RB" || pos == "LB" || pos == "RWB" || pos == "LWB";
            bool st = pos == "ST" || pos == "CF";
            bool mid = pos == "CM" || pos == "AM" || pos == "DM";
            bool def = p.NaturalPosition == PositionGroup.Defender;
            bool rightSide = pos == "RW" || pos == "RM" || pos == "RB" || pos == "RWB";
            bool inverted = wide && (rightSide ? a.StrongFoot == Foot.Left : a.StrongFoot == Foot.Right);
            float A(int v) => MathUtil.Clamp01((v - 30f) / 60f);

            var weights = new Dictionary<PlayerTrait, float>
            {
                // (a winger whose strong foot is on the inside cuts in; one with it on the touchline side is a natural crosser)
                [PlayerTrait.CutsInsideToShoot] = gk || def && !fb ? 0f : (wide ? (inverted ? 1.0f : 0.2f) : fb ? 0.2f : st || pos == "AM" ? 0.5f : 0.15f) * (0.4f + A((a.Dribbling + a.Finishing) / 2)),
                [PlayerTrait.LovesToDribble] = gk ? 0f : (wide ? 2f : pos == "AM" || st ? 1.1f : fb ? 0.7f : def ? 0.15f : 0.6f) * (0.3f + A(a.Dribbling)),
                [PlayerTrait.Unselfish] = (gk ? 0.6f : mid ? 1.6f : 1f) * (0.3f + A((a.Teamwork + a.Passing) / 2)),
                [PlayerTrait.OutsideOfTheFoot] = gk ? 0f : 0.45f * (0.2f + A((a.BallControl + a.Creativity) / 2)) * (a.WeakFootRating <= 1 ? 1.6f : 0.8f),
                [PlayerTrait.UsesWeakFoot] = (gk ? 0.4f : 0.6f) * (a.WeakFootRating >= 2 ? 1.5f : 0.6f),
                [PlayerTrait.Showboat] = gk ? 0f : (def && !fb ? 0.15f : 0.8f) * (0.15f + A((a.Creativity + a.BallControl + a.Dribbling) / 3)),
                // (a hard man: a strong tackler, more often a defender; he is given the aggression that goes with it below)
                [PlayerTrait.HardTackler] = (gk ? 0.1f : def || pos == "DM" ? 1.1f : 0.35f) * (0.2f + A(a.Tackling)),
                [PlayerTrait.ShootsFromDistance] = gk ? 0f : (mid ? 1.3f : st || wide ? 0.8f : fb ? 0.35f : 0.15f) * (0.25f + A(a.LongShots)),
                [PlayerTrait.SwitchesPlay] = (gk ? 0.5f : mid || fb ? 1.3f : def ? 0.8f : 0.4f) * (0.3f + A((a.Passing + a.Creativity) / 2)),
                [PlayerTrait.BackToGoal] = gk ? 0f : (st ? 2.2f : pos == "AM" ? 0.4f : 0.05f) * (0.3f + A((a.PhysicalPower + a.BallControl) / 2)),
            };

            int want = gk ? 2 : rng.NextDouble() < 0.2 ? 2 : rng.NextDouble() < 0.85 ? 3 : 4;
            PlayerTrait set = PlayerTrait.None;
            for (int k = 0; k < want; k++)
            {
                float total = 0f;
                foreach (var t in All) if (Compatible(set, t, p)) total += weights[t];
                if (total <= 0f) break;
                double r = rng.NextDouble() * total;
                foreach (var t in All)
                {
                    if (!Compatible(set, t, p)) continue;
                    r -= weights[t];
                    if (r <= 0) { set |= t; break; }
                }
            }
            return set;
        }

        /// <summary>Gives every player in the squad his traits (deterministic for the seed).</summary>
        public static void Assign(TeamSetup t, int seed)
        {
            var rng = new Random(seed * 7919 + 131);
            var all = new List<PlayerProfile>(t.Starters);
            all.AddRange(t.Bench);
            foreach (var p in all) p.Traits = Generate(p, rng);
            // A squad has one or two hard men, not a team of them: the best tacklers keep the trait.
            var hard = all.FindAll(p => (p.Traits & PlayerTrait.HardTackler) != 0);
            hard.Sort((x, y) => y.Attributes.Tackling.CompareTo(x.Attributes.Tackling));
            for (int i = 0; i < hard.Count; i++)
            {
                var p = hard[i];
                if (i >= 2) { p.Traits &= ~PlayerTrait.HardTackler; continue; }
                // the hard tackler is an aggressive player by definition: 70 to 80
                if (p.Attributes.Aggression < HardTacklerAggression) p.Attributes.Aggression = HardTacklerAggression + rng.Next(11);
            }
        }
    }
}
