using System;
using System.Text.Json.Serialization;

namespace Selvagen.Core.Models
{
    public class ColorLegendPayload
    {
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("variant")]
        public string Variant { get; set; }

        [JsonPropertyName("colors")]
        public string[] Colors { get; set; }

        [JsonPropertyName("labels")]
        public string[] Labels { get; set; }

        [JsonPropertyName("domain_min")]
        public float? DomainMin { get; set; }

        [JsonPropertyName("domain_max")]
        public float? DomainMax { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }
    }

    public class ColorLegendInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("variant")]
        public string Variant { get; set; }

        [JsonPropertyName("colors")]
        public string[] Colors { get; set; }

        [JsonPropertyName("labels")]
        public string[] Labels { get; set; }

        [JsonPropertyName("domain_min")]
        public float? DomainMin { get; set; }

        [JsonPropertyName("domain_max")]
        public float? DomainMax { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
