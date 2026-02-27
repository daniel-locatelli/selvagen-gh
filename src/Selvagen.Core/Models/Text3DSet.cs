using System.Text.Json.Serialization;

namespace Selvagen.Core.Models
{
    /// <summary>
    /// Text3DSet JSON format matching Text3DSetLoader.tsx in the web app.
    /// Positions are [x,y,z] tuples in Y-up coordinates.
    /// </summary>
    public class Text3DSet
    {
        [JsonPropertyName("labels")]
        public LabelData[] Labels { get; set; } = System.Array.Empty<LabelData>();
    }

    public class LabelData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("position")]
        public double[] Position { get; set; } = new double[3];

        [JsonPropertyName("rotation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double[] Rotation { get; set; }

        [JsonPropertyName("fontSize")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? FontSize { get; set; }

        [JsonPropertyName("color")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Color { get; set; }

        [JsonPropertyName("anchorX")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AnchorX { get; set; }

        [JsonPropertyName("anchorY")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AnchorY { get; set; }
    }
}
