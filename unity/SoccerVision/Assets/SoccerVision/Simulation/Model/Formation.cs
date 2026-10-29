using System;
using System.Collections.Generic;

namespace SoccerVision.Simulation
{
    public enum Role
    {
        Goalkeeper,
        RightBack, CentreBackRight, CentreBack, CentreBackLeft, LeftBack,
        DefensiveMidfielder,
        RightMidfielder, CentreMidRight, CentreMid, CentreMidLeft, LeftMidfielder,
        AttackingMidfielder,
        RightWinger, LeftWinger,
        StrikerRight, Striker, StrikerLeft,
    }

    public enum Line { Goalkeeper, Defence, Midfield, Attack }

    /// <summary>The kind of position, for the player roles: keeper, centre-back, full-back, holding, central and attacking midfielder, winger, striker.</summary>
    public enum PositionKind { Goalkeeper, CentreBack, FullBack, DefensiveMidfielder, CentralMidfielder, AttackingMidfielder, Winger, Striker }

    /// <summary>
    /// One position in a formation, in team-local metres: Forward is along the attacking direction
    /// (-52.5 = own goal line), Lateral is negative on the team's right flank and positive on its left.
    /// Base coordinates describe the neutral shape with the ball on the centre spot.
    /// </summary>
    [Serializable]
    public struct FormationSlot
    {
        public Role Role;
        public Line Line;
        public float BaseForward;
        public float BaseLateral;
        /// <summary>0 = back line, 1 = front line; drives the vertical stretch of the block.</summary>
        public float Depth;

        public FormationSlot(Role role, Line line, float forward, float lateral, float depth)
        {
            Role = role;
            Line = line;
            BaseForward = forward;
            BaseLateral = lateral;
            Depth = depth;
        }

        public bool IsWide => MathF.Abs(BaseLateral) > 16f;
        public bool IsFullBack => Line == Line.Defence && IsWide;
        public bool IsCentreBack => Line == Line.Defence && !IsWide;
        public bool IsWideMidfielder => Line == Line.Midfield && IsWide;
        public bool IsCentralMidfielder => Line == Line.Midfield && !IsWide;
    }

    [Serializable]
    public sealed class Formation
    {
        public string Name;
        public FormationSlot[] Slots;

        public Formation(string name, FormationSlot[] slots)
        {
            if (slots == null || slots.Length != 11) throw new ArgumentException("A formation needs exactly 11 slots");
            if (slots[0].Line != Line.Goalkeeper) throw new ArgumentException("Slot 0 must be the goalkeeper");
            Name = name;
            Slots = slots;
        }

        /// <summary>Flat 4-4-2 (both demo teams use it).</summary>
        public static Formation F442()
        {
            return new Formation("4-4-2", new[]
            {
                new FormationSlot(Role.Goalkeeper,       Line.Goalkeeper, -49f,   0f, 0f),
                new FormationSlot(Role.RightBack,        Line.Defence,    -31f, -23f, 0f),
                new FormationSlot(Role.CentreBackRight,  Line.Defence,    -33f,  -8f, 0f),
                new FormationSlot(Role.CentreBackLeft,   Line.Defence,    -33f,   8f, 0f),
                new FormationSlot(Role.LeftBack,         Line.Defence,    -31f,  23f, 0f),
                new FormationSlot(Role.RightMidfielder,  Line.Midfield,   -14f, -24f, 0.5f),
                new FormationSlot(Role.CentreMidRight,   Line.Midfield,   -16f,  -7.5f, 0.45f),
                new FormationSlot(Role.CentreMidLeft,    Line.Midfield,   -16f,   7.5f, 0.45f),
                new FormationSlot(Role.LeftMidfielder,   Line.Midfield,   -14f,  24f, 0.5f),
                new FormationSlot(Role.StrikerRight,     Line.Attack,      -1f,  -7f, 1f),
                new FormationSlot(Role.StrikerLeft,      Line.Attack,      -1f,   7f, 1f),
            });
        }

        private static FormationSlot S(Role r, Line l, float f, float lat, float depth) => new FormationSlot(r, l, f, lat, depth);
        private static FormationSlot Gk => S(Role.Goalkeeper, Line.Goalkeeper, -49f, 0f, 0f);
        private static FormationSlot[] BackFour => new[]
        {
            S(Role.RightBack, Line.Defence, -31f, -23f, 0f), S(Role.CentreBackRight, Line.Defence, -33f, -8f, 0f),
            S(Role.CentreBackLeft, Line.Defence, -33f, 8f, 0f), S(Role.LeftBack, Line.Defence, -31f, 23f, 0f),
        };
        /// <summary>Three centre-backs and two wing-backs (who drop into a back five without the ball).</summary>
        private static FormationSlot[] BackThree => new[]
        {
            S(Role.RightBack, Line.Defence, -27f, -27f, 0f), S(Role.CentreBackRight, Line.Defence, -33f, -11f, 0f),
            S(Role.CentreBackLeft, Line.Defence, -33f, 11f, 0f), S(Role.LeftBack, Line.Defence, -27f, 27f, 0f),
            S(Role.CentreBack, Line.Defence, -34.5f, 0f, 0f),
        };
        private static Formation Make(string name, FormationSlot[] back, params FormationSlot[] rest)
        {
            var all = new FormationSlot[11];
            all[0] = Gk;
            back.CopyTo(all, 1);
            rest.CopyTo(all, 1 + back.Length);
            return new Formation(name, all);
        }

        /// <summary>4-4-2 narrow: four midfielders packed in the middle, no wingers - the full-backs give the width.</summary>
        public static Formation F442Narrow() => Make("4-4-2 narrow", BackFour,
            S(Role.CentreMidRight, Line.Midfield, -15f, -14f, 0.46f), S(Role.CentreMidRight, Line.Midfield, -16f, -5f, 0.44f),
            S(Role.CentreMidLeft, Line.Midfield, -16f, 5f, 0.44f), S(Role.CentreMidLeft, Line.Midfield, -15f, 14f, 0.46f),
            S(Role.StrikerRight, Line.Attack, -1f, -7f, 1f), S(Role.StrikerLeft, Line.Attack, -1f, 7f, 1f));

        /// <summary>4-4-2 diamond (4-1-2-1-2): a holding man, two shuttlers, a number 10 at the tip, two strikers.</summary>
        public static Formation F442Diamond() => Make("4-4-2 diamond", BackFour,
            S(Role.CentreMidRight, Line.Midfield, -15f, -13f, 0.47f), S(Role.DefensiveMidfielder, Line.Midfield, -23f, 0f, 0.22f),
            S(Role.AttackingMidfielder, Line.Midfield, -8f, 0f, 0.74f), S(Role.CentreMidLeft, Line.Midfield, -15f, 13f, 0.47f),
            S(Role.StrikerRight, Line.Attack, -1f, -7f, 1f), S(Role.StrikerLeft, Line.Attack, -1f, 7f, 1f));

        /// <summary>4-3-3: a holding midfielder behind two central midfielders, two wingers high and wide, one striker.</summary>
        public static Formation F433() => Make("4-3-3", BackFour,
            S(Role.RightWinger, Line.Midfield, -5f, -25f, 0.82f), S(Role.CentreMidRight, Line.Midfield, -14f, -10f, 0.5f),
            S(Role.CentreMidLeft, Line.Midfield, -14f, 10f, 0.5f), S(Role.LeftWinger, Line.Midfield, -5f, 25f, 0.82f),
            S(Role.DefensiveMidfielder, Line.Midfield, -23f, 0f, 0.22f), S(Role.Striker, Line.Attack, -1f, 0f, 1f));

        /// <summary>4-3-3 defensive: a double pivot behind one central midfielder, the wingers and a striker (safety first).</summary>
        public static Formation F433Defensive() => Make("4-3-3 defensive", BackFour,
            S(Role.RightWinger, Line.Midfield, -7f, -24f, 0.78f), S(Role.CentreMidRight, Line.Midfield, -22f, -7f, 0.24f),
            S(Role.CentreMidLeft, Line.Midfield, -22f, 7f, 0.24f), S(Role.LeftWinger, Line.Midfield, -7f, 24f, 0.78f),
            S(Role.CentreMid, Line.Midfield, -13f, 0f, 0.55f), S(Role.Striker, Line.Attack, -1f, 0f, 1f));

        /// <summary>4-2-3-1 wide: two holding midfielders, a number 10 and two wide men behind one striker.</summary>
        public static Formation F4231() => Make("4-2-3-1", BackFour,
            S(Role.RightWinger, Line.Midfield, -8f, -24f, 0.72f), S(Role.CentreMidRight, Line.Midfield, -21f, -6f, 0.27f),
            S(Role.CentreMidLeft, Line.Midfield, -21f, 6f, 0.27f), S(Role.LeftWinger, Line.Midfield, -8f, 24f, 0.72f),
            S(Role.AttackingMidfielder, Line.Midfield, -9f, 0f, 0.72f), S(Role.Striker, Line.Attack, -1f, 0f, 1f));

        /// <summary>4-1-4-1: one holding midfielder, a flat four in front of him, one striker.</summary>
        public static Formation F4141() => Make("4-1-4-1", BackFour,
            S(Role.RightMidfielder, Line.Midfield, -12f, -24f, 0.58f), S(Role.CentreMidRight, Line.Midfield, -13f, -8f, 0.56f),
            S(Role.CentreMidLeft, Line.Midfield, -13f, 8f, 0.56f), S(Role.LeftMidfielder, Line.Midfield, -12f, 24f, 0.58f),
            S(Role.DefensiveMidfielder, Line.Midfield, -24f, 0f, 0.2f), S(Role.Striker, Line.Attack, -1f, 0f, 1f));

        /// <summary>4-5-1: a wall of five midfielders close to the back four, one striker left up for the counter.</summary>
        public static Formation F451() => Make("4-5-1", BackFour,
            S(Role.RightMidfielder, Line.Midfield, -18f, -24f, 0.36f), S(Role.CentreMidRight, Line.Midfield, -20f, -10f, 0.32f),
            S(Role.CentreMidLeft, Line.Midfield, -20f, 10f, 0.32f), S(Role.LeftMidfielder, Line.Midfield, -18f, 24f, 0.36f),
            S(Role.CentreMid, Line.Midfield, -21f, 0f, 0.3f), S(Role.Striker, Line.Attack, -4f, 0f, 0.95f));

        /// <summary>3-5-2: three centre-backs, wing-backs the length of the flank, three in midfield and two strikers.</summary>
        public static Formation F352() => Make("3-5-2", BackThree,
            S(Role.CentreMidRight, Line.Midfield, -15f, -11f, 0.5f), S(Role.CentreMidLeft, Line.Midfield, -15f, 11f, 0.5f),
            S(Role.DefensiveMidfielder, Line.Midfield, -22f, 0f, 0.25f),
            S(Role.StrikerRight, Line.Attack, -1f, -7f, 1f), S(Role.StrikerLeft, Line.Attack, -1f, 7f, 1f));

        /// <summary>3-4-3: three centre-backs, wing-backs, two central midfielders and a front three.</summary>
        public static Formation F343() => Make("3-4-3", BackThree,
            S(Role.CentreMidRight, Line.Midfield, -18f, -7f, 0.38f), S(Role.CentreMidLeft, Line.Midfield, -18f, 7f, 0.38f),
            S(Role.RightWinger, Line.Midfield, -4f, -21f, 0.86f), S(Role.LeftWinger, Line.Midfield, -4f, 21f, 0.86f),
            S(Role.Striker, Line.Attack, -1f, 0f, 1f));

        /// <summary>3-4-1-2: the 3-5-2 with a number 10 right behind the two strikers.</summary>
        public static Formation F3412() => Make("3-4-1-2", BackThree,
            S(Role.CentreMidRight, Line.Midfield, -18f, -7f, 0.36f), S(Role.CentreMidLeft, Line.Midfield, -18f, 7f, 0.36f),
            S(Role.AttackingMidfielder, Line.Midfield, -7f, 0f, 0.74f),
            S(Role.StrikerRight, Line.Attack, -1f, -7f, 1f), S(Role.StrikerLeft, Line.Attack, -1f, 7f, 1f));

        /// <summary>The formations a manager can choose, by name.</summary>
        public static readonly string[] Names = { "4-4-2", "4-4-2 narrow", "4-4-2 diamond", "4-3-3", "4-3-3 defensive", "3-5-2", "3-4-3", "3-4-1-2", "4-2-3-1", "4-1-4-1", "4-5-1" };

        public static Formation ByName(string name)
        {
            switch (name)
            {
                case "4-4-2 narrow": return F442Narrow();
                case "4-4-2 diamond": return F442Diamond();
                case "4-3-3": return F433();
                case "4-3-3 defensive": return F433Defensive();
                case "3-5-2": return F352();
                case "3-4-3": return F343();
                case "3-4-1-2": return F3412();
                case "4-2-3-1": return F4231();
                case "4-1-4-1": return F4141();
                case "4-5-1": return F451();
                default: return F442();
            }
        }

        /// <summary>
        /// Who plays where after a change of formation: each outfield player goes to the free new slot nearest to his old
        /// place (closest pairs first), so a right midfielder becomes the right winger, not a centre-back.
        /// Returns newSlotOf[oldSlot].
        /// </summary>
        public static int[] Remap(Formation from, Formation to)
        {
            var map = new int[11];
            var usedOld = new bool[11];
            var usedNew = new bool[11];
            map[0] = 0; usedOld[0] = usedNew[0] = true;
            for (int n = 1; n < 11; n++)
            {
                int bo = -1, bn = -1;
                float bd = float.PositiveInfinity;
                for (int i = 1; i < 11; i++)
                {
                    if (usedOld[i]) continue;
                    for (int j = 1; j < 11; j++)
                    {
                        if (usedNew[j]) continue;
                        var a = from.Slots[i]; var b = to.Slots[j];
                        float df = a.BaseForward - b.BaseForward, dl = a.BaseLateral - b.BaseLateral;
                        // (the line matters more than the exact spot: a defender stays a defender)
                        float d = df * df + dl * dl + (a.Line != b.Line ? 400f : 0f);
                        if (d < bd) { bd = d; bo = i; bn = j; }
                    }
                }
                map[bo] = bn; usedOld[bo] = usedNew[bn] = true;
            }
            return map;
        }

        /// <summary>The kind of position a slot is (which roles it can be given).</summary>
        public static PositionKind KindOf(Role role)
        {
            switch (role)
            {
                case Role.Goalkeeper: return PositionKind.Goalkeeper;
                case Role.RightBack: case Role.LeftBack: return PositionKind.FullBack;
                case Role.CentreBack: case Role.CentreBackLeft: case Role.CentreBackRight: return PositionKind.CentreBack;
                case Role.DefensiveMidfielder: return PositionKind.DefensiveMidfielder;
                case Role.CentreMid: case Role.CentreMidLeft: case Role.CentreMidRight: return PositionKind.CentralMidfielder;
                case Role.AttackingMidfielder: return PositionKind.AttackingMidfielder;
                case Role.RightMidfielder: case Role.LeftMidfielder: case Role.RightWinger: case Role.LeftWinger: return PositionKind.Winger;
                default: return PositionKind.Striker;
            }
        }

        /// <summary>The kind of a slot in this formation; the two holding men of a 4-2-3-1 count as defensive midfielders.</summary>
        public PositionKind KindAt(int slot)
        {
            var s = Slots[slot];
            var k = KindOf(s.Role);
            if (k == PositionKind.CentralMidfielder && s.Depth < 0.32f) return PositionKind.DefensiveMidfielder;
            return k;
        }

        public static string RoleAbbreviation(Role role)
        {
            switch (role)
            {
                case Role.Goalkeeper: return "GK";
                case Role.RightBack: return "RB";
                case Role.LeftBack: return "LB";
                case Role.CentreBack:
                case Role.CentreBackLeft:
                case Role.CentreBackRight: return "CB";
                case Role.DefensiveMidfielder: return "DM";
                case Role.RightMidfielder: return "RM";
                case Role.LeftMidfielder: return "LM";
                case Role.CentreMid:
                case Role.CentreMidLeft:
                case Role.CentreMidRight: return "CM";
                case Role.AttackingMidfielder: return "AM";
                case Role.RightWinger: return "RW";
                case Role.LeftWinger: return "LW";
                default: return "ST";
            }
        }

        public IEnumerable<int> IndicesOf(Line line)
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i].Line == line) yield return i;
        }
    }
}
