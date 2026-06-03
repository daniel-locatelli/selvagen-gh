using System;
using System.Text.Json.Serialization;

namespace Selvagen.Core.Models
{
    /// <summary>
    /// Response from Supabase Auth token endpoint.
    /// </summary>
    public class AuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("user")]
        public AuthUser User { get; set; }
    }

    public class AuthUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";
    }

    /// <summary>
    /// Response from plugin-projects Edge Function.
    /// </summary>
    public class ProjectInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>
    /// Response from upload Edge Functions.
    /// </summary>
    public class UploadResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>
    /// Lightweight firm info for list-clients flow.
    /// </summary>
    public class FirmInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("legal_name")]
        public string LegalName { get; set; } = "";
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }

    /// <summary>
    /// Lightweight asset info returned when listing meshes, curve_sets, or label_sets.
    /// </summary>
    public class AssetInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>
    /// Result of the delete_asset_by_id RPC. Status is one of:
    /// "deleted" (Table is populated), "forbidden", or "not_found".
    /// </summary>
    public class DeleteAssetResult
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("table")]
        public string Table { get; set; } = "";
    }

    /// <summary>
    /// Response when creating an animation sequence.
    /// </summary>
    public class AnimationSequenceInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("frame_count")]
        public int FrameCount { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>
    /// Represents a generic module record (Topography, Geology, etc.).
    /// </summary>
    public class ModuleRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = "";

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>
    /// Error response from Edge Functions.
    /// </summary>
    public class ApiError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// Exception thrown when the Selvagen API returns an error response.
    /// </summary>
    public class SelvagenApiException : Exception
    {
        /// <summary>HTTP status code from the API response.</summary>
        public int StatusCode { get; }

        /// <summary>Error message from the API response body, if available.</summary>
        public string ApiError { get; }

        public SelvagenApiException(string message, int statusCode, string apiError = null)
            : base(message)
        {
            StatusCode = statusCode;
            ApiError = apiError;
        }
    }

    /// <summary>
    /// Full mesh row from PostgREST, including geometry data.
    /// </summary>
    public class MeshAssetFull
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("geometry_data")]
        public BufferGeometry GeometryData { get; set; }

        [JsonPropertyName("geometry_url")]
        public string GeometryUrl { get; set; }
    }

    /// <summary>
    /// Full curve set row from PostgREST, including geometry data.
    /// </summary>
    public class CurveSetAssetFull
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("geometry_data")]
        public CurveSet GeometryData { get; set; }

        [JsonPropertyName("geometry_url")]
        public string GeometryUrl { get; set; }
    }

    public class LabelSetAssetFull
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("text_data")]
        public LabelSet TextData { get; set; }

        [JsonPropertyName("geometry_url")]
        public string GeometryUrl { get; set; }
    }

    /// <summary>
    /// Full animation sequence metadata from PostgREST.
    /// </summary>
    public class AnimationSequenceFull
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("fps")]
        public double? Fps { get; set; }

        [JsonPropertyName("loop")]
        public bool? Loop { get; set; }

        [JsonPropertyName("base_asset_id")]
        public string BaseAssetId { get; set; } = "";

        [JsonPropertyName("frame_count")]
        public int FrameCount { get; set; }
    }

    /// <summary>
    /// Full animation frame row from PostgREST, including geometry data.
    /// </summary>
    public class AnimationFrameFull
    {
        [JsonPropertyName("frame_index")]
        public int FrameIndex { get; set; }

        [JsonPropertyName("geometry_data")]
        public AnimationFrameData GeometryData { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }
    }
}
