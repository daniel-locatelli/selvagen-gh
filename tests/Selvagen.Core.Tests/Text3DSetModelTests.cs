using System.Text.Json;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class Text3DSetModelTests
    {
        [Fact]
        public void Text3DSet_Serializes_To_Expected_Format()
        {
            var textSet = new Text3DSet
            {
                Labels = new[]
                {
                    new LabelData
                    {
                        Id = "label-0",
                        Text = "Point A",
                        Position = new double[] { 1.0, 2.0, 3.0 },
                        FontSize = 5.0,
                        Color = "#ff0000",
                    },
                    new LabelData
                    {
                        Id = "label-1",
                        Text = "Point B",
                        Position = new double[] { 4.0, 5.0, 6.0 },
                    },
                },
            };

            var json = JsonSerializer.Serialize(textSet);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Must have "labels" array
            var labels = root.GetProperty("labels");
            Assert.Equal(2, labels.GetArrayLength());

            // First label
            var l0 = labels[0];
            Assert.Equal("label-0", l0.GetProperty("id").GetString());
            Assert.Equal("Point A", l0.GetProperty("text").GetString());
            Assert.Equal(3, l0.GetProperty("position").GetArrayLength());
            Assert.Equal(1.0, l0.GetProperty("position")[0].GetDouble());
            Assert.Equal(5.0, l0.GetProperty("fontSize").GetDouble());

            // Second label — no optional fields
            var l1 = labels[1];
            Assert.False(l1.TryGetProperty("fontSize", out _));
            Assert.False(l1.TryGetProperty("color", out _));
        }

        [Fact]
        public void LabelData_Position_Is_3_Element_Array()
        {
            var label = new LabelData
            {
                Id = "test",
                Text = "Hello",
                Position = new double[] { 10, 20, 30 },
            };

            var json = JsonSerializer.Serialize(label);
            var doc = JsonDocument.Parse(json);
            Assert.Equal(3, doc.RootElement.GetProperty("position").GetArrayLength());
        }
    }
}
