using System.Text.Json.Serialization;

namespace Selvagen.Core.Models
{
    /// <summary>
    /// CurveSet JSON format matching CurveSetLoader.tsx in the web app.
    /// Points are stored as flat arrays [x,y,z, x,y,z, ...] in Y-up coordinates.
    /// </summary>
    public class CurveSet
    {
        [JsonPropertyName("curves")]
        public CurveData[] Curves { get; set; } = System.Array.Empty<CurveData>();
    }

    public class CurveData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("points")]
        public double[] Points { get; set; } = System.Array.Empty<double>();

        [JsonPropertyName("closed")]
        public bool Closed { get; set; } = false;

        [JsonPropertyName("color")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Color { get; set; }

        [JsonPropertyName("linewidth")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Linewidth { get; set; }
    }
}
