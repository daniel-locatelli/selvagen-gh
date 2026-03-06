using System.Text.Json.Serialization;

namespace Selvagen.Core.Models
{
    /// <summary>
    /// Data for a single animation frame.
    /// Two formats supported:
    ///   "positions" — position array only (same topology as base mesh)
    ///   "buffer_geometry" — full BufferGeometry (topology changed)
    /// </summary>
    public class AnimationFrameData
    {
        [JsonPropertyName("format")]
        public string Format { get; set; } = "positions";

        /// <summary>
        /// Flat position array [x,y,z, x,y,z, ...] in Y-up coordinates.
        /// Used when Format = "positions".
        /// </summary>
        [JsonPropertyName("positions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[] Positions { get; set; }

        /// <summary>
        /// Full BufferGeometry for frames where topology changes.
        /// Used when Format = "buffer_geometry".
        /// </summary>
        [JsonPropertyName("geometry")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BufferGeometry Geometry { get; set; }
    }
}
