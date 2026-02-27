using System.Text.Json;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class CurveSetModelTests
    {
        [Fact]
        public void CurveSet_Serializes_To_Expected_Format()
        {
            var curveSet = new CurveSet
            {
                Curves = new[]
                {
                    new CurveData
                    {
                        Id = "curve-0",
                        Points = new double[] { 0, 0, 0, 1, 1, 0, 2, 0, 0 },
                        Closed = false,
                    },
                    new CurveData
                    {
                        Id = "curve-1",
                        Points = new double[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 },
                        Closed = true,
                        Color = "#ff0000",
                        Linewidth = 2.0,
                    },
                },
            };

            var json = JsonSerializer.Serialize(curveSet);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Must have "curves" array
            var curves = root.GetProperty("curves");
            Assert.Equal(2, curves.GetArrayLength());

            // First curve
            var c0 = curves[0];
            Assert.Equal("curve-0", c0.GetProperty("id").GetString());
            Assert.Equal(9, c0.GetProperty("points").GetArrayLength());
            Assert.False(c0.GetProperty("closed").GetBoolean());

            // Second curve — has optional color + linewidth
            var c1 = curves[1];
            Assert.True(c1.GetProperty("closed").GetBoolean());
            Assert.Equal("#ff0000", c1.GetProperty("color").GetString());
            Assert.Equal(2.0, c1.GetProperty("linewidth").GetDouble());
        }

        [Fact]
        public void CurveData_Omits_Null_Optional_Fields()
        {
            var curve = new CurveData
            {
                Id = "test",
                Points = new double[] { 0, 0, 0 },
            };

            var json = JsonSerializer.Serialize(curve);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // color and linewidth should not be present when null
            Assert.False(root.TryGetProperty("color", out _));
            Assert.False(root.TryGetProperty("linewidth", out _));
        }
    }
}
