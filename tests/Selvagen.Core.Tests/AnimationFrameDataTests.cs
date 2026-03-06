using System.Text.Json;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    /// <summary>
    /// Serialization tests for animation frame data models.
    /// Follows the same pattern as BufferGeometryModelTests — constructs models
    /// directly and verifies JSON output matches what the platform expects.
    /// </summary>
    public class AnimationFrameDataTests
    {
        [Fact]
        public void PositionFrame_Serializes_With_Correct_Format()
        {
            var frame = new AnimationFrameData
            {
                Format = "positions",
                Positions = new double[] { 0, 5, 0, 1, 5, 0, 1, 5.5, -1, 0, 5.5, -1 },
            };

            var json = JsonSerializer.Serialize(frame);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("positions", root.GetProperty("format").GetString());
            Assert.True(root.TryGetProperty("positions", out var posArray));
            Assert.Equal(12, posArray.GetArrayLength());
            // geometry should not be present (WhenWritingNull)
            Assert.False(root.TryGetProperty("geometry", out _));
        }

        [Fact]
        public void BufferGeometryFrame_Serializes_With_Correct_Format()
        {
            var frame = new AnimationFrameData
            {
                Format = "buffer_geometry",
                Geometry = new BufferGeometry
                {
                    Data = new BufferGeometryData
                    {
                        Attributes = new BufferGeometryAttributes
                        {
                            Position = new BufferAttribute
                            {
                                ItemSize = 3,
                                Type = "Float32Array",
                                Array = new double[] { 0, 0, 0, 1, 0, 0, 0.5, 1, 0 },
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
                },
            };

            var json = JsonSerializer.Serialize(frame);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("buffer_geometry", root.GetProperty("format").GetString());
            Assert.True(root.TryGetProperty("geometry", out var geo));
            Assert.Equal("BufferGeometry", geo.GetProperty("type").GetString());
            // positions should not be present (WhenWritingNull)
            Assert.False(root.TryGetProperty("positions", out _));
        }

        [Fact]
        public void PositionFrame_Omits_NullGeometry()
        {
            var frame = new AnimationFrameData
            {
                Format = "positions",
                Positions = new double[] { 1, 2, 3 },
                Geometry = null,
            };

            var json = JsonSerializer.Serialize(frame);
            var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.TryGetProperty("geometry", out _));
        }

        [Fact]
        public void BufferGeometryFrame_Omits_NullPositions()
        {
            var frame = new AnimationFrameData
            {
                Format = "buffer_geometry",
                Positions = null,
                Geometry = new BufferGeometry
                {
                    Data = new BufferGeometryData
                    {
                        Attributes = new BufferGeometryAttributes
                        {
                            Position = new BufferAttribute { Array = new double[] { 0, 0, 0 } },
                        },
                    },
                },
            };

            var json = JsonSerializer.Serialize(frame);
            var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.TryGetProperty("positions", out _));
        }

        [Fact]
        public void PositionArray_Length_Is_Multiple_Of_3()
        {
            // 4 vertices in Y-up coords
            var positions = new double[] { 0, 5, 0, 1, 5, 0, 1, 5.5, -1, 0, 5.5, -1 };
            var frame = new AnimationFrameData
            {
                Format = "positions",
                Positions = positions,
            };

            Assert.True(frame.Positions.Length % 3 == 0);
        }

        [Fact]
        public void Default_Format_Is_Positions()
        {
            var frame = new AnimationFrameData();
            Assert.Equal("positions", frame.Format);
        }

        [Fact]
        public void AnimationSequenceInfo_Serializes_Correctly()
        {
            var seq = new AnimationSequenceInfo
            {
                Id = "seq-abc-123",
                Name = "Test Animation",
                FrameCount = 10,
                CreatedAt = "2026-03-05T12:00:00Z",
            };

            var json = JsonSerializer.Serialize(seq);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("seq-abc-123", root.GetProperty("id").GetString());
            Assert.Equal("Test Animation", root.GetProperty("name").GetString());
            Assert.Equal(10, root.GetProperty("frame_count").GetInt32());
        }

        [Fact]
        public void FrameData_Roundtrips_Through_Json()
        {
            var original = new AnimationFrameData
            {
                Format = "positions",
                Positions = new double[] { 1.5, 2.5, -3.5, 4.0, 5.0, 6.0 },
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AnimationFrameData>(json);

            Assert.Equal(original.Format, deserialized.Format);
            Assert.Equal(original.Positions.Length, deserialized.Positions.Length);
            for (int i = 0; i < original.Positions.Length; i++)
                Assert.Equal(original.Positions[i], deserialized.Positions[i]);
            Assert.Null(deserialized.Geometry);
        }
    }
}
