using System;
using SoccerVision.Simulation;

namespace SoccerVision.Runtime
{
    // Message contract between the Unity match and the React shell. Mirrored by web/src/bridge/protocol.ts —
    // keep both files in sync. Unity -> React: events "MatchReady", "MatchState", "MatchEvent".
    // React -> Unity: sendMessage("SoccerVisionBridge", "OnCommand", json(BridgeCommand)).

    [Serializable]
    public sealed class BridgeCommand
    {
        /// <summary>pause | resume | togglePause | setSpeed | restart | setHud | setTactics | requestState</summary>
        public string type = "";
        public float value;
        public bool flag;
        public long seed = -1;
        public int team = -1;
        public TeamTacticsDto tactics;
    }

    [Serializable]
    public sealed class TeamTacticsDto
    {
        public float mentality = 0.5f;
        public float defensiveLine = 0.5f;
        public float width = 0.5f;
        public float tempo = 0.5f;
        public float pressingIntensity = 0.5f;

        public TeamTactics ToModel() => new TeamTactics
        {
            Mentality = mentality,
            DefensiveLine = defensiveLine,
            Width = width,
            Tempo = tempo,
            PressingIntensity = pressingIntensity,
        };
    }

    [Serializable]
    public sealed class PlayerDto
    {
        public int index;
        public int number;
        public string name;
        public string role;
        public bool goalkeeper;
        public int passing, speed, stamina, finishing, tackling;
    }

    [Serializable]
    public sealed class TeamInfoDto
    {
        public string name;
        public string shortName;
        public string color;
        public string goalkeeperColor;
        public string formation;
        public PlayerDto[] players;
    }

    [Serializable]
    public sealed class MatchReadyDto
    {
        public long seed;
        public float halfRealSeconds;
        public float halftimeRealSeconds;
        public TeamInfoDto home;
        public TeamInfoDto away;
    }

    [Serializable]
    public sealed class TeamStatsDto
    {
        public int goals, shots, shotsOnTarget, corners, fouls, yellowCards, redCards, offsides, passes, passesCompleted, saves;
        public float xg, possession;

        public static TeamStatsDto From(TeamStats s, float possessionShare) => new TeamStatsDto
        {
            goals = s.Goals,
            shots = s.Shots,
            shotsOnTarget = s.ShotsOnTarget,
            corners = s.Corners,
            fouls = s.Fouls,
            yellowCards = s.YellowCards,
            redCards = s.RedCards,
            offsides = s.Offsides,
            passes = s.Passes,
            passesCompleted = s.PassesCompleted,
            saves = s.Saves,
            xg = s.ExpectedGoals,
            possession = possessionShare,
        };
    }

    [Serializable]
    public sealed class MatchStateDto
    {
        public string phase;
        public string period;
        public string minute;
        public string clock;
        public int homeScore;
        public int awayScore;
        public int addedMinutes;
        public float halftimeRemaining;
        public float realElapsed;
        public bool paused;
        public float speed;
        public bool finished;
        public TeamStatsDto homeStats;
        public TeamStatsDto awayStats;
    }

    [Serializable]
    public sealed class MatchEventDto
    {
        public string type;
        public string minute;
        public float clock;
        public string text;
        public string tone;
        public string whistle;
        public int team;
        public int player;
        public int playerNumber;
        public int other;
        public float x, y;

        public static MatchEventDto From(MatchEvent e, MatchEngine m)
        {
            var p = m.PlayerByIndex(e.Player);
            return new MatchEventDto
            {
                type = e.Type.ToString(),
                minute = e.Minute,
                clock = e.ClockSeconds,
                text = e.Text,
                tone = Commentary.ToneOf(e.Type).ToString(),
                whistle = e.Type == MatchEventType.Whistle ? e.Whistle.ToString() : "",
                team = e.Team,
                player = e.Player,
                playerNumber = p != null ? p.Number : -1,
                other = e.OtherPlayer,
                x = e.X,
                y = e.Y,
            };
        }
    }
}
