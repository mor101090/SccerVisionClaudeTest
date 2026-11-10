using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SoccerVision.Simulation;
using Xunit;

namespace SoccerVision.Simulation.Tests
{
    /// <summary>
    /// Ball physics stage 12: every parameter of the ball's physics is classified (A-E) in docs/BALL_PARAMETERS.md. A field added to a model class
    /// without a row there fails this test.
    /// </summary>
    public class BallParameterTests
    {
        private static string Doc()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docs", "BALL_PARAMETERS.md"))) d = d.Parent;
            Assert.NotNull(d);
            return File.ReadAllText(Path.Combine(d.FullName, "docs", "BALL_PARAMETERS.md"));
        }

        [Theory]
        [InlineData(typeof(BallSpec))]
        [InlineData(typeof(AeroModel))]
        [InlineData(typeof(GroundModel))]
        [InlineData(typeof(KickModel))]
        [InlineData(typeof(CollisionModel))]
        [InlineData(typeof(DeformationModel))]
        public void EveryFieldOfTheModelIsClassified(Type model)
        {
            var doc = Doc();
            var fields = model.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                              .Where(f => f.FieldType != model && f.Name != "Name") // (the static Reference instances; a profile's name)
                              .Select(f => f.Name).ToList();
            Assert.NotEmpty(fields);
            var missing = fields.Where(n => !doc.Contains("`" + n + "`", StringComparison.Ordinal)).ToList();
            Assert.True(missing.Count == 0, model.Name + ": not in docs/BALL_PARAMETERS.md: " + string.Join(", ", missing));
        }

        [Fact]
        public void TheAirAndTheStepAreClassified()
        {
            var doc = Doc();
            foreach (var n in new[] { "Gravity", "AirDensity", "AirDynamicViscosity", "FixedStep", "AirDragK" })
                Assert.Contains("`" + n + "`", doc);
        }

        [Fact]
        public void EveryRowHasAClassAndTheSummaryCountsThem()
        {
            var doc = Doc();
            var body = doc.Substring(0, doc.IndexOf("## Summary", StringComparison.Ordinal));
            var counts = new int[5];
            foreach (var line in body.Split('\n'))
            {
                if (!line.StartsWith("| ", StringComparison.Ordinal) || line.StartsWith("| Parameter", StringComparison.Ordinal)) continue;
                var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                Assert.True(cells.Length >= 4, "a row with too few cells: " + line);
                var m = Regex.Match(cells[2], "^[A-E]");
                Assert.True(m.Success, "a row without a class: " + line);
                counts[m.Value[0] - 'A']++;
            }
            var summary = doc.Substring(doc.IndexOf("## Summary", StringComparison.Ordinal));
            for (int k = 0; k < 5; k++)
                Assert.Matches("\\| " + (char)('A' + k) + " \\| " + counts[k] + " \\|", summary);
        }
    }
}
