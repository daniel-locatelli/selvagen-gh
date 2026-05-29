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
            _http.Timeout = TimeSpan.FromSeconds(100); // explicit; a hung server now fails instead of hanging
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

            Log($"ListProjectsAsync: HTTP {(int)response.StatusCode}");

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

        public async Task<UploadResult> UploadLabelSetAsync(string projectId, string name, LabelSet labelSet)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (labelSet == null) throw new ArgumentNullException(nameof(labelSet));

            var payload = new
            {
                name,
                project_id = projectId,
                text_data = labelSet,
            };
            return await PostUploadAsync("/functions/v1/plugin-upload-labels", payload).ConfigureAwait(false);
        }

        // ── Asset Queries ─────────────────────────────────────────────

        /// <summary>
        /// List meshes belonging to a project.
        /// </summary>
        public async Task<AssetInfo[]> ListMeshesAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            var path = $"/rest/v1/meshes?{Postgrest.Eq("project_id", projectId)}&select=id,name,type,created_at&order=created_at.desc";
            return await QueryAssetsAsync(path, "meshes").ConfigureAwait(false);
        }

        /// <summary>
        /// List curve sets belonging to a project.
        /// </summary>
        public async Task<AssetInfo[]> ListCurveSetsAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            var path = $"/rest/v1/curve_sets?{Postgrest.Eq("project_id", projectId)}&select=id,name,created_at&order=created_at.desc";
            return await QueryAssetsAsync(path, "curve_sets").ConfigureAwait(false);
        }

        public async Task<AssetInfo[]> ListLabelSetsAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            var path = $"/rest/v1/label_sets?{Postgrest.Eq("project_id", projectId)}&select=id,name,created_at&order=created_at.desc";
            return await QueryAssetsAsync(path, "label_sets").ConfigureAwait(false);
        }

        /// <summary>
        /// List animation sequences belonging to a project.
        /// </summary>
        public async Task<AssetInfo[]> ListAnimationSequencesAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            var path = $"/rest/v1/animation_sequences?{Postgrest.Eq("project_id", projectId)}&select=id,name,created_at&order=created_at.desc";
            return await QueryAssetsAsync(path, "animation_sequences").ConfigureAwait(false);
        }

        // ── Color Legends ────────────────────────────────────────────────────

        /// <summary>
        /// List all color legends belonging to a project.
        /// </summary>
        public async Task<ColorLegendInfo[]> ListColorLegendsAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));

            var path = $"/rest/v1/color_legends?{Postgrest.Eq("project_id", projectId)}&select=id,name,variant,colors,labels,domain_min,domain_max,unit&order=name";
            return await GetJsonAsync<ColorLegendInfo[]>(path, "List color legends").ConfigureAwait(false);
        }

        /// <summary>
        /// Get a single color legend by its ID.
        /// </summary>
        public async Task<ColorLegendInfo> GetColorLegendAsync(string legendId)
        {
            if (string.IsNullOrEmpty(legendId)) throw new ArgumentNullException(nameof(legendId));

            var path = $"/rest/v1/color_legends?{Postgrest.Eq("id", legendId)}&select=id,project_id,name,variant,colors,labels,domain_min,domain_max,unit,created_at,updated_at";
            var results = await GetJsonAsync<ColorLegendInfo[]>(path, "Get color legend").ConfigureAwait(false);
            if (results == null || results.Length == 0)
                throw new SelvagenApiException($"Color legend {legendId} not found", 404);

            return results[0];
        }

        /// <summary>
        /// Upsert a color legend for a project using PostgREST native upsert
        /// (atomic POST with Prefer: resolution=merge-duplicates — no client-side check-then-write).
        /// </summary>
        public async Task<ColorLegendInfo> UpsertColorLegendAsync(string projectId, string name, ColorLegendPayload payload)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            payload.ProjectId = projectId;
            payload.Name = name;

            var path = "/rest/v1/color_legends?on_conflict=project_id,name";
            var response = await SendJsonAsync(HttpMethod.Post, path, payload, "resolution=merge-duplicates,return=representation").ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"Upsert color legend failed: {json}", (int)response.StatusCode);

            var results = JsonSerializer.Deserialize<ColorLegendInfo[]>(json);
            if (results == null || results.Length == 0)
                throw new SelvagenApiException("Upsert color legend returned no data", 0);

            return results[0];
        }

        /// <summary>
        /// Delete a color legend by its ID.
        /// </summary>
        public async Task DeleteColorLegendAsync(string legendId)
        {
            if (string.IsNullOrEmpty(legendId)) throw new ArgumentNullException(nameof(legendId));

            var path = $"/rest/v1/color_legends?{Postgrest.Eq("id", legendId)}";
            var response = await SendJsonAsync(HttpMethod.Delete, path).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new SelvagenApiException($"Delete color legend failed: {json}", (int)response.StatusCode);
            }
        }

        // ── Custom Properties ────────────────────────────────────────────────

        /// <summary>
        /// List all custom properties for a project, sorted by key ascending.
        /// </summary>
        public async Task<CustomPropertyInfo[]> ListCustomPropertiesAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));

            var path = $"/rest/v1/custom_properties?{Postgrest.Eq("project_id", projectId)}&select=id,project_id,key,value,value_type,created_at,updated_at&order=key";
            return await GetJsonAsync<CustomPropertyInfo[]>(path, "List custom properties").ConfigureAwait(false) ?? new CustomPropertyInfo[0];
        }

        /// <summary>
        /// Batch-upsert custom properties. Conflicts on (project_id, key) resolve via
        /// PostgREST native merge-duplicates. Returns the resulting rows (created or updated).
        /// </summary>
        public async Task<CustomPropertyInfo[]> UpsertCustomPropertiesAsync(
            string projectId, CustomPropertyUpsert[] properties)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            if (properties.Length == 0) return new CustomPropertyInfo[0];

            // Defensive: stamp project_id on every row so callers can't forget.
            foreach (var p in properties) p.ProjectId = projectId;

            var path = "/rest/v1/custom_properties?on_conflict=project_id,key";
            var response = await SendJsonAsync(HttpMethod.Post, path, properties, "resolution=merge-duplicates,return=representation").ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"Upsert custom properties failed: {json}", (int)response.StatusCode);

            return JsonSerializer.Deserialize<CustomPropertyInfo[]>(json) ?? new CustomPropertyInfo[0];
        }

        /// <summary>
        /// Batch-delete custom properties by key, scoped to the given project.
        /// Returns the count of rows actually removed (read from Content-Range).
        /// Idempotent: deleting a non-existent key succeeds with count = 0.
        /// </summary>
        public async Task<int> DeleteCustomPropertiesAsync(string projectId, string[] keys)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Length == 0) return 0;

            var path = $"/rest/v1/custom_properties?{Postgrest.Eq("project_id", projectId)}&{Postgrest.InList("key", keys)}";

            var response = await SendJsonAsync(HttpMethod.Delete, path, null, "count=exact,return=minimal").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new SelvagenApiException($"Delete custom properties failed: {json}", (int)response.StatusCode);
            }

            // Content-Range comes back as e.g. "0-2/3" or "*/0". Last segment after '/' is the count.
            if (response.Content.Headers.TryGetValues("Content-Range", out var values))
            {
                foreach (var v in values)
                {
                    var slash = v.LastIndexOf('/');
                    if (slash >= 0 && int.TryParse(v.Substring(slash + 1), out var n)) return n;
                }
            }
            return 0;
        }

        /// <summary>
        /// Delete an asset by table name and ID.
        /// </summary>
        public async Task DeleteAssetAsync(string tableName, string assetId)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(assetId)) throw new ArgumentNullException(nameof(assetId));

            var path = $"/rest/v1/{tableName}?{Postgrest.Eq("id", assetId)}";
            var response = await SendJsonAsync(HttpMethod.Delete, path).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new SelvagenApiException($"Delete asset failed: {json}", (int)response.StatusCode);
            }
        }

        private async Task<AssetInfo[]> QueryAssetsAsync(string path, string label)
        {
            return await GetJsonAsync<AssetInfo[]>(path, $"List {label}").ConfigureAwait(false);
        }

        // ── Asset Downloads (full data) ─────────────────────────────────

        /// <summary>
        /// Fetch a single mesh including its geometry data.
        /// </summary>
        public async Task<MeshAssetFull> GetMeshAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var path = $"/rest/v1/meshes?{Postgrest.Eq("id", id)}&select=id,name,type,geometry_data,geometry_url";
            var results = await GetJsonAsync<MeshAssetFull[]>(path, "Get mesh").ConfigureAwait(false);
            if (results == null || results.Length == 0)
                throw new SelvagenApiException($"Mesh not found: {id}", 404);

            return results[0];
        }

        /// <summary>
        /// Fetch a single curve set including its geometry data.
        /// </summary>
        public async Task<CurveSetAssetFull> GetCurveSetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var path = $"/rest/v1/curve_sets?{Postgrest.Eq("id", id)}&select=id,name,geometry_data,geometry_url";
            var results = await GetJsonAsync<CurveSetAssetFull[]>(path, "Get curve set").ConfigureAwait(false);
            if (results == null || results.Length == 0)
                throw new SelvagenApiException($"Curve set not found: {id}", 404);

            return results[0];
        }

        public async Task<LabelSetAssetFull> GetLabelSetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var path = $"/rest/v1/label_sets?{Postgrest.Eq("id", id)}&select=id,name,text_data,geometry_url";
            var results = await GetJsonAsync<LabelSetAssetFull[]>(path, "Get label set").ConfigureAwait(false);
            if (results == null || results.Length == 0)
                throw new SelvagenApiException($"Label set not found: {id}", 404);

            return results[0];
        }

        /// <summary>
        /// Fetch animation sequence metadata.
        /// </summary>
        public async Task<AnimationSequenceFull> GetAnimationSequenceInfoAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var path = $"/rest/v1/animation_sequences?{Postgrest.Eq("id", id)}&select=id,name,fps,loop,base_asset_id,frame_count";
            var results = await GetJsonAsync<AnimationSequenceFull[]>(path, "Get animation sequence").ConfigureAwait(false);
            if (results == null || results.Length == 0)
                throw new SelvagenApiException($"Animation sequence not found: {id}", 404);

            return results[0];
        }

        /// <summary>
        /// Fetch all frames for an animation sequence, ordered by frame_index.
        /// </summary>
        public async Task<AnimationFrameFull[]> GetAnimationFramesAsync(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId)) throw new ArgumentNullException(nameof(sequenceId));

            var path = $"/rest/v1/animation_frames?{Postgrest.Eq("sequence_id", sequenceId)}&select=frame_index,geometry_data,label&order=frame_index";
            return await GetJsonAsync<AnimationFrameFull[]>(path, "Get animation frames").ConfigureAwait(false) ?? new AnimationFrameFull[0];
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
            var response = await SendJsonAsync(HttpMethod.Post, path, payload, "return=representation").ConfigureAwait(false);
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
            var response = await SendJsonAsync(HttpMethod.Post, "/rest/v1/animation_sequences", payload, "return=representation").ConfigureAwait(false);
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

            var response = await SendJsonAsync(HttpMethod.Post, "/rest/v1/animation_frames", payload).ConfigureAwait(false);
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
            return await GetJsonAsync<FirmInfo[]>(path, "List clients").ConfigureAwait(false);
        }

        /// <summary>
        /// List projects for a specific client.
        /// </summary>
        public async Task<ProjectInfo[]> ListProjectsByClientAsync(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return await ListProjectsAsync();

            var path = $"/rest/v1/projects?{Postgrest.Eq("client_id", clientId)}&select=id,name,created_at";
            return await GetJsonAsync<ProjectInfo[]>(path, "List projects by client").ConfigureAwait(false);
        }

        /// <summary>
        /// List module records (Topography, Geology, etc.) for a specific project.
        /// </summary>
        public async Task<ModuleRecord[]> ListModuleRecordsAsync(string tableName, string projectId)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));

            var path = $"/rest/v1/{tableName}?{Postgrest.Eq("project_id", projectId)}&select=id,project_id,created_at";
            return await GetJsonAsync<ModuleRecord[]>(path, $"List module records ({tableName})").ConfigureAwait(false);
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

            var path = $"/rest/v1/{tableName}?{Postgrest.Eq("id", recordId)}";
            var response = await SendJsonAsync(new HttpMethod("PATCH"), path, values, "return=representation").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new SelvagenApiException($"Update {tableName} failed: {json}", (int)response.StatusCode);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>GET an authorized PostgREST/Edge path and deserialize the JSON array/object.</summary>
        private async Task<T> GetJsonAsync<T>(string path, string label)
        {
            var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"{label} failed: {json}", (int)response.StatusCode);
            return JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// Send an authorized request with an optional JSON body (POST/PATCH/DELETE) and
        /// optional Prefer header, returning the raw response for the caller to interpret.
        /// </summary>
        private async Task<HttpResponseMessage> SendJsonAsync(
            HttpMethod method, string path, object payload = null, string prefer = null)
        {
            await EnsureValidTokenAsync().ConfigureAwait(false);
            var request = new HttpRequestMessage(method, $"{_supabaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("apikey", _anonKey);
            if (prefer != null) request.Headers.Add("Prefer", prefer);
            if (payload != null)
            {
                var body = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
            return await _http.SendAsync(request).ConfigureAwait(false);
        }

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

            Log($"{method} {path}");

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
