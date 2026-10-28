using System;

namespace SoccerVision.Simulation
{
    /// <summary>Runs the staged match's script (see <see cref="MatchScript"/>).</summary>
    public sealed partial class MatchEngine
    {
        private bool[] _scriptDone;
        private PlayerAgent _scriptCautioned;
        /// <summary>The card the next foul gets (LightInjury: none forced).</summary>
        private ScriptKind _forcedSanction = ScriptKind.LightInjury;
        private bool _scriptedFoul;
        /// <summary>The cautioned player, sent at the ball carrier until he commits his second foul.</summary>
        private PlayerAgent _scriptPresser;
        private float _scriptCheckAt;

        private bool ScriptExclusive => Config.Script != null && Config.Script.Exclusive;

        private void UpdateScript()
        {
            var script = Config.Script;
            if (script == null) return;
            if (_scriptDone == null) _scriptDone = new bool[script.Items.Count];
            if (SimTime < _scriptCheckAt) return;
            _scriptCheckAt = SimTime + 0.25f;
            for (int i = 0; i < script.Items.Count; i++)
            {
                var item = script.Items[i];
                if (_scriptDone[i] || Clock.Seconds < item.AtClockSeconds) continue;
                if (item.AtClockSeconds >= Config.RegulationHalfClockSeconds && Clock.Period != MatchPeriod.SecondHalf) continue; // second-half items wait for the second half
                // an item may wait for a fitting moment, but not for ever: after 6 clock minutes either team will do
                bool anyTeam = Clock.Seconds > item.AtClockSeconds + 360f;
                if (TryRunScriptItem(item, anyTeam)) _scriptDone[i] = true;
                return; // one incident at a time, in order
            }
        }

        private bool TryRunScriptItem(ScriptItem item, bool anyTeam)
        {
            int team = anyTeam ? -1 : item.Team;
            switch (item.Kind)
            {
                case ScriptKind.LightInjury:
                case ScriptKind.SeriousInjury:
                {
                    var p = PickInjured(team);
                    if (p == null) return false;
                    if (item.Kind == ScriptKind.LightInjury) InjureLightly(p, "script");
                    else
                    {
                        if (FindSubstitute(p) == null) return false;
                        InjureSeriously(p, "script");
                    }
                    return true;
                }
                default:
                    return TryScriptedFoul(item.Kind, team);
            }
        }

        /// <summary>An outfield player near the ball who is fit.</summary>
        private PlayerAgent PickInjured(int team)
        {
            PlayerAgent best = null;
            float bestD = float.MaxValue;
            foreach (var p in AllPlayers)
            {
                if (p.SentOff || p.IsGoalkeeper || p.Injury != InjuryLevel.None || p.IsDown || p == _scriptCautioned) continue;
                if (team >= 0 && p.Team.Index != team) continue;
                if (Pitch.InPenaltyArea(p.Pos, 1) || Pitch.InPenaltyArea(p.Pos, -1)) continue;
                float d = Vec2.Distance(p.Pos, Ball.Pos);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>
        /// A foul on the ball carrier by a defender close to him (in the second yellow's case, by the player cautioned
        /// before), outside the penalty areas, with the scripted card.
        /// </summary>
        private bool TryScriptedFoul(ScriptKind kind, int team)
        {
            var victim = Ball.Owner;
            if (victim == null || victim.SentOff || Ball.InHands) return false;
            if (Pitch.InPenaltyArea(victim.Pos, 1) || Pitch.InPenaltyArea(victim.Pos, -1)) return false;
            PlayerAgent offender = null;
            if (kind == ScriptKind.SecondYellow)
            {
                offender = _scriptCautioned;
                if (offender == null || offender.SentOff || offender.Team == victim.Team) return false;
                if (Vec2.Distance(offender.Pos, victim.Pos) > 3f)
                {
                    // not close enough yet: he is sent at the carrier (a defender "pressing hard"); the foul comes as he arrives
                    _scriptPresser = offender;
                    return false;
                }
                _scriptPresser = null;
            }
            else
            {
                if (team >= 0 && victim.Team.Opponent.Index != team) return false;
                float bestD = 6f;
                foreach (var o in victim.Team.Opponent.Players)
                {
                    if (o.SentOff || o.IsGoalkeeper || o.IsDown || (kind == ScriptKind.Yellow && o.YellowCards > 0)) continue;
                    float d = Vec2.Distance(o.Pos, victim.Pos);
                    if (d < bestD) { bestD = d; offender = o; }
                }
                if (offender == null) return false;
            }
            if (kind == ScriptKind.Yellow) _scriptCautioned = offender;
            _forcedSanction = kind == ScriptKind.DirectRed ? ScriptKind.DirectRed : ScriptKind.Yellow;
            _scriptedFoul = true;
            CommitFoul(offender, victim, fromBehind: true, playedForBall: false, contact: victim.Pos);
            _scriptedFoul = false;
            _forcedSanction = ScriptKind.LightInjury;
            return true;
        }
    }
}
