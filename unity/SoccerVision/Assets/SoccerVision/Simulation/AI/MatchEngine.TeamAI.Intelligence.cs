using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// Reading the game, without any tactic to set (from tactical analysis of top teams):
    /// <list type="bullet">
    /// <item><b>Splitting the presser</b>: the two players nearest the ball offer it on both sides of the man closing it down,
    /// not both on the same side - he cannot cover two lanes, and the pass goes to the side he does not shut.
    /// When the man on the ball faces his own goal or is squeezed, one of them drops off to give him the way out behind.</item>
    /// <item><b>Situational tempo</b>: the team slows down against a press in its own half (to draw it on and play through it)
    /// and speeds up right after winning the ball, while the opponents are still out of shape. The tempo the manager set
    /// is the base; the players shift it by what they see.</item>
    /// <item><b>Deep completions</b>: a ground pass into the space within ~20 m of goal, in front of the goal or in a half-space,
    /// is worth more to a good passer than a hopeful cross - he looks for it when the lane is open.</item>
    /// </list>
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>The opponent closing the carrier down (within 9 m and nearest), or null.</summary>
        private PlayerAgent Presser(PlayerAgent carrier)
        {
            PlayerAgent best = null;
            float bd = 9f * 9f;
            foreach (var o in carrier.Team.Opponent.Players)
            {
                if (o.SentOff || o.IsGoalkeeper) continue;
                float d = Vec2.DistanceSq(o.Pos, carrier.Pos);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        /// <summary>Which side of the line carrier -> presser a point lies on (+1 / -1).</summary>
        private static float SideOf(Vec2 carrier, Vec2 presser, Vec2 point)
        {
            Vec2 a = presser - carrier, b = point - carrier;
            return a.X * b.Y - a.Y * b.X >= 0f ? 1f : -1f;
        }

        /// <summary>
        /// The supporters' reading of the presser, added to the support-spot score: the second man takes the other side of
        /// the presser from the first; neither stands so close that the presser covers him and the ball together, nor so far
        /// that the pass is long and slow. How well he does it is his reading of the game (off the ball, decisions).
        /// </summary>
        private float SplitScore(PlayerAgent supporter, PlayerAgent carrier, PlayerAgent presser, Vec2 spot, Vec2 avoid)
        {
            float read = 0.35f + 0.65f * MathUtil.A((supporter.Attr.OffTheBall + supporter.Attr.Decisions) / 2);
            float d = Vec2.Distance(spot, carrier.Pos);
            float s = 0f;
            if (d < 8f) s -= (8f - d) * 0.25f * read;               // (too close: one presser covers both)
            else if (d > 22f) s -= (d - 22f) * 0.08f * read;        // (too far: the pass is slow and can be read)
            if (presser == null || avoid.X > 900f) return s;
            bool split = SideOf(carrier.Pos, presser.Pos, spot) != SideOf(carrier.Pos, presser.Pos, avoid);
            return s + (split ? 2.4f : -0.6f) * read;
        }

        /// <summary>A carrier facing his own goal, or under real pressure, needs a man behind him to play it back to.</summary>
        private bool NeedsDropOff(PlayerAgent carrier)
        {
            var team = carrier.Team;
            bool facingBack = Vec2.Dot(carrier.FacingDir, team.DirToWorld(Vec2.UnitX)) < -0.2f;
            return facingBack || Pressure(carrier, out _) > 0.55f;
        }

        /// <summary>
        /// The tempo of the moment (see the class notes): the manager's tempo shifted by what the players see. Called while
        /// the team has the ball; eased so that the rhythm changes over a second or two, not in jumps.
        /// </summary>
        private void UpdateSituationalTempo(TeamAgent team)
        {
            var carrier = Ball.Owner != null && Ball.Owner.Team == team ? Ball.Owner : null;
            float want = 0f;
            // just won it back, and not deep in our own corner: go, before they are organised (a transition)
            if (SimTime - team.Opponent.LostBallAt < 5f && team.Forward(Ball.Pos) > -30f) want = 0.25f;
            else if (carrier != null && team.Forward(carrier.Pos) < -8f && Pressure(carrier, out _) > 0.45f)
                want = -0.2f; // a press in our own half: calm, short, draw them on - the gap opens behind them
            else if (carrier != null && team.Forward(carrier.Pos) > 15f && team.Opponent.LineForward < -28f)
                want = 0.08f; // a deep block: keep the ball moving to shift it (circulation, not hurry)
            team.TempoShift = MathUtil.MoveTowards(team.TempoShift, want, 0.05f); // (called every 0.2 s: the full shift in about a second)
            if (CollectDiagnostics)
            {
                if (want > 0.2f) Count("tempo.accelerate"); else if (want < 0f) Count("tempo.calm");
            }
        }

        /// <summary>The tempo the team plays at right now: the manager's tempo shifted by the moment.</summary>
        private static float TempoNow(TeamAgent team) => MathUtil.Clamp01(team.Eff.Tempo + team.TempoShift);

        /// <summary>A target for a deep completion: within ~20 m of the goal, in front of it or in a half-space.</summary>
        private static bool IsDeepZone(TeamAgent team, Vec2 target)
        {
            float f = team.Forward(target), l = team.Lateral(target);
            float dx = Pitch.HalfLength - f;
            return dx > 0f && dx * dx + l * l < 20f * 20f && MathF.Abs(l) < 15f;
        }
    }
}
