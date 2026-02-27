using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Selvagen.Core.Models;

namespace Selvagen.Core.Api
{
    /// <summary>
    /// HTTP client for the Selvagen Edge Functions API + Supabase Auth.
    /// </summary>
    public class SelvigenClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _supabaseUrl;
        private readonly string _anonKey;

        private string _accessToken;
        private string _refreshToken;

        /// <summary>
        /// Whether the client has a valid access token (may still be expired).
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        public SelvigenClient(string supabaseUrl, string anonKey)
        {
            _supabaseUrl = supabaseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(supabaseUrl));
            _anonKey = anonKey ?? throw new ArgumentNullException(nameof(anonKey));
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("apikey", _anonKey);
        }

        // ── Auth ────────────────────────────────────────────────────────────

        /// <summary>
        /// Sign in with email/password via Supabase Auth.
        /// </summary>
        public async Task<AuthTokenResponse> LoginAsync(string email, string password)
        {
            var url = $"{_supabaseUrl}/auth/v1/token?grant_type=password";
            var body = JsonSerializer.Serialize(new { email, password });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(json);
                throw new Exception($"Login failed: {apiError?.Error ?? response.StatusCode.ToString()}");
            }

            var result = JsonSerializer.Deserialize<AuthTokenResponse>(json);
            _accessToken = result.AccessToken;
            _refreshToken = result.RefreshToken;

            return result;
        }

        // ── Projects ────────────────────────────────────────────────────────

        /// <summary>
        /// List projects the current user has access to.
        /// </summary>
        public async Task<ProjectInfo[]> ListProjectsAsync()
        {
            var response = await SendAuthorizedAsync(HttpMethod.Get, "/functions/v1/plugin-projects");
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(json);
                throw new Exception($"List projects failed: {apiError?.Error ?? response.StatusCode.ToString()}");
            }

            return JsonSerializer.Deserialize<ProjectInfo[]>(json);
        }

        // ── Upload ──────────────────────────────────────────────────────────

        /// <summary>
        /// Upload a mesh to a project.
        /// </summary>
        public async Task<UploadResult> UploadMeshAsync(string projectId, string name, BufferGeometry geometry, string type = null, object metadata = null)
        {
            var payload = new
            {
                name,
                project_id = projectId,
                geometry_data = geometry,
                type,
                metadata,
            };
            return await PostUploadAsync("/functions/v1/plugin-upload-mesh", payload);
        }

        /// <summary>
        /// Upload a curve set to a project.
        /// </summary>
        public async Task<UploadResult> UploadCurvesAsync(string projectId, string name, CurveSet curveSet)
        {
            var payload = new
            {
                name,
                project_id = projectId,
                geometry_data = curveSet,
            };
            return await PostUploadAsync("/functions/v1/plugin-upload-curves", payload);
        }

        /// <summary>
        /// Upload a text label set to a project.
        /// </summary>
        public async Task<UploadResult> UploadText3DAsync(string projectId, string name, Text3DSet textSet)
        {
            var payload = new
            {
                name,
                project_id = projectId,
                text_data = textSet,
            };
            return await PostUploadAsync("/functions/v1/plugin-upload-text3d", payload);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private async Task<UploadResult> PostUploadAsync(string path, object payload)
        {
            var body = JsonSerializer.Serialize(payload);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await SendAuthorizedAsync(HttpMethod.Post, path, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(json);
                throw new Exception($"Upload failed ({response.StatusCode}): {apiError?.Error ?? json}");
            }

            return JsonSerializer.Deserialize<UploadResult>(json);
        }

        private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string path, HttpContent content = null)
        {
            if (!IsAuthenticated)
                throw new InvalidOperationException("Not authenticated. Call LoginAsync first.");

            var request = new HttpRequestMessage(method, $"{_supabaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            if (content != null)
                request.Content = content;

            return await _http.SendAsync(request);
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
