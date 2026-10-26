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
