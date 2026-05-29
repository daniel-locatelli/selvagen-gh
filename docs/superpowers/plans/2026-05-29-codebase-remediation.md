# Codebase Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **REQUIRED SKILL for every code/build/deploy step:** `creating-grasshopper-plugin`. The "edit cycle" (build → copy to `%APPDATA%\Grasshopper\Libraries\Selvagen\` → restart Rhino → verify on canvas) is the only valid verification for GH component changes. "Build succeeded" is NOT verification.

**Goal:** Fix all security, correctness, hygiene, and maintainability findings from the 2026-05-29 code review, prioritising security and finishing with a full migration off the UI-blocking sync-over-async pattern.

**Architecture:** Four ordered phases. Phase 1 (security + repo hygiene) is low-risk and ships first. Phase 2 fixes correctness bugs. Phase 3 reduces duplication. Phase 4 is the large architectural change — converting every network component to true non-blocking async using the manual-async pattern already proven in `SelvagenSelectableComponentBase`.

**Tech Stack:** .NET 8 / .NET 4.8 multi-target, RhinoCommon 8, Grasshopper 8, System.Text.Json, xUnit (Core tests, `net8.0`, RhinoCommon runtime assets excluded → **new unit-testable logic must not touch `Rhino.Geometry`**), Supabase (PostgREST + Auth), `git filter-repo`.

**Scope decisions (from user, 2026-05-29):**
- debug.log: **purge from full git history** (history rewrite).
- Sync-over-async: **full migration to true async**.
- Supabase anon key: **keep in source, document RLS dependency** (no rotation).

---

## File Structure

**Phase 1 — Security & hygiene**
- Modify: `.gitignore` — add `debug.log`, `*.gh.inflated`, `Properties/launchSettings.json`, worktree exclusion.
- Delete (untrack): `debug.log`, `src/Selvagen.GH/Icons/__pycache__/`, `src/**/Properties/launchSettings.json`, `grasshopper-sample/Selvagen_test.gh.inflated`, `.claude/worktrees/feat+dropdown-selectors`.
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs` — stop logging tokens/apikey/bodies.
- Modify: `src/Selvagen.GH/Components/SelvagenLoginComponent.cs` — stop logging email.
- Create: `src/Selvagen.Core/Api/Postgrest.cs` — pure URL-value escaping helper.
- Test: `tests/Selvagen.Core.Tests/PostgrestTests.cs`.
- Modify: `src/Selvagen.Core/Api/SelvagenConfig.cs` — RLS doc comment.

**Phase 2 — Correctness**
- Create: `src/Selvagen.Core/Converters/BufferGeometryValidator.cs` — pure (Rhino-free) shape validation.
- Test: `tests/Selvagen.Core.Tests/BufferGeometryValidatorTests.cs`.
- Modify: `src/Selvagen.Core/Converters/MeshConverter.cs` — call validator first.
- Modify: `src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs` — lock shared state.
- Modify: `src/Selvagen.Core/Api/SelvagenConfig.cs` — surface load failures.

**Phase 3 — Maintainability**
- Modify: `src/Selvagen.GH/IconLoader.cs` — memoize bitmaps.
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs` — generic GET/SEND helpers; deeper exception unwrap.
- Create: `src/Selvagen.Core/Api/ExceptionExtensions.cs` — `Unwrap()`.

**Phase 4 — Full async migration**
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs` — `HttpClient.Timeout`, `CancellationToken` plumbing.
- Modify: `src/Selvagen.GH/Components/SelvagenActionComponentBase.cs` — shared `StartAsync`/`TryFinishAsync` runner.
- Modify: all upload/download/module/delete components (enumerated in Tasks 15–18).

---

# PHASE 1 — SECURITY & REPO HYGIENE

### Task 1: Stop committing the runtime log

**Files:**
- Modify: `.gitignore`
- Delete (untrack): `debug.log`

- [ ] **Step 1: Add ignore rules**

Append to `.gitignore` under the `## Secrets` section:

```gitignore
## Runtime logs (may contain emails, project names, tokens)
debug.log
*.log

## Build/runtime artifacts that slipped in
*.gh.inflated
**/Properties/launchSettings.json
```

- [ ] **Step 2: Untrack the file (keep it on disk)**

Run:
```powershell
git rm --cached debug.log
```
Expected: `rm 'debug.log'`.

- [ ] **Step 3: Verify it is no longer tracked**

Run:
```powershell
git status --short debug.log
```
Expected: shows `D  debug.log` staged (deletion from index) and the file is ignored going forward. `git check-ignore debug.log` should print `debug.log`.

- [ ] **Step 4: Commit**

```powershell
git add .gitignore
git commit -m @'
chore(gh): stop tracking debug.log and runtime artifacts

Runtime log contains real email, project names, and partial tokens.
Untrack it and ignore logs / launchSettings / .gh.inflated going forward.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 2: Stop logging credentials and response bodies in the API client

**Files:**
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs:148` (ListProjectsAsync body log), `:820-822` (SendAuthorizedAsync header logs)

- [ ] **Step 1: Remove the response-body log in `ListProjectsAsync`**

In `ListProjectsAsync`, delete these two lines (`:147-148`):

```csharp
            // Log the raw response so we can diagnose issues
            Log($"ListProjectsAsync: HTTP {(int)response.StatusCode}, body = {json}");
```

Replace with a status-only line (no body):

```csharp
            Log($"ListProjectsAsync: HTTP {(int)response.StatusCode}");
```

- [ ] **Step 2: Remove token/apikey logging in `SendAuthorizedAsync`**

In `SendAuthorizedAsync`, delete these three lines (`:820-822`):

```csharp
            Log($"SendAuthorizedAsync: {method} {_supabaseUrl}{path}");
            Log($"  Authorization: Bearer {_accessToken?.Substring(0, Math.Min(20, _accessToken?.Length ?? 0))}...");
            Log($"  apikey: {_anonKey?.Substring(0, Math.Min(20, _anonKey?.Length ?? 0))}...");
```

Replace with a single line that logs method + path only (path may contain a project UUID, which is acceptable operational data — but never the token or apikey):

```csharp
            Log($"{method} {path}");
```

- [ ] **Step 3: Audit for any other body/credential logging**

Run:
```powershell
Select-String -Path src\Selvagen.Core\Api\SelvagenClient.cs -Pattern 'Log\(' 
```
Expected: only `Refreshing access token...`, `Token refreshed successfully.`, the two lines edited above. Confirm no remaining `Log(...)` interpolates `_accessToken`, `_anonKey`, `_refreshToken`, `password`, or `json`/`body`.

- [ ] **Step 4: Build Core**

Run:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src\Selvagen.Core\Api\SelvagenClient.cs
git commit -m @'
fix(core): stop logging tokens, apikey, and response bodies

SendAuthorizedAsync logged a token prefix + apikey prefix on every call,
and ListProjectsAsync logged full response bodies (project names/UUIDs).
Reduce to method+path only.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 3: Stop logging the login email

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenLoginComponent.cs:62`

- [ ] **Step 1: Redact the email from the pre-login log**

Replace line `:62`:

```csharp
                PluginLogger.Log($"Logging in to {url} with email {email}");
```

with:

```csharp
                PluginLogger.Log($"Logging in to {url}");
```

Leave the post-login success line (`:68`) — `_statusMessage` is shown in the UI anyway, but change it to avoid persisting the email to the log file. Replace `:67-68`:

```csharp
                _statusMessage = $"Logged in as {result.User?.Email ?? email}";
                PluginLogger.Log($"Login successful: {_statusMessage}");
```

with:

```csharp
                _statusMessage = $"Logged in as {result.User?.Email ?? email}";
                PluginLogger.Log("Login successful.");
```

- [ ] **Step 2: Edit cycle — build, deploy, restart, verify**

Per `creating-grasshopper-plugin`:
```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```
Then copy full build output to `%APPDATA%\Grasshopper\Libraries\Selvagen\`, restart Rhino, place the Login component, log in with the test account (`admin@selvagen.com` / `123456`), and confirm: (a) login succeeds, status reads "Logged in as …"; (b) `%APPDATA%\Selvagen\Logs\selvagen.log` contains "Login successful." with **no email address**.

- [ ] **Step 3: Commit**

```powershell
git add src\Selvagen.GH\Components\SelvagenLoginComponent.cs
git commit -m @'
fix(gh): stop writing login email to the plugin log

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 4: Untrack build/IDE artifacts and the committed worktree

**Files:**
- Delete (untrack): `src/Selvagen.GH/Icons/__pycache__/generate_icons.cpython-312.pyc`, `src/Selvagen.Core/Properties/launchSettings.json`, `src/Selvagen.GH/Properties/launchSettings.json`, `grasshopper-sample/Selvagen_test.gh.inflated`, `.claude/worktrees/feat+dropdown-selectors`
- Modify: `.gitignore`

- [ ] **Step 1: Confirm what is tracked**

Run:
```powershell
git ls-files | Select-String -Pattern '__pycache__|launchSettings|\.inflated|worktrees'
```
Expected: the five paths above (the worktree may appear as a single gitlink entry `.claude/worktrees/feat+dropdown-selectors`).

- [ ] **Step 2: Add ignore rules**

Append to `.gitignore`:

```gitignore
## Claude worktrees (never commit a worktree into its parent repo)
.claude/worktrees/
```
(`__pycache__/` is already present; `launchSettings.json` and `*.gh.inflated` were added in Task 1.)

- [ ] **Step 3: Untrack each path (keep on disk)**

Run:
```powershell
git rm --cached "src/Selvagen.GH/Icons/__pycache__/generate_icons.cpython-312.pyc"
git rm --cached "src/Selvagen.Core/Properties/launchSettings.json"
git rm --cached "src/Selvagen.GH/Properties/launchSettings.json"
git rm --cached "grasshopper-sample/Selvagen_test.gh.inflated"
git rm --cached -r ".claude/worktrees/feat+dropdown-selectors"
```
Expected: each prints an `rm '...'` line. (If the worktree is a gitlink, the `-r` is harmless; if it errors as "not in index", skip it.)

- [ ] **Step 4: Verify clean**

Run:
```powershell
git ls-files | Select-String -Pattern '__pycache__|launchSettings|\.inflated|worktrees'
```
Expected: no output.

- [ ] **Step 5: Commit**

```powershell
git add .gitignore
git commit -m @'
chore: untrack build/IDE artifacts and committed worktree

Removes __pycache__ pyc, launchSettings.json, .gh.inflated, and the
.claude/worktrees copy of the source tree from version control.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 5: Purge `debug.log` from full git history

**Files:** git history rewrite (no source files). **Destructive — coordinate before running.**

> **Pre-flight:** This rewrites every commit SHA. After it, all collaborators must re-clone or hard-reset. Do this only after Tasks 1–4 are committed, and confirm with the repo owner that no one has unpushed work.

- [ ] **Step 1: Install git-filter-repo**

Run:
```powershell
pip install git-filter-repo
git filter-repo --version
```
Expected: prints a version (e.g. `git-filter-repo 2.x`).

- [ ] **Step 2: Back up the repo**

Run:
```powershell
git bundle create ../selvagen-gh-backup.bundle --all
```
Expected: `Writing objects` … completes. Keep this bundle until the rewrite is verified.

- [ ] **Step 3: Rewrite history to drop debug.log**

Run from the repo root:
```powershell
git filter-repo --invert-paths --path debug.log --force
```
Expected: `Parsed N commits` … `Completely finished after …`.

- [ ] **Step 4: Verify debug.log is gone from history**

Run:
```powershell
git log --all --oneline -- debug.log
```
Expected: **no output** (the path no longer exists in any commit).

- [ ] **Step 5: Re-add the remote and force-push**

`git filter-repo` removes `origin` by design. Run (substitute the real URL):
```powershell
git remote add origin <REMOTE_URL>
git push origin --force --all
git push origin --force --tags
```
Expected: force-update succeeds. Notify collaborators to re-clone.

- [ ] **Step 6: Confirm working tree still builds**

Run:
```powershell
dotnet build Selvagen.sln
```
Expected: `Build succeeded`.

---

### Task 6: Escape interpolated values in PostgREST query URLs

**Files:**
- Create: `src/Selvagen.Core/Api/Postgrest.cs`
- Test: `tests/Selvagen.Core.Tests/PostgrestTests.cs`
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs` (query builders that interpolate `id`/`legendId`/`assetId`/`sequenceId`/`clientId`)

- [ ] **Step 1: Write the failing test**

Create `tests/Selvagen.Core.Tests/PostgrestTests.cs`:

```csharp
using Selvagen.Core.Api;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class PostgrestTests
    {
        [Fact]
        public void Eq_EscapesSpecialCharacters()
        {
            // A value containing PostgREST/URL-significant chars must be percent-encoded.
            var clause = Postgrest.Eq("id", "abc,def&select=*");
            Assert.Equal("id=eq.abc%2Cdef%26select%3D%2A", clause);
        }

        [Fact]
        public void Eq_LeavesPlainUuidUntouched()
        {
            var clause = Postgrest.Eq("project_id", "0ae6073d-c80a-4eed-a537-5ad8ee51d028");
            Assert.Equal("project_id=eq.0ae6073d-c80a-4eed-a537-5ad8ee51d028", clause);
        }

        [Fact]
        public void InList_EncodesEachValue()
        {
            var clause = Postgrest.InList("key", new[] { "a_b", "c,d" });
            Assert.Equal("key=in.(a_b,c%2Cd)", clause);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```powershell
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj --filter PostgrestTests
```
Expected: FAIL — `Postgrest` does not exist (compile error).

- [ ] **Step 3: Implement `Postgrest`**

Create `src/Selvagen.Core/Api/Postgrest.cs`:

```csharp
using System;

namespace Selvagen.Core.Api
{
    /// <summary>
    /// Helpers for building PostgREST query clauses with URL-encoded values.
    /// Centralizes escaping so callers can't forget it (see DeleteCustomPropertiesAsync,
    /// which already escaped; this brings every other query builder in line).
    /// </summary>
    public static class Postgrest
    {
        /// <summary>Builds <c>column=eq.{encoded value}</c>.</summary>
        public static string Eq(string column, string value)
            => $"{column}=eq.{Uri.EscapeDataString(value ?? string.Empty)}";

        /// <summary>Builds <c>column=in.(v1,v2,...)</c> with each value encoded.</summary>
        public static string InList(string column, string[] values)
        {
            var encoded = string.Join(",", Array.ConvertAll(values ?? Array.Empty<string>(), Uri.EscapeDataString));
            return $"{column}=in.({encoded})";
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```powershell
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj --filter PostgrestTests
```
Expected: PASS (3 tests).

- [ ] **Step 5: Apply `Postgrest.Eq` to the unescaped query builders in `SelvagenClient`**

Replace each raw `id=eq.{...}` / `{col}=eq.{...}` interpolation. Exact edits:

`GetMeshAsync` (`:492`):
```csharp
            var path = $"/rest/v1/meshes?{Postgrest.Eq("id", id)}&select=id,name,type,geometry_data,geometry_url";
```
`GetCurveSetAsync` (`:513`):
```csharp
            var path = $"/rest/v1/curve_sets?{Postgrest.Eq("id", id)}&select=id,name,geometry_data,geometry_url";
```
`GetLabelSetAsync` (`:531`):
```csharp
            var path = $"/rest/v1/label_sets?{Postgrest.Eq("id", id)}&select=id,name,text_data,geometry_url";
```
`GetAnimationSequenceInfoAsync` (`:552`):
```csharp
            var path = $"/rest/v1/animation_sequences?{Postgrest.Eq("id", id)}&select=id,name,fps,loop,base_asset_id,frame_count";
```
`GetAnimationFramesAsync` (`:573`):
```csharp
            var path = $"/rest/v1/animation_frames?{Postgrest.Eq("sequence_id", sequenceId)}&select=frame_index,geometry_data,label&order=frame_index";
```
`GetColorLegendAsync` (`:282`):
```csharp
            var path = $"/rest/v1/color_legends?{Postgrest.Eq("id", legendId)}&select=id,project_id,name,variant,colors,labels,domain_min,domain_max,unit,created_at,updated_at";
```
`DeleteColorLegendAsync` (`:342`):
```csharp
            var path = $"/rest/v1/color_legends?{Postgrest.Eq("id", legendId)}";
```
`DeleteAssetAsync` (`:458`):
```csharp
            var path = $"/rest/v1/{tableName}?{Postgrest.Eq("id", assetId)}";
```
`UpdateModuleAsync` (`:771`):
```csharp
            var path = $"/rest/v1/{tableName}?{Postgrest.Eq("id", recordId)}";
```
`ListProjectsByClientAsync` (`:725`):
```csharp
            var path = $"/rest/v1/projects?{Postgrest.Eq("client_id", clientId)}&select=id,name,created_at";
```
The `project_id=eq.{projectId}` builders in the `ListMeshesAsync`/`ListCurveSetsAsync`/`ListLabelSetsAsync`/`ListAnimationSequencesAsync`/`ListColorLegendsAsync`/`ListCustomPropertiesAsync`/`ListModuleRecordsAsync` methods: apply `Postgrest.Eq("project_id", projectId)` the same way.

`DeleteCustomPropertiesAsync` (`:422-423`) — replace the hand-rolled escaping with the helper:
```csharp
            var path = $"/rest/v1/custom_properties?{Postgrest.Eq("project_id", projectId)}&{Postgrest.InList("key", keys)}";
```
(Delete the now-unused `encoded` local and its comment block at `:419-422`.)

- [ ] **Step 6: Add `using Selvagen.Core.Api;`** — not needed (same namespace). Confirm `Postgrest` resolves.

- [ ] **Step 7: Build + full test run**

Run:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj
```
Expected: build succeeds; all tests pass (existing + 3 new).

- [ ] **Step 8: Commit**

```powershell
git add src\Selvagen.Core\Api\Postgrest.cs tests\Selvagen.Core.Tests\PostgrestTests.cs src\Selvagen.Core\Api\SelvagenClient.cs
git commit -m @'
fix(core): URL-encode all interpolated PostgREST query values

Adds Postgrest.Eq/InList and routes every id/project_id/client_id query
through it, closing the unescaped-interpolation gap (only delete-custom-
properties escaped before).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 7: Document the anon-key / RLS security model

**Files:**
- Modify: `src/Selvagen.Core/Api/SelvagenConfig.cs:14-15`

- [ ] **Step 1: Add an explanatory comment above the baked-in defaults**

Insert before line `:14`:

```csharp
        // SECURITY MODEL: the Supabase "anon" key below is a PUBLISHABLE key and is
        // intentionally shipped in the binary. It grants no privileges on its own —
        // every table is protected by Postgres Row Level Security (RLS). The plugin's
        // data security therefore depends ENTIRELY on server-side RLS policies being
        // correct. Do NOT add a service-role key here. If RLS guarantees are ever in
        // doubt, rotate this key and tighten policies rather than relying on secrecy.
        private const string DefaultSupabaseUrl = "https://aqzfsrebvjkegvfexcut.supabase.co";
```

- [ ] **Step 2: Build**

Run:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```powershell
git add src\Selvagen.Core\Api\SelvagenConfig.cs
git commit -m @'
docs(core): document anon-key/RLS security model in SelvagenConfig

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

# PHASE 2 — CORRECTNESS BUGS

### Task 8: Guard `FromBufferGeometry` against malformed server payloads

**Files:**
- Create: `src/Selvagen.Core/Converters/BufferGeometryValidator.cs`
- Test: `tests/Selvagen.Core.Tests/BufferGeometryValidatorTests.cs`
- Modify: `src/Selvagen.Core/Converters/MeshConverter.cs:117-124`

> The validator is **Rhino-free** (operates on the `BufferGeometry` model only) so it runs under the headless `net8.0` test runner. `MeshConverter.FromBufferGeometry` calls it *before* `new Mesh()`, so a bad payload fails with a clear `ArgumentException` instead of an `NullReferenceException`.

- [ ] **Step 1: Write the failing test**

Create `tests/Selvagen.Core.Tests/BufferGeometryValidatorTests.cs`:

```csharp
using System;
using Selvagen.Core.Converters;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class BufferGeometryValidatorTests
    {
        [Fact]
        public void Throws_When_Bg_Null()
            => Assert.Throws<ArgumentNullException>(() => BufferGeometryValidator.ValidateForDecode(null));

        [Fact]
        public void Throws_When_Data_Missing()
        {
            var bg = new BufferGeometry { Data = null };
            var ex = Assert.Throws<ArgumentException>(() => BufferGeometryValidator.ValidateForDecode(bg));
            Assert.Contains("data", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Throws_When_Position_Array_Missing()
        {
            var bg = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes { Position = new BufferAttribute { Array = null } }
                }
            };
            var ex = Assert.Throws<ArgumentException>(() => BufferGeometryValidator.ValidateForDecode(bg));
            Assert.Contains("position", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Throws_When_Position_Not_Multiple_Of_3()
        {
            var bg = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes { Position = new BufferAttribute { Array = new double[] { 1, 2 } } }
                }
            };
            Assert.Throws<ArgumentException>(() => BufferGeometryValidator.ValidateForDecode(bg));
        }

        [Fact]
        public void Passes_For_Valid_Geometry()
        {
            var bg = new BufferGeometry
            {
                Data = new BufferGeometryData
                {
                    Attributes = new BufferGeometryAttributes { Position = new BufferAttribute { Array = new double[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 } } }
                }
            };
            BufferGeometryValidator.ValidateForDecode(bg); // does not throw
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```powershell
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj --filter BufferGeometryValidatorTests
```
Expected: FAIL — `BufferGeometryValidator` does not exist.

- [ ] **Step 3: Implement the validator**

Create `src/Selvagen.Core/Converters/BufferGeometryValidator.cs`:

```csharp
using System;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    /// <summary>
    /// Validates the shape of a <see cref="BufferGeometry"/> received from the server
    /// before it is decoded into a Rhino mesh. Rhino-free so it is unit-testable
    /// headless. Throws a descriptive <see cref="ArgumentException"/> instead of
    /// letting a malformed payload surface as a NullReferenceException downstream.
    /// </summary>
    public static class BufferGeometryValidator
    {
        public static void ValidateForDecode(BufferGeometry bg)
        {
            if (bg == null) throw new ArgumentNullException(nameof(bg));
            if (bg.Data == null)
                throw new ArgumentException("BufferGeometry.data is missing.", nameof(bg));
            if (bg.Data.Attributes == null)
                throw new ArgumentException("BufferGeometry.data.attributes is missing.", nameof(bg));
            var pos = bg.Data.Attributes.Position;
            if (pos?.Array == null)
                throw new ArgumentException("BufferGeometry position attribute is missing.", nameof(bg));
            if (pos.Array.Length % 3 != 0)
                throw new ArgumentException(
                    $"BufferGeometry position array length ({pos.Array.Length}) is not a multiple of 3.", nameof(bg));
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```powershell
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj --filter BufferGeometryValidatorTests
```
Expected: PASS (5 tests).

- [ ] **Step 5: Call the validator from `FromBufferGeometry`**

In `MeshConverter.cs`, replace the opening of `FromBufferGeometry` (`:117-124`):

```csharp
        public static Mesh FromBufferGeometry(BufferGeometry bg)
        {
            if (bg == null)
                throw new ArgumentNullException(nameof(bg));

            var mesh = new Mesh();
            var posArr = bg.Data.Attributes.Position.Array;
            int vertCount = posArr.Length / 3;
```

with:

```csharp
        public static Mesh FromBufferGeometry(BufferGeometry bg)
        {
            BufferGeometryValidator.ValidateForDecode(bg);

            var mesh = new Mesh();
            var posArr = bg.Data.Attributes.Position.Array;
            int vertCount = posArr.Length / 3;
```

- [ ] **Step 6: Build Core + run all Core tests**

Run:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj
```
Expected: build + all tests pass.

- [ ] **Step 7: Edit-cycle verification (download path)**

Build/deploy `Selvagen.GH`, restart Rhino, and run a Download Mesh component against a known-good asset → mesh appears (no regression). The malformed-payload path can't be reproduced live without a bad server row; the unit tests cover it.

- [ ] **Step 8: Commit**

```powershell
git add src\Selvagen.Core\Converters\BufferGeometryValidator.cs tests\Selvagen.Core.Tests\BufferGeometryValidatorTests.cs src\Selvagen.Core\Converters\MeshConverter.cs
git commit -m @'
fix(core): validate BufferGeometry shape before mesh decode

Malformed/partial download payloads threw NullReferenceException at
bg.Data.Attributes.Position.Array. Add a Rhino-free validator that throws
a descriptive ArgumentException first.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 9: Fix the cross-thread data race in `SelvagenSelectableComponentBase`

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs`

> Shared reference fields (`_cachedItems`, `_cachedKey`, `_selectedId`) are written on the `Task.Run` worker thread (`:101-133`) and read on the solver/UI thread (`EmitOutputs`, `CurrentDisplayText`). Guard all access with a single lock. Keep the network `await` *outside* the lock — only the field assignments and reads are synchronized.

- [ ] **Step 1: Add a lock object**

After the state field declarations (`:33`), add:

```csharp
        private readonly object _stateLock = new object();
```

- [ ] **Step 2: Synchronize the worker-thread writes**

In the `Task.Run` body (`:105-117`), wrap the field mutations:

```csharp
                        var items = await FetchAsync(client, inputs).ConfigureAwait(false);
                        if (items != null)
                        {
                            string reconciled;
                            lock (_stateLock)
                            {
                                _cachedItems = items;
                                _cachedKey = capturedKey;
                            }
                            reconciled = Reconcile.SelectId(items, _selectedId, GetId);
                            lock (_stateLock)
                            {
                                _selectedId = reconciled;
                            }
                        }
                        else
                        {
                            _lastFetchError ??= "Fetch returned no data.";
                        }
```

- [ ] **Step 3: Synchronize the reads in `EmitOutputs`**

Replace the body of `EmitOutputs` (`:144-165`) with a snapshot taken under lock, then emit outside it:

```csharp
        private void EmitOutputs(IGH_DataAccess DA)
        {
            TItem[] items;
            string selId;
            lock (_stateLock)
            {
                items = _cachedItems;
                selId = _selectedId;
            }

            string selectedId = selId ?? "";
            string selectedName = "";
            if (selId != null && items != null)
            {
                var match = items.FirstOrDefault(i => GetId(i) == selId);
                if (match != null) selectedName = GetDisplayName(match);
            }

            var ids = items == null ? new List<string>() : items.Select(GetId).ToList();
            var names = items == null ? new List<string>() : items.Select(GetDisplayName).ToList();

            DA.SetData(0, selectedId);
            DA.SetData(1, selectedName);
            DA.SetDataList(2, ids);
            DA.SetDataList(3, names);
        }
```

- [ ] **Step 4: Synchronize `CurrentDisplayText` and `GetMenuItems`**

In `CurrentDisplayText` (`:169-183`) snapshot under lock before the branch logic:

```csharp
        public string CurrentDisplayText
        {
            get
            {
                if (SessionManager.Current == null) return "Not logged in";
                TItem[] items; string selId;
                lock (_stateLock) { items = _cachedItems; selId = _selectedId; }
                if (items == null) return "Loading…";
                if (selId != null)
                {
                    var match = items.FirstOrDefault(i => GetId(i) == selId);
                    return match != null ? GetDisplayName(match) : "<missing item>";
                }
                return "— Select —";
            }
        }
```

In `GetMenuItems` (`:187-192`) snapshot first:

```csharp
        public IEnumerable<(string Id, string Name)> GetMenuItems()
        {
            TItem[] items;
            lock (_stateLock) { items = _cachedItems; }
            if (items == null) yield break;
            foreach (var item in items)
                yield return (GetId(item), GetDisplayName(item));
        }
```

- [ ] **Step 5: Build + edit-cycle verification**

Build/deploy `Selvagen.GH`, restart Rhino. Verify the Clients → Projects cascade: pick a client, confirm the projects dropdown repopulates without showing a stale list, and rapidly change the client selector a few times — the final displayed list must match the final selection (no torn/stale state).

- [ ] **Step 6: Commit**

```powershell
git add src\Selvagen.GH\Components\SelvagenSelectableComponentBase.cs
git commit -m @'
fix(gh): lock shared selector state across worker/solver threads

_cachedItems/_cachedKey/_selectedId were written on the fetch worker
thread and read on the solver thread without synchronization. Guard all
access with a lock; keep the network await outside the lock.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 10: Surface config-load failures instead of silently using prod defaults

**Files:**
- Modify: `src/Selvagen.Core/Api/SelvagenConfig.cs:38-65`

- [ ] **Step 1: Log on parse/IO failure**

Replace the `catch` block (`:55-58`):

```csharp
            catch
            {
                // Fall through to defaults
            }
```

with:

```csharp
            catch (Exception ex)
            {
                // A malformed config file would otherwise silently route the user to the
                // baked-in PRODUCTION environment. Surface it so "why am I hitting prod?"
                // is debuggable.
                System.Diagnostics.Debug.WriteLine($"[Selvagen] Failed to load {ConfigFilePath}: {ex.Message}. Using compiled defaults.");
            }
```

- [ ] **Step 2: Build + test**

Run:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj
```
Expected: build + tests pass (existing tests read defaults; unaffected).

- [ ] **Step 3: Commit**

```powershell
git add src\Selvagen.Core\Api\SelvagenConfig.cs
git commit -m @'
fix(core): log config-load failures instead of silently using defaults

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

# PHASE 3 — MAINTAINABILITY

### Task 11: Memoize embedded icons

**Files:**
- Modify: `src/Selvagen.GH/IconLoader.cs`

> The 95 `Icon => IconLoader.Load("X")` getters re-open the embedded stream and allocate a new `Bitmap` every time Grasshopper queries them (frequent during redraws). Cache by name.

- [ ] **Step 1: Add a thread-safe cache**

Replace the whole `IconLoader` class body:

```csharp
using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;

namespace Selvagen.GH
{
    internal static class IconLoader
    {
        private static readonly ConcurrentDictionary<string, Bitmap> _cache =
            new ConcurrentDictionary<string, Bitmap>();

        /// <summary>
        /// Load a 24x24 icon from embedded resources, cached by name.
        /// Resource names follow: Selvagen.GH.Icons.{name}.png
        /// </summary>
        internal static Bitmap Load(string name)
        {
            return _cache.GetOrAdd(name, static key =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"Selvagen.GH.Icons.{key}.png";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    return stream != null ? new Bitmap(stream) : null;
                }
            });
        }
    }
}
```

> Note: `ConcurrentDictionary.GetOrAdd` may store a `null` if a resource is missing; that's the prior behavior (return null) and is fine — it just caches the miss.

- [ ] **Step 2: Build + edit-cycle verification**

Build/deploy `Selvagen.GH`, restart Rhino. Confirm every Selvagen component still shows its icon on the palette and on-canvas (a regression here would show blank icons).

- [ ] **Step 3: Commit**

```powershell
git add src\Selvagen.GH\IconLoader.cs
git commit -m @'
perf(gh): memoize embedded icon bitmaps

Icon getters re-opened the resource stream and allocated a new Bitmap on
every canvas redraw. Cache by name.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 12: Add an `Unwrap()` extension and use it for error messages

**Files:**
- Create: `src/Selvagen.Core/Api/ExceptionExtensions.cs`
- Test: `tests/Selvagen.Core.Tests/ExceptionExtensionsTests.cs`
- Modify: call sites using `ex.InnerException?.Message ?? ex.Message`

- [ ] **Step 1: Write the failing test**

Create `tests/Selvagen.Core.Tests/ExceptionExtensionsTests.cs`:

```csharp
using System;
using Selvagen.Core.Api;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class ExceptionExtensionsTests
    {
        [Fact]
        public void Unwrap_ReturnsDeepestMessage()
        {
            var deep = new InvalidOperationException("root cause");
            var mid = new Exception("mid", deep);
            var top = new AggregateException("agg", mid);
            Assert.Equal("root cause", top.Unwrap().Message);
        }

        [Fact]
        public void Unwrap_PlainException_ReturnsItself()
        {
            var ex = new Exception("solo");
            Assert.Equal("solo", ex.Unwrap().Message);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run:
```powershell
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj --filter ExceptionExtensionsTests
```
Expected: FAIL — `Unwrap` not defined.

- [ ] **Step 3: Implement**

Create `src/Selvagen.Core/Api/ExceptionExtensions.cs`:

```csharp
using System;

namespace Selvagen.Core.Api
{
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Returns the innermost exception, unwrapping AggregateException and
        /// nested InnerExceptions. Use for user-facing error messages, since
        /// Task.GetResult() wraps the real cause one or more levels deep.
        /// </summary>
        public static Exception Unwrap(this Exception ex)
        {
            if (ex == null) return null;
            if (ex is AggregateException agg) return agg.Flatten().InnerException?.Unwrap() ?? agg;
            return ex.InnerException != null ? ex.InnerException.Unwrap() : ex;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run:
```powershell
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj --filter ExceptionExtensionsTests
```
Expected: PASS (2 tests).

- [ ] **Step 5: Use `Unwrap()` at the error-message call sites**

In `SelvagenActionComponentBase.SetActionError` (`:59-64`), add `using Selvagen.Core.Api;` and change:

```csharp
            var msg = ex.InnerException?.Message ?? ex.Message;
```
to:
```csharp
            var msg = ex.Unwrap().Message;
```

Apply the same one-line change at each remaining `ex.InnerException?.Message ?? ex.Message` site (run the search below to enumerate; add the `using` where needed):

```powershell
Select-String -Path src\Selvagen.GH\**\*.cs, src\Selvagen.Core\**\*.cs -Pattern 'InnerException\?\.Message \?\? '
```
Expected sites: `SelvagenLoginComponent.cs:72`, `SelvagenModuleComponentBase.cs:102`, `SelvagenSelectableComponentBase.cs:121`, `SelvagenActionComponentBase.cs:61`. Replace each RHS expression with `ex.Unwrap().Message` (matching the local exception variable name).

- [ ] **Step 6: Build all + test**

Run:
```powershell
dotnet build Selvagen.sln
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj
```
Expected: build + tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src\Selvagen.Core\Api\ExceptionExtensions.cs tests\Selvagen.Core.Tests\ExceptionExtensionsTests.cs src\Selvagen.GH\Components\*.cs src\Selvagen.GH\Components\SelvagenModuleComponentBase.cs
git commit -m @'
refactor: unwrap nested exceptions for user-facing error messages

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 13: DRY the repeated request/deserialize boilerplate in `SelvagenClient`

**Files:**
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs`

> ~25 methods repeat: build request → add `Authorization`/`apikey`/`Prefer` → `SendAsync` → read string → `if (!IsSuccessStatusCode) throw` → `Deserialize<T>`. Extract two private helpers. This is a structural refactor with no behavior change — guarded by the existing `RestTests` (auth-guard) plus the smoke check below. **Do this carefully, one method group at a time, building between each.**

- [ ] **Step 1: Add helpers near the existing `Helpers` region (`:790`)**

```csharp
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
        /// Send an authorized request with a JSON body (POST/PATCH/DELETE) and optional
        /// extra Prefer header, returning the raw response for the caller to interpret.
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
```

- [ ] **Step 2: Migrate the GET-list methods to `GetJsonAsync<T>`**

Convert `ListColorLegendsAsync`, `GetColorLegendAsync`, `ListCustomPropertiesAsync`, `GetMeshAsync`, `GetCurveSetAsync`, `GetLabelSetAsync`, `GetAnimationSequenceInfoAsync`, `GetAnimationFramesAsync`, `ListClientsAsync`, `ListProjectsByClientAsync`, `ListModuleRecordsAsync`. Example — `ListClientsAsync` becomes:

```csharp
        public async Task<FirmInfo[]> ListClientsAsync()
        {
            var path = "/rest/v1/firms?type=eq.client&select=id,legal_name,type";
            return await GetJsonAsync<FirmInfo[]>(path, "List clients").ConfigureAwait(false);
        }
```
For the "must return at least one" methods (`GetColorLegendAsync`, `GetMeshAsync`, etc.) keep the post-call null/empty check, just source the array from `GetJsonAsync<T[]>`:

```csharp
        public async Task<MeshAssetFull> GetMeshAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            var path = $"/rest/v1/meshes?{Postgrest.Eq("id", id)}&select=id,name,type,geometry_data,geometry_url";
            var results = await GetJsonAsync<MeshAssetFull[]>(path, "Get mesh").ConfigureAwait(false);
            if (results == null || results.Length == 0)
                throw new SelvagenApiException($"Mesh not found: {id}", 404);
            return results[0];
        }
```
Build after this step:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 3: Migrate the write methods to `SendJsonAsync`**

Convert `UpsertColorLegendAsync`, `DeleteColorLegendAsync`, `UpsertCustomPropertiesAsync`, `DeleteCustomPropertiesAsync`, `DeleteAssetAsync`, `CreateModuleRecordAsync`, `CreateAnimationSequenceAsync`, `UploadAnimationFrameAsync`, `UpdateModuleAsync`. Example — `DeleteAssetAsync`:

```csharp
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
```
Preserve the special `Content-Range` count parsing in `DeleteCustomPropertiesAsync` (only the request construction is replaced by `SendJsonAsync(..., prefer: "count=exact,return=minimal")`).

Build after this step:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 4: Full test run**

Run:
```powershell
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj
```
Expected: all pass (auth-guard tests still green).

- [ ] **Step 5: Live smoke test (edit cycle)**

Build/deploy `Selvagen.GH`, restart Rhino, log in, then exercise one of each verb against the test project: list (Clients/Projects), download (Mesh), upload (Mesh), a module PATCH (e.g. Topography), and a delete. Each must behave exactly as before. This is the real regression gate for the refactor.

- [ ] **Step 6: Commit**

```powershell
git add src\Selvagen.Core\Api\SelvagenClient.cs
git commit -m @'
refactor(core): extract GetJsonAsync/SendJsonAsync helpers in SelvagenClient

Collapses ~25 copies of the request+deserialize+error boilerplate. No
behavior change; auth-guard tests + live smoke test green.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

# PHASE 4 — FULL ASYNC MIGRATION

> **Pattern:** Mirror the proven manual-async approach in `SelvagenSelectableComponentBase`: capture inputs (and any Rhino geometry conversion) synchronously on the solver thread, run only the network call on a worker thread via `StartAsync`, store the result, `ExpireSolution` on the UI thread, and emit the stored result on the next solve. **Rhino geometry (`Mesh`, `Curve`, etc.) must be touched only on the solver thread — never inside the worker lambda.**

### Task 14: Add HttpClient timeout + cancellation to `SelvagenClient`

**Files:**
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs:48-54`

- [ ] **Step 1: Set a request timeout**

In the constructor (`:52`), after `_http = new HttpClient();`, add:

```csharp
            _http.Timeout = TimeSpan.FromSeconds(100); // explicit; a hung server now fails instead of hanging
```
(100s is the existing default made explicit; with true async the UI no longer freezes, and the connection is released on timeout.)

- [ ] **Step 2: Build + test**

Run:
```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj
```
Expected: build + tests pass.

- [ ] **Step 3: Commit**

```powershell
git add src\Selvagen.Core\Api\SelvagenClient.cs
git commit -m @'
fix(core): set explicit HttpClient timeout

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 15: Add the shared async runner to `SelvagenActionComponentBase`

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenActionComponentBase.cs`

- [ ] **Step 1: Add async state + runner**

Add fields after `_actionRequested` (`:14`):

```csharp
        private volatile bool _isRunningAsync;
        private Exception _asyncError;
        private bool _resultPending;
        private object _asyncResultBox;
        private readonly object _asyncLock = new object();

        /// <summary>True while a click-triggered network action is in flight.</summary>
        public bool IsRunningAsync => _isRunningAsync;
```

Add the runner methods (anywhere in the class body):

```csharp
        /// <summary>
        /// Run a click-triggered network action off the solver thread, then re-solve
        /// to emit its result. Capture all inputs (and any Rhino geometry conversion)
        /// BEFORE calling this — the worker lambda must not touch the solver thread or
        /// Rhino geometry.
        /// </summary>
        protected void StartAsync<TResult>(Func<Task<TResult>> work)
        {
            if (_isRunningAsync) return;
            _isRunningAsync = true;
            IsRunning = true;
            lock (_asyncLock) { _asyncError = null; }
            ForceCanvasRefresh();

            Task.Run(async () =>
            {
                try
                {
                    var r = await work().ConfigureAwait(false);
                    lock (_asyncLock) { _asyncResultBox = r; _resultPending = true; }
                }
                catch (Exception ex)
                {
                    lock (_asyncLock) { _asyncError = ex; }
                }
                finally
                {
                    _isRunningAsync = false;
                    IsRunning = false;
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        if (OnPingDocument() != null) ExpireSolution(true);
                    }));
                }
            });
        }

        /// <summary>
        /// Call at the TOP of SolveInstance. If a finished async result or error is
        /// waiting, emits it via the callback / runtime message and returns true
        /// (the caller should then return immediately).
        /// </summary>
        protected bool TryFinishAsync<TResult>(IGH_DataAccess DA, int statusIndex, Action<IGH_DataAccess, TResult> emitSuccess)
        {
            Exception err; bool pending; object box;
            lock (_asyncLock)
            {
                err = _asyncError; pending = _resultPending; box = _asyncResultBox;
                _asyncError = null;
                if (pending) _resultPending = false;
            }
            if (err != null) { SetActionError(DA, statusIndex, err); return true; }
            if (pending) { emitSuccess(DA, (TResult)box); return true; }
            return false;
        }
```

Add `using System.Threading.Tasks;` at the top if not present.

- [ ] **Step 2: Build**

Run:
```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```
Expected: `Build succeeded` (no component uses it yet).

- [ ] **Step 3: Commit**

```powershell
git add src\Selvagen.GH\Components\SelvagenActionComponentBase.cs
git commit -m @'
feat(gh): add StartAsync/TryFinishAsync runner to action base

Shared non-blocking machinery for migrating action components off
sync-over-async. Not yet wired to any component.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 16: Migrate `SelvagenUploadMeshComponent` (worked reference)

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenUploadMeshComponent.cs:31-76`

- [ ] **Step 1: Replace `SolveInstance` with the async pattern**

```csharp
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Emit a finished async result, if one is waiting.
            if (TryFinishAsync<Selvagen.Core.Models.UploadResult>(DA, 1, (da, result) =>
                {
                    da.SetData(0, result.Id);
                    da.SetData(1, $"Uploaded: {result.Name}");
                }))
                return;

            string projectId = "", name = "";
            Mesh mesh = null;
            DA.GetData(0, ref projectId);
            DA.GetData(1, ref mesh);
            DA.GetData(2, ref name);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (IsRunningAsync) { DA.SetData(1, "Uploading..."); return; }
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || mesh == null || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Mesh, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            // Convert Rhino geometry on the solver thread; only the HTTP call goes async.
            var geometry = MeshConverter.ToBufferGeometry(mesh);
            StartAsync(() => client.UploadMeshAsync(projectId, name, geometry));
            DA.SetData(1, "Uploading...");
        }
```

- [ ] **Step 2: Edit-cycle verification**

Build/deploy, restart Rhino. Upload a large mesh and confirm: (a) the canvas does **not** freeze (you can pan/zoom while "Uploading..." shows); (b) on completion the Mesh ID + "Uploaded: …" appear; (c) an error (e.g. bad Project ID) surfaces as a red runtime error, not a freeze.

- [ ] **Step 3: Commit**

```powershell
git add src\Selvagen.GH\Components\SelvagenUploadMeshComponent.cs
git commit -m @'
refactor(gh): migrate Upload Mesh to non-blocking async

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 17: Migrate the remaining upload components

**Files (each, apply the Task 16 pattern):**
- `SelvagenUploadCurvesComponent.cs:77` — convert `CurveConverter` output on solver thread, `StartAsync(() => client.UploadCurvesAsync(projectId, name, curveSet))`, result type `UploadResult`, emit `Id` + `Uploaded: {Name}`.
- `SelvagenUploadLabelsComponent.cs:81` — `StartAsync(() => client.UploadLabelSetAsync(projectId, name, labelSet))`, `UploadResult`.
- `SelvagenUploadColorLegendComponent.cs:105` — `StartAsync(() => client.UpsertColorLegendAsync(projectId, name, payload))`, result type `ColorLegendInfo`, emit its id/status.
- `SelvagenUploadAnimationComponent.cs:87-105` — multi-call: capture all frame geometry conversions on the solver thread into a list first, then `StartAsync(async () => { var seq = await client.CreateAnimationSequenceAsync(...); foreach frame await client.UploadAnimationFrameAsync(...); return seq; })`.
- `SelvagenUploadCustomPropertyComponent.cs:173` — `StartAsync(() => client.UpsertCustomPropertiesAsync(projectId, rows))`, result `CustomPropertyInfo[]`.

- [ ] **Step 1:** For each file, apply the worked pattern from Task 16: add `TryFinishAsync<...>` at the top with the file's existing output mapping; capture inputs + geometry synchronously; replace the `try { ... Task.Run(...).GetAwaiter().GetResult(); ... } catch { SetUploadError } finally { IsUploading=false }` block with `StartAsync(...)` + `DA.SetData(statusIndex, "Uploading...")`; add the `if (IsRunningAsync) { ...; return; }` short-circuit in the no-action branch.

- [ ] **Step 2: Build**

Run:
```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 3: Edit-cycle verification**

Deploy, restart Rhino, and run each upload component once against the test project — confirm non-blocking behavior + correct outputs (same checks as Task 16 Step 2).

- [ ] **Step 4: Commit**

```powershell
git add src\Selvagen.GH\Components\SelvagenUpload*.cs src\Selvagen.GH\Components\Shared\SelvagenUploadCustomPropertyComponent.cs
git commit -m @'
refactor(gh): migrate remaining upload components to non-blocking async

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 18: Migrate the download components

**Files (each, apply the Task 16 pattern; build the Rhino geometry from the result INSIDE the TryFinishAsync emit callback, which runs on the solver thread):**
- `SelvagenDownloadMeshComponent.cs:64` — `StartAsync(() => client.GetMeshAsync(assetId))`, result `MeshAssetFull`; in the emit callback call `MeshConverter.FromBufferGeometry(result.GeometryData)` and `DA.SetData` the mesh.
- `SelvagenDownloadCurvesComponent.cs:74` — `client.GetCurveSetAsync(assetId)`; build curves in the emit callback.
- `SelvagenDownloadLabelsComponent.cs:80` — `client.GetLabelSetAsync(assetId)`; build labels in the emit callback.
- `SelvagenDownloadColorLegendComponent.cs:73` — `client.GetColorLegendAsync(legendId)`; emit legend fields.
- `SelvagenDownloadAnimationComponent.cs:77-87` — multi-call: `StartAsync(async () => { var info = await client.GetAnimationSequenceInfoAsync(id); var frames = await client.GetAnimationFramesAsync(id); return (info, frames); })`; build geometry from frames in the emit callback.

> **Why geometry-build moves to the emit callback here (vs. before `StartAsync` for uploads):** downloads receive geometry *from* the network, so the `Rhino.Geometry` construction must happen after the result returns — and the emit callback runs on the solver thread, which is the correct place to touch Rhino types.

- [ ] **Step 1:** Apply the pattern to each download file. The trigger for downloads is the in-canvas action button (same `ActionRequested`/`IsRunningAsync` flow as uploads).

- [ ] **Step 2: Build**

Run:
```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 3: Edit-cycle verification**

Deploy, restart Rhino, download one known asset per component — geometry appears, canvas stays responsive during the fetch.

- [ ] **Step 4: Commit**

```powershell
git add src\Selvagen.GH\Components\SelvagenDownload*.cs
git commit -m @'
refactor(gh): migrate download components to non-blocking async

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 19: Migrate the module, delete, and custom-property action components

**Files:**
- `SelvagenModuleComponentBase.cs:63-110` — capture `projectId` + `CollectValues(DA)` synchronously, then `StartAsync(async () => { var existing = await client.ListModuleRecordsAsync(ModuleTable, projectId); string recId; bool created=false; if (existing?.Length > 0) recId = existing[0].Id; else { var rec = await client.CreateModuleRecordAsync(ModuleTable, projectId); recId = rec.Id; created = true; } if (values.Count > 0) await client.UpdateModuleAsync(ModuleTable, recId, values); return (recId, created, values.Count); })`. Emit `RecordID` + `"{Created|Updated}: {ModuleTable} ({n} fields)"` in `TryFinishAsync`. (All ~17 Topo/Geo/Anl/Opt leaf components inherit this base unchanged — verify a representative one of each family.)
- `SelvagenDeleteAssetComponent.cs:75` — `StartAsync(() => client.DeleteAssetAsync(tableNorm, assetId))` (returns `Task`; use `StartAsync<bool>` returning `true` via a wrapper lambda `async () => { await client.DeleteAssetAsync(...); return true; }`).
- `SelvagenDeleteCustomPropertyComponent.cs:99` — wrap `client.DeleteCustomPropertiesAsync(...)` (returns `int`) in `StartAsync<int>`; emit the deleted count.

- [ ] **Step 1:** Apply the pattern. For `Task`-returning calls (no result), use the `async () => { await ...; return true; }` wrapper so `StartAsync<bool>`/`TryFinishAsync<bool>` can carry a completion signal.

- [ ] **Step 2: Build all**

Run:
```powershell
dotnet build Selvagen.sln
```
Expected: `Build succeeded`.

- [ ] **Step 3: Edit-cycle verification**

Deploy, restart Rhino. Verify: a module component (e.g. Topography) PATCH completes without freezing and reports "Updated: topography (N fields)"; a Delete Asset removes a test asset; a Delete Custom Property reports the count.

- [ ] **Step 4: Commit**

```powershell
git add src\Selvagen.GH\Components\SelvagenModuleComponentBase.cs src\Selvagen.GH\Components\SelvagenDeleteAssetComponent.cs src\Selvagen.GH\Components\Shared\SelvagenDeleteCustomPropertyComponent.cs
git commit -m @'
refactor(gh): migrate module/delete/custom-property actions to async

Module base now runs its list→create→patch sequence off-thread in one
StartAsync. Completes the sync-over-async removal.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 20: Final verification — confirm zero blocking calls remain

- [ ] **Step 1: Search for leftover sync-over-async**

Run:
```powershell
Select-String -Path src\**\*.cs -Pattern 'GetAwaiter\(\)\.GetResult\(\)|\.Result\b|\.Wait\(\)'
```
Expected: **no matches** under `src/` (docs/ and worktree copies may still match — ignore those).

- [ ] **Step 2: Full solution build + tests**

Run:
```powershell
dotnet build Selvagen.sln
dotnet test tests\Selvagen.Core.Tests\Selvagen.Core.Tests.csproj
```
Expected: build succeeds; all tests pass.

- [ ] **Step 3: Full deploy + smoke test**

Deploy, restart Rhino, and run the integration scenarios (`tests/integration/run.ps1`) or a manual end-to-end: login → list clients/projects → upload mesh → download mesh → module PATCH → delete. Canvas stays responsive throughout.

- [ ] **Step 4: Update `PRODUCT_ANALYSIS.md`**

Mark item 6 ("Async non-blocking uploads") as done; remove the `Task.Run().GetAwaiter().GetResult()` debt note at `PRODUCT_ANALYSIS.md:207` / `:257`.

- [ ] **Step 5: Commit**

```powershell
git add PRODUCT_ANALYSIS.md
git commit -m @'
docs: mark async-migration debt resolved

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
'@
```

---

## Self-Review Notes

- **Spec coverage:** Findings #1 (logs/PII) → Tasks 1–5; #2 (query injection) → Task 6; anon-key → Task 7; #5 (null-deref) → Task 8; #4 (thread race) → Task 9; #9 (silent config) → Task 10; #7 (icons) → Task 11; #11 (exception unwrap) → Task 12; #8 (client DRY) → Task 13; #3 (timeout) → Task 14; #6 (sync-over-async) → Tasks 15–20. All 12 findings mapped.
- **Test reality:** unit tests target `net8.0` with RhinoCommon runtime excluded, so all new TDD logic (Postgrest, BufferGeometryValidator, ExceptionExtensions) is Rhino-free; GH component changes are verified via the mandatory edit cycle.
- **Ordering:** security/hygiene (1–7) before correctness (8–10) before maintainability (11–13) before the large async migration (14–20), per the "security first" requirement. Task 5 (history rewrite) is gated behind a backup and owner confirmation.
