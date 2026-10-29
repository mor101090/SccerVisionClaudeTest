using System.Linq;
using System.Threading.Tasks;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>Officials: the flag points the way the awarded team attacks, no restart while a card is shown, injuries come from contact.</summary>
    public class RefereeContactTests
    {
        [Fact]
        public void TheFlagPointsTheWayTheTeamAwardedTheRestartAttacks()
        {
            int raised = 0, wrong = 0, left = 0, right = 0;
            for (ulong seed = 4400; seed < 4403; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
                while (!e.IsFinished)
                {
                    e.Advance(0.05f);
                    var r = e.CurrentRestart;
                    foreach (var a in new[] { e.Assistant1, e.Assistant2 })
                    {
                        if (!a.FlagRaised || r == null || (r.Type != RestartType.IndirectFreeKick && r.Type != RestartType.DirectFreeKick)) continue;
                        raised++;
                        if (a.FlagDir != r.Team.AttackDir) wrong++;
                        if (a.FlagDir > 0) right++; else left++;
                    }
                }
            }
            Assert.True(raised > 20, "flags raised for free kicks: " + raised);
            Assert.Equal(0, wrong);
            Assert.True(left > 0 && right > 0, $"the flag goes to both sides: left {left}, right {right}");
        }

        [Fact]
        public void NothingRestartsWhileTheRefereeIsShowingACard()
        {
            int cards = 0, early = 0;
            for (ulong seed = 4500; seed < 4504; seed++)
            {
                var e = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), seed, MatchConfig.CreateDefault());
                e.RestartExecuting += r => { if (e.Referee.CardShown != 0) early++; };
                while (!e.IsFinished)
                {
                    e.Advance(0.05f);
                    foreach (var ev in e.DrainEvents()) if (ev.Type == MatchEventType.YellowCard || ev.Type == MatchEventType.RedCard) cards++;
                }
            }
            Assert.True(cards >= 4, "cards " + cards);
            Assert.Equal(0, early);
        }

        [Fact]
        public void InjuriesComeFromContactNotFromNowhere()
        {
            var es = new MatchEngine[10];
            Parallel.For(0, es.Length, i =>
            {
                es[i] = new MatchEngine(DemoTeams.Red(), DemoTeams.Blue(), 4600 + (ulong)i, MatchConfig.CreateDefault()) { CollectDiagnostics = true };
                es[i].RunToEnd();
            });
            int strain = es.Sum(e => e.Diagnostics.Where(kv => kv.Key.StartsWith("injury.") && kv.Key.EndsWith(".strain")).Sum(kv => kv.Value));
            int contact = es.Sum(e => e.Diagnostics.Where(kv => kv.Key.StartsWith("injury.") && !kv.Key.EndsWith(".strain")).Sum(kv => kv.Value));
            Assert.True(contact >= 3, "injuries from contact " + contact);
            Assert.True(strain <= contact / 4, $"strains {strain} v contact injuries {contact}");
            Assert.Equal(0, es.Sum(e => e.Diagnostics.GetValueOrDefault("foul.nocontact")));
        }
    }
}
