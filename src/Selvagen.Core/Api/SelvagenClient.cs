using System;
using System.Collections.Generic;
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
    public class SelvagenClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _supabaseUrl;
        private readonly string _anonKey;

        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;

        /// <summary>
        /// Buffer before actual expiry to trigger a refresh (avoids race conditions).
        /// </summary>
        private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Optional logging delegate. Set this from your GH plugin to route logs to PluginLogger.
        /// </summary>
        public static Action<string> LogAction { get; set; }

        private void Log(string message)
        {
            if (LogAction != null)
                LogAction(message);
            else
                System.Diagnostics.Debug.WriteLine(message);
        }

        /// <summary>
        /// Whether the client has a valid access token (may still be expired).
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        public SelvagenClient(string supabaseUrl, string anonKey)
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

            var response = await _http.PostAsync(url, content).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(json);
                throw new SelvagenApiException(
                    $"Login failed: {apiError?.Error ?? response.StatusCode.ToString()}",
                    (int)response.StatusCode,
                    apiError?.Error);
            }

            var result = JsonSerializer.Deserialize<AuthTokenResponse>(json);
            StoreTokens(result);

            return result;
        }

        /// <summary>
        /// Refresh the access token using the stored refresh token.
        /// Called automatically before API requests when the token is near expiry.
        /// </summary>
        public async Task RefreshSessionAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
                throw new InvalidOperationException("No refresh token available. Call LoginAsync first.");

            var url = $"{_supabaseUrl}/auth/v1/token?grant_type=refresh_token";
            var body = JsonSerializer.Serialize(new { refresh_token = _refreshToken });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            Log("Refreshing access token...");
            var response = await _http.PostAsync(url, content).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(json);
                throw new SelvagenApiException(
                    $"Token refresh failed: {apiError?.Error ?? response.StatusCode.ToString()}",
                    (int)response.StatusCode,
                    apiError?.Error);
            }

            var result = JsonSerializer.Deserialize<AuthTokenResponse>(json);
            StoreTokens(result);
            Log("Token refreshed successfully.");
        }

        private void StoreTokens(AuthTokenResponse result)
        {
            _accessToken = result.AccessToken;
            _refreshToken = result.RefreshToken;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn);
        }

        private bool IsTokenExpiringSoon =>
            _tokenExpiresAt != DateTime.MinValue && DateTime.UtcNow >= _tokenExpiresAt - RefreshBuffer;

        private async Task EnsureValidTokenAsync()
        {
            if (!IsAuthenticated)
                throw new InvalidOperationException("Not authenticated. Call LoginAsync first.");

            if (IsTokenExpiringSoon && !string.IsNullOrEmpty(_refreshToken))
            {
                await RefreshSessionAsync().ConfigureAwait(false);
            }
        }

        // ── Projects ────────────────────────────────────────────────────────

        /// <summary>
        /// List projects the current user has access to.
        /// </summary>
        public async Task<ProjectInfo[]> ListProjectsAsync()
        {
            var response = await SendAuthorizedAsync(HttpMethod.Get, "/functions/v1/plugin-projects").ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Log the raw response so we can diagnose issues
            Log($"ListProjectsAsync: HTTP {(int)response.StatusCode}, body = {json}");

            if (!response.IsSuccessStatusCode)
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(json);
                throw new SelvagenApiException(
                    $"List projects failed: {apiError?.Error ?? response.StatusCode.ToString()}",
                    (int)response.StatusCode,
                    apiError?.Error);
            }

            return JsonSerializer.Deserialize<ProjectInfo[]>(json);
        }

        // ── Upload ──────────────────────────────────────────────────────────

        /// <summary>
        /// Upload a mesh to a project.
        /// </summary>
        public async Task<UploadResult> UploadMeshAsync(string projectId, string name, BufferGeometry geometry, string type = null, Dictionary<string, object> metadata = null)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));

            var payload = new
            {
                name,
                project_id = projectId,
                geometry_data = geometry,
                type,
                metadata,
            };
            return await PostUploadAsync("/functions/v1/plugin-upload-mesh", payload).ConfigureAwait(false);
        }

        /// <summary>
        /// Upload a curve set to a project.
        /// </summary>
        public async Task<UploadResult> UploadCurvesAsync(string projectId, string name, CurveSet curveSet)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (curveSet == null) throw new ArgumentNullException(nameof(curveSet));

            var payload = new
            {
                name,
                project_id = projectId,
                geometry_data = curveSet,
            };
            return await PostUploadAsync("/functions/v1/plugin-upload-curves", payload).ConfigureAwait(false);
        }

        /// <summary>
        /// Upload a text label set to a project.
        /// </summary>
        public async Task<UploadResult> UploadText3DAsync(string projectId, string name, Text3DSet textSet)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (textSet == null) throw new ArgumentNullException(nameof(textSet));

            var payload = new
            {
                name,
                project_id = projectId,
                text_data = textSet,
            };
            return await PostUploadAsync("/functions/v1/plugin-upload-text3d", payload).ConfigureAwait(false);
        }

        // ── Asset Queries ─────────────────────────────────────────────

        /// <summary>
        /// List meshes belonging to a project.
        /// </summary>
        public async Task<AssetInfo[]> ListMeshesAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            var path = $"/rest/v1/meshes?project_id=eq.{projectId}&select=id,name,type,created_at&order=created_at.desc";
            return await QueryAssetsAsync(path, "meshes").ConfigureAwait(false);
        }

        /// <summary>
        /// List curve sets belonging to a project.
        /// </summary>
        public async Task<AssetInfo[]> ListCurveSetsAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            var path = $"/rest/v1/curve_sets?project_id=eq.{projectId}&select=id,name,created_at&order=created_at.desc";
            return await QueryAssetsAsync(path, "curve_sets").ConfigureAwait(false);
        }

        /// <summary>
        /// List text 3D sets belonging to a project.
        /// </summary>
        public async Task<AssetInfo[]> ListText3DSetsAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            var path = $"/rest/v1/text_3d_sets?project_id=eq.{projectId}&select=id,name,created_at&order=created_at.desc";
            return await QueryAssetsAsync(path, "text_3d_sets").ConfigureAwait(false);
        }

        /// <summary>
        /// Delete an asset by table name and ID.
        /// </summary>
        public async Task DeleteAssetAsync(string tableName, string assetId)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(assetId)) throw new ArgumentNullException(nameof(assetId));

            var path = $"/rest/v1/{tableName}?id=eq.{assetId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_supabaseUrl}{path}");
            await EnsureValidTokenAsync().ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("apikey", _anonKey);

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new SelvagenApiException($"Delete asset failed: {json}", (int)response.StatusCode);
            }
        }

        private async Task<AssetInfo[]> QueryAssetsAsync(string path, string label)
        {
            var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"List {label} failed: {json}", (int)response.StatusCode);

            return JsonSerializer.Deserialize<AssetInfo[]>(json);
        }

        // ── Module Records ───────────────────────────────────────────────

        /// <summary>
        /// Create a new module record for a project (e.g., topography, geology).
        /// Returns the created record.
        /// </summary>
        public async Task<ModuleRecord> CreateModuleRecordAsync(string tableName, string projectId)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));

            var path = $"/rest/v1/{tableName}";
            var payload = new Dictionary<string, object> { { "project_id", projectId } };
            var body = JsonSerializer.Serialize(payload);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}{path}");
            await EnsureValidTokenAsync().ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("apikey", _anonKey);
            request.Headers.Add("Prefer", "return=representation");
            request.Content = content;

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"Create module record ({tableName}) failed: {json}", (int)response.StatusCode);

            var records = JsonSerializer.Deserialize<ModuleRecord[]>(json);
            if (records == null || records.Length == 0)
                throw new SelvagenApiException($"Create module record ({tableName}) returned no data", 0);

            return records[0];
        }

        // ── Animation Sequences ──────────────────────────────────────────

        /// <summary>
        /// Create an animation sequence record.
        /// </summary>
        public async Task<AnimationSequenceInfo> CreateAnimationSequenceAsync(
            string projectId, string name, string baseMeshId,
            int frameCount, double fps = 1.0, bool loop = false)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrEmpty(baseMeshId)) throw new ArgumentNullException(nameof(baseMeshId));

            var payload = new Dictionary<string, object>
            {
                { "project_id", projectId },
                { "name", name },
                { "asset_type", "mesh" },
                { "base_asset_id", baseMeshId },
                { "frame_count", frameCount },
                { "fps", fps },
                { "loop", loop },
            };
            var body = JsonSerializer.Serialize(payload);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            await EnsureValidTokenAsync().ConfigureAwait(false);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/rest/v1/animation_sequences");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("apikey", _anonKey);
            request.Headers.Add("Prefer", "return=representation");
            request.Content = content;

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"Create animation sequence failed: {json}", (int)response.StatusCode);

            var records = JsonSerializer.Deserialize<AnimationSequenceInfo[]>(json);
            if (records == null || records.Length == 0)
                throw new SelvagenApiException("Create animation sequence returned no data", 0);

            return records[0];
        }

        /// <summary>
        /// Upload a single animation frame.
        /// </summary>
        public async Task UploadAnimationFrameAsync(
            string sequenceId, int frameIndex, AnimationFrameData frameData, string label = null)
        {
            if (string.IsNullOrEmpty(sequenceId)) throw new ArgumentNullException(nameof(sequenceId));
            if (frameData == null) throw new ArgumentNullException(nameof(frameData));

            var payload = new Dictionary<string, object>
            {
                { "sequence_id", sequenceId },
                { "frame_index", frameIndex },
                { "geometry_data", frameData },
            };
            if (label != null)
                payload["label"] = label;

            var body = JsonSerializer.Serialize(payload);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            await EnsureValidTokenAsync().ConfigureAwait(false);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/rest/v1/animation_frames");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("apikey", _anonKey);
            request.Content = content;

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new SelvagenApiException($"Upload animation frame {frameIndex} failed: {json}", (int)response.StatusCode);
            }
        }

        // ── Direct REST Queries (PostgREST) ──────────────────────────

        /// <summary>
        /// List clients of the firm.
        /// </summary>
        public async Task<FirmInfo[]> ListClientsAsync()
        {
            // Query firms table for type = 'client'
            var path = "/rest/v1/firms?type=eq.client&select=id,legal_name,type";
            var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"List clients failed: {json}", (int)response.StatusCode);

            return JsonSerializer.Deserialize<FirmInfo[]>(json);
        }

        /// <summary>
        /// List projects for a specific client.
        /// </summary>
        public async Task<ProjectInfo[]> ListProjectsByClientAsync(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return await ListProjectsAsync();

            var path = $"/rest/v1/projects?client_id=eq.{clientId}&select=id,name,created_at";
            var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"List projects by client failed: {json}", (int)response.StatusCode);

            return JsonSerializer.Deserialize<ProjectInfo[]>(json);
        }

        /// <summary>
        /// List module records (Topography, Geology, etc.) for a specific project.
        /// </summary>
        public async Task<ModuleRecord[]> ListModuleRecordsAsync(string tableName, string projectId)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));

            var path = $"/rest/v1/{tableName}?project_id=eq.{projectId}&select=id,project_id,created_at";
            var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"List module records ({tableName}) failed: {json}", (int)response.StatusCode);

            return JsonSerializer.Deserialize<ModuleRecord[]>(json);
        }

        /// <summary>
        /// Update a specific property on a module record (Topography, Geology, etc.).
        /// </summary>
        public async Task UpdateModulePropertyAsync(string tableName, string recordId, string propertyName, object value)
        {
            await UpdateModuleAsync(tableName, recordId, new Dictionary<string, object> { { propertyName, value } })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Batch-update multiple columns on a module record in a single PATCH request.
        /// </summary>
        public async Task UpdateModuleAsync(string tableName, string recordId, Dictionary<string, object> values)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(recordId)) throw new ArgumentNullException(nameof(recordId));
            if (values == null || values.Count == 0) return;

            var path = $"/rest/v1/{tableName}?id=eq.{recordId}";
            var body = JsonSerializer.Serialize(values);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            await EnsureValidTokenAsync().ConfigureAwait(false);
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_supabaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("apikey", _anonKey);
            request.Headers.Add("Prefer", "return=representation");
            request.Content = content;

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new SelvagenApiException($"Update {tableName} failed: {json}", (int)response.StatusCode);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private async Task<UploadResult> PostUploadAsync(string path, object payload)
        {
            var body = JsonSerializer.Serialize(payload);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await SendAuthorizedAsync(HttpMethod.Post, path, content).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(json);
                throw new SelvagenApiException(
                    $"Upload failed ({response.StatusCode}): {apiError?.Error ?? json}",
                    (int)response.StatusCode,
                    apiError?.Error);
            }

            return JsonSerializer.Deserialize<UploadResult>(json);
        }

        private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string path, HttpContent content = null)
        {
            await EnsureValidTokenAsync().ConfigureAwait(false);

            var request = new HttpRequestMessage(method, $"{_supabaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            // Explicitly add apikey to the request (some .NET runtimes don't forward DefaultRequestHeaders via SendAsync)
            request.Headers.Add("apikey", _anonKey);

            Log($"SendAuthorizedAsync: {method} {_supabaseUrl}{path}");
            Log($"  Authorization: Bearer {_accessToken?.Substring(0, Math.Min(20, _accessToken?.Length ?? 0))}...");
            Log($"  apikey: {_anonKey?.Substring(0, Math.Min(20, _anonKey?.Length ?? 0))}...");

            if (content != null)
                request.Content = content;

            return await _http.SendAsync(request).ConfigureAwait(false);
        }

        public override string ToString()
            => IsAuthenticated ? $"Selvagen Client [authenticated]" : "Selvagen Client [not authenticated]";

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
