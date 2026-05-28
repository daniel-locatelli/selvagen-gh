using System;
using System.Text.Json.Serialization;

namespace Selvagen.Core.Models
{
    /// <summary>
    /// Full row as returned by PostgREST.
    /// </summary>
    public class CustomPropertyInfo
    {
        [JsonPropertyName("id")]         public string Id { get; set; }
        [JsonPropertyName("project_id")] public string ProjectId { get; set; }
        [JsonPropertyName("key")]        public string Key { get; set; }
        [JsonPropertyName("value")]      public string Value { get; set; }
        [JsonPropertyName("value_type")] public string ValueType { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Lean shape for outbound upsert requests. project_id is set per-row
    /// (not pulled out as a query param) so PostgREST native batch upsert works.
    /// </summary>
    public class CustomPropertyUpsert
    {
        [JsonPropertyName("project_id")] public string ProjectId { get; set; }
        [JsonPropertyName("key")]        public string Key { get; set; }
        [JsonPropertyName("value")]      public string Value { get; set; }
        [JsonPropertyName("value_type")] public string ValueType { get; set; }
    }
}
