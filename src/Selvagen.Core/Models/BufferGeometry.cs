using System.Text.Json.Serialization;

namespace Selvagen.Core.Models
{
    /// <summary>
    /// Three.js BufferGeometry JSON format.
    /// Parsed by THREE.BufferGeometryLoader().parse() in the web app.
    /// </summary>
    public class BufferGeometry
    {
        [JsonPropertyName("metadata")]
        public BufferGeometryMetadata Metadata { get; set; } = new BufferGeometryMetadata();

        [JsonPropertyName("type")]
        public string Type { get; set; } = "BufferGeometry";

        [JsonPropertyName("data")]
        public BufferGeometryData Data { get; set; } = new BufferGeometryData();
    }

    public class BufferGeometryMetadata
    {
        [JsonPropertyName("version")]
        public double Version { get; set; } = 4.6;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "BufferGeometry";

        [JsonPropertyName("generator")]
        public string Generator { get; set; } = "selvagen-grasshopper";
    }

    public class BufferGeometryData
    {
        [JsonPropertyName("attributes")]
        public BufferGeometryAttributes Attributes { get; set; } = new BufferGeometryAttributes();

        [JsonPropertyName("index")]
        public BufferGeometryIndex Index { get; set; }
    }

    public class BufferGeometryAttributes
    {
        [JsonPropertyName("position")]
        public BufferAttribute Position { get; set; } = new BufferAttribute();

        [JsonPropertyName("normal")]
        public BufferAttribute Normal { get; set; }
    }

    public class BufferAttribute
    {
        [JsonPropertyName("itemSize")]
        public int ItemSize { get; set; } = 3;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "Float32Array";

        [JsonPropertyName("array")]
        public double[] Array { get; set; } = System.Array.Empty<double>();

        [JsonPropertyName("normalized")]
        public bool Normalized { get; set; } = false;
    }

    public class BufferGeometryIndex
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Uint32Array";

        [JsonPropertyName("array")]
        public int[] Array { get; set; } = System.Array.Empty<int>();
    }
}
