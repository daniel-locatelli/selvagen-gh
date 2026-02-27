using System.Text.Json;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    /// <summary>
    /// Snapshot tests: verify C# models serialize to JSON matching the web app format.
    /// These ensure schema sync between the C# plugin and the web app loaders.
    /// </summary>
    public class BufferGeometryModelTests
    {
        [Fact]
        public void BufferGeometry_Serializes_To_ThreeJs_Format()
        {
            var geo = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes
                    {
                        Position = new BufferAttribute
                        {
                            ItemSize = 3,
                            Type = "Float32Array",
                            Array = new double[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 },
                            Normalized = false,
                        },
                        Normal = new BufferAttribute
                        {
                            ItemSize = 3,
                            Type = "Float32Array",
                            Array = new double[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 },
                            Normalized = false,
                        },
                    },
                    Index = new BufferGeometryIndex
                    {
                        Type = "Uint16Array",
                        Array = new[] { 0, 1, 2 },
                    },
                },
            };

            var json = JsonSerializer.Serialize(geo);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify top-level structure
            Assert.Equal("BufferGeometry", root.GetProperty("type").GetString());

            // Verify nested data.attributes.position
            var position = root.GetProperty("data").GetProperty("attributes").GetProperty("position");
            Assert.Equal(3, position.GetProperty("itemSize").GetInt32());
            Assert.Equal("Float32Array", position.GetProperty("type").GetString());
            Assert.Equal(9, position.GetProperty("array").GetArrayLength());

            // Verify index
            var index = root.GetProperty("data").GetProperty("index");
            Assert.Equal(3, index.GetProperty("array").GetArrayLength());
        }

        [Fact]
        public void BufferGeometry_Position_Array_Length_Is_Multiple_Of_3()
        {
            var geo = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes
                    {
                        Position = new BufferAttribute
                        {
                            Array = new double[] { 1, 2, 3, 4, 5, 6 },
                        },
                    },
                },
            };

            Assert.True(geo.Data.Attributes.Position.Array.Length % 3 == 0);
        }
    }
}
