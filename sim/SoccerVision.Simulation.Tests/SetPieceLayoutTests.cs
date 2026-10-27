using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    public class SetPieceLayoutTests
    {
        [Fact]
        public void FreeKicksInTheAttackingThirdFillTheBoxWithMarkingPairsNotAStraightLine()
        {
            int kicks = 0, pairs = 0, pairable = 0, linedUp = 0;
            var sync = new object();
            Parallel.For(0, 30, i =>
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4000 + (ulong)i);
                e.RestartExecuting += r =>
                {
                    if (r.Type != RestartType.DirectFreeKick && r.Type != RestartType.IndirectFreeKick) return;
                    var att = r.Team;
                    var def = att.Opponent;
                    if (att.Forward(r.Spot) < 22f || att.Forward(r.Spot) > 41f || MathF.Abs(r.Spot.Y) > 22f) return; // a crossable / shootable spot
                    var inBox = att.Players.Where(p => !p.SentOff && !p.IsGoalkeeper && p != r.Taker && Pitch.InPenaltyArea(p.Pos, att.AttackGoalSign)).ToList();
                    lock (sync)
                    {
                        kicks++;
                        pairable += inBox.Count;
                        foreach (var a in inBox)
                            if (def.Players.Any(d => !d.SentOff && !d.IsGoalkeeper && Vec2.Distance(d.Pos, a.Pos) < 2.6f)) pairs++;
                        // spread of the attackers in the box along the attacking direction: a straight line has almost none
                        if (inBox.Count >= 3)
                        {
                            var xs = inBox.Select(p => att.Forward(p.Pos)).ToList();
                            if (xs.Max() - xs.Min() < 2.0f) linedUp++;
                        }
                    }
                };
                e.RunToEnd();
            });
            Assert.True(kicks >= 20, "free kicks checked " + kicks);
            Assert.True(pairable >= kicks * 2.5, $"attackers in the box {pairable} over {kicks} kicks");
            Assert.True(pairs >= pairable * 0.7, $"marked attackers {pairs}/{pairable}");
            Assert.True(linedUp <= kicks * 0.1, $"{linedUp} of {kicks} kicks had the attackers in a straight line");
        }

        [Fact]
        public void FreeKicksNearTheByLineDoNotPutPlayersInALineAlongIt()
        {
            int kicks = 0, onLine = 0;
            var sync = new object();
            Parallel.For(0, 40, i =>
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4100 + (ulong)i);
                e.RestartExecuting += r =>
                {
                    if (r.Type != RestartType.DirectFreeKick && r.Type != RestartType.IndirectFreeKick) return;
                    if (r.Team.Forward(r.Spot) < 41f) return; // deep, near the goal line
                    lock (sync)
                    {
                        kicks++;
                        int hugging = e.AllPlayers.Count(p => !p.SentOff && !p.IsGoalkeeper && p != r.Taker && MathF.Abs(MathF.Abs(p.Pos.X) - Pitch.HalfLength) < 1.2f);
                        if (hugging >= 4) onLine++;
                    }
                };
                e.RunToEnd();
            });
            Assert.True(kicks >= 3, "deep free kicks checked " + kicks);
            Assert.True(onLine == 0, $"{onLine} of {kicks} deep free kicks had four or more players on the goal line");
        }
    }
}
