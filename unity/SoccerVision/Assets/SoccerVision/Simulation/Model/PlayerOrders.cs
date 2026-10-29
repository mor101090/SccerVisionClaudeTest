using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// A player's role: how he plays his position. Each kind of position has its own roles; Auto plays the position the
    /// way the engine always has (the player's attributes decide, e.g. a winger with his strong foot inside cuts in).
    /// </summary>
    public enum PlayerRole
    {
        Auto,
        // goalkeepers
        ShotStopper, SweeperKeeper, DominantKeeper,
        // centre-backs
        Stopper, BallPlayingDefender, CoverDefender,
        // full-backs
        WingBack, InvertedFullBack, DefensiveFullBack,
        // defensive midfielders
        Destroyer, DeepLyingPlaymaker, BoxToBoxDefensive, AnchorMan,
        // central midfielders
        AllRounder, TempoSetter, Carrier,
        // attacking midfielders
        ClassicPlaymaker, ShadowStriker, WidePlaymaker,
        // wingers
        InvertedWinger, ClassicWinger, WideTargetMan,
        // strikers
        CentreForward, TargetForward, SecondStriker, FalseNine,
    }

    /// <summary>An individual instruction: less, mixed (the player's own judgement) or more.</summary>
    public enum Amount { Less, Mixed, More }

    /// <summary>The manager's orders for one position: its role and the individual instructions.</summary>
    [Serializable]
    public sealed class PlayerOrders
    {
        public PlayerRole Role = PlayerRole.Auto;
        /// <summary>Shots from distance: more, mixed, less.</summary>
        public Amount LongShots = Amount.Mixed;
        /// <summary>Running with the ball: a lot, mixed, little.</summary>
        public Amount Dribbling = Amount.Mixed;
        /// <summary>In the tackle: very aggressive (More), mixed, gentle (Less).</summary>
        public Amount Aggression = Amount.Mixed;

        public PlayerOrders Clone() => (PlayerOrders)MemberwiseClone();

        private static readonly PlayerRole[][] ByKind =
        {
            new[] { PlayerRole.Auto, PlayerRole.ShotStopper, PlayerRole.SweeperKeeper, PlayerRole.DominantKeeper },
            new[] { PlayerRole.Auto, PlayerRole.Stopper, PlayerRole.BallPlayingDefender, PlayerRole.CoverDefender },
            new[] { PlayerRole.Auto, PlayerRole.WingBack, PlayerRole.InvertedFullBack, PlayerRole.DefensiveFullBack },
            new[] { PlayerRole.Auto, PlayerRole.Destroyer, PlayerRole.DeepLyingPlaymaker, PlayerRole.BoxToBoxDefensive, PlayerRole.AnchorMan },
            new[] { PlayerRole.Auto, PlayerRole.AllRounder, PlayerRole.TempoSetter, PlayerRole.Carrier },
            new[] { PlayerRole.Auto, PlayerRole.ClassicPlaymaker, PlayerRole.ShadowStriker, PlayerRole.WidePlaymaker },
            new[] { PlayerRole.Auto, PlayerRole.InvertedWinger, PlayerRole.ClassicWinger, PlayerRole.WideTargetMan },
            new[] { PlayerRole.Auto, PlayerRole.CentreForward, PlayerRole.TargetForward, PlayerRole.SecondStriker, PlayerRole.FalseNine },
        };

        /// <summary>The roles a kind of position can be given (Auto first). Shared arrays: do not change them.</summary>
        public static PlayerRole[] RolesFor(PositionKind kind) => ByKind[(int)kind];

        /// <summary>Whether the role belongs to that kind of position (Auto fits every position).</summary>
        public static bool Fits(PlayerRole role, PositionKind kind) => role == PlayerRole.Auto || Array.IndexOf(ByKind[(int)kind], role) >= 0;

        public static string Name(PlayerRole r)
        {
            switch (r)
            {
                case PlayerRole.ShotStopper: return "Shot-stopper";
                case PlayerRole.SweeperKeeper: return "Sweeper-keeper";
                case PlayerRole.DominantKeeper: return "Dominant keeper";
                case PlayerRole.Stopper: return "Stopper";
                case PlayerRole.BallPlayingDefender: return "Ball-playing defender";
                case PlayerRole.CoverDefender: return "Cover defender";
                case PlayerRole.WingBack: return "Wing-back";
                case PlayerRole.InvertedFullBack: return "Inverted full-back";
                case PlayerRole.DefensiveFullBack: return "Defensive full-back";
                case PlayerRole.Destroyer: return "Destroyer";
                case PlayerRole.DeepLyingPlaymaker: return "Deep-lying playmaker";
                case PlayerRole.BoxToBoxDefensive: return "Box-to-box";
                case PlayerRole.AnchorMan: return "Anchor";
                case PlayerRole.AllRounder: return "All-rounder";
                case PlayerRole.TempoSetter: return "Tempo setter";
                case PlayerRole.Carrier: return "Carrier";
                case PlayerRole.ClassicPlaymaker: return "Classic playmaker";
                case PlayerRole.ShadowStriker: return "Shadow striker";
                case PlayerRole.WidePlaymaker: return "Wide playmaker";
                case PlayerRole.InvertedWinger: return "Inverted winger";
                case PlayerRole.ClassicWinger: return "Classic winger";
                case PlayerRole.WideTargetMan: return "Wide target man";
                case PlayerRole.CentreForward: return "Centre forward";
                case PlayerRole.TargetForward: return "Target forward";
                case PlayerRole.SecondStriker: return "Second striker";
                case PlayerRole.FalseNine: return "False nine";
                default: return "Auto";
            }
        }
    }
}
