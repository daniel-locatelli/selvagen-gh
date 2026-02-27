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
    /// Error response from Edge Functions.
    /// </summary>
    public class ApiError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";
    }
}
