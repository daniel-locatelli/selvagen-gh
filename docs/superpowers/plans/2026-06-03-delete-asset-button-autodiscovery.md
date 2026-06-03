# Delete Asset — Button + Table Auto-Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Delete Asset component's table+boolean inputs with a single Asset ID input and an embedded Delete button, backed by a Postgres RPC that finds the asset's table and deletes it under the caller's RLS.

**Architecture:** A `SECURITY INVOKER` RPC (`delete_asset_by_id`) does a PK existence scan across the five asset tables, guards against a multi-table id, then deletes from the one match — returning a `{status, table}` jsonb. A new `SelvagenClient.DeleteAssetByIdAsync` calls it and returns a `DeleteAssetResult` DTO. `SelvagenDeleteAssetComponent` is rewritten to inherit `SelvagenActionComponentBase` (embedded button), take only `Asset ID`, and map the three statuses to status text.

**Tech Stack:** PostgreSQL/PL-pgSQL (Supabase project `aqzfsrebvjkegvfexcut`), C# (.NET 8 / net8.0-windows), Grasshopper SDK, xUnit, System.Text.Json.

---

## File Structure

- **Create** `docs/migrations/2026-06-03-add-delete-asset-by-id-rpc.sql` — checked-in copy of the migration (convention; the live apply happens via the Supabase MCP `apply_migration`).
- **Modify** `src/Selvagen.Core/Models/ApiResponses.cs` — add `DeleteAssetResult` DTO next to `AssetInfo`.
- **Modify** `src/Selvagen.Core/Api/SelvagenClient.cs` — add `DeleteAssetByIdAsync`, remove `DeleteAssetAsync(tableName, assetId)`.
- **Rewrite** `src/Selvagen.GH/Components/SelvagenDeleteAssetComponent.cs` — inherit `SelvagenActionComponentBase`, single input, button, status mapping.
- **Create** `tests/Selvagen.Core.Tests/DeleteAssetResultTests.cs` — DTO deserialization unit tests.
- **Modify** `tests/Selvagen.Core.Tests/RestTests.cs` — add auth-guard test for `DeleteAssetByIdAsync`.

**Testing note (read before Task 1):** The Supabase MCP connection runs as an admin/`postgres` role, which **bypasses RLS** and has a null `auth.uid()`. So the `forbidden` branch (which depends on a non-editor JWT) and the RLS-filtered `not_found`-for-other-firms case **cannot** be unit-tested through the MCP connection. Those are verified end-to-end on the Grasshopper canvas in Task 5. What we CAN test as admin: the `not_found` path for a random UUID, and (Task 5) the multi-table integrity guard.

---

### Task 1: Database migration — `delete_asset_by_id` RPC

**Files:**
- Create: `docs/migrations/2026-06-03-add-delete-asset-by-id-rpc.sql`
- Apply: Supabase project `aqzfsrebvjkegvfexcut` via MCP `apply_migration`

- [ ] **Step 1: Write the migration file**

Create `docs/migrations/2026-06-03-add-delete-asset-by-id-rpc.sql` with exactly:

```sql
-- Finds the asset table that owns p_asset_id and deletes from it, under the
-- caller's RLS (SECURITY INVOKER). SELECT is granted to any active firm member
-- while DELETE requires editor, so a visible-but-undeletable row reports
-- 'forbidden' rather than collapsing into 'not_found'. Scans all five tables so
-- a UUID present in >1 table is refused before any delete.
create or replace function public.delete_asset_by_id(p_asset_id uuid)
returns jsonb
language plpgsql
security invoker
set search_path = public
as $$
declare
  v_found text[] := array[]::text[];
  v_table text;
  v_count integer;
begin
  -- array_append, NOT `||`: appending an untyped literal via || makes Postgres
  -- resolve it as anyarray||anyarray and parse e.g. 'meshes' as an array literal
  -- (22P02 malformed array literal). array_append binds the literal to anyelement.
  if exists (select 1 from public.meshes              where id = p_asset_id) then v_found := array_append(v_found, 'meshes');              end if;
  if exists (select 1 from public.curve_sets          where id = p_asset_id) then v_found := array_append(v_found, 'curve_sets');          end if;
  if exists (select 1 from public.label_sets          where id = p_asset_id) then v_found := array_append(v_found, 'label_sets');          end if;
  if exists (select 1 from public.animation_sequences where id = p_asset_id) then v_found := array_append(v_found, 'animation_sequences'); end if;
  if exists (select 1 from public.color_legends       where id = p_asset_id) then v_found := array_append(v_found, 'color_legends');       end if;

  if array_length(v_found, 1) is null then
    return jsonb_build_object('status', 'not_found');
  end if;
  if array_length(v_found, 1) > 1 then
    raise exception 'Data integrity violation: asset id % found in multiple tables (%).', p_asset_id, v_found;
  end if;

  v_table := v_found[1];
  execute format('delete from public.%I where id = $1', v_table) using p_asset_id;
  get diagnostics v_count = row_count;   -- EXECUTE does not set FOUND

  if v_count > 0 then
    return jsonb_build_object('status', 'deleted', 'table', v_table);
  else
    return jsonb_build_object('status', 'forbidden');
  end if;
end;
$$;

grant execute on function public.delete_asset_by_id(uuid) to authenticated;
```

- [ ] **Step 2: Apply the migration**

Use the Supabase MCP tool `apply_migration` with:
- `project_id`: `aqzfsrebvjkegvfexcut`
- `name`: `add_delete_asset_by_id_rpc`
- `query`: the full SQL from Step 1.

Expected: success, no error.

- [ ] **Step 3: Smoke-test the not_found path**

Use the Supabase MCP tool `execute_sql` with `project_id` `aqzfsrebvjkegvfexcut` and:

```sql
select public.delete_asset_by_id('00000000-0000-0000-0000-000000000000'::uuid);
```

Expected: one row, value `{"status": "not_found"}`.

- [ ] **Step 4: Commit**

```bash
git add docs/migrations/2026-06-03-add-delete-asset-by-id-rpc.sql
git commit -m "feat(db): add delete_asset_by_id RPC (find-and-delete across asset tables)"
```

---

### Task 2: `DeleteAssetResult` DTO

**Files:**
- Modify: `src/Selvagen.Core/Models/ApiResponses.cs`
- Test: `tests/Selvagen.Core.Tests/DeleteAssetResultTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Selvagen.Core.Tests/DeleteAssetResultTests.cs`:

```csharp
using System.Text.Json;
using Selvagen.Core.Models;
using Xunit;

namespace Selvagen.Core.Tests
{
    public class DeleteAssetResultTests
    {
        [Fact]
        public void Deserializes_Deleted_With_Table()
        {
            var json = "{\"status\":\"deleted\",\"table\":\"meshes\"}";
            var result = JsonSerializer.Deserialize<DeleteAssetResult>(json);
            Assert.Equal("deleted", result.Status);
            Assert.Equal("meshes", result.Table);
        }

        [Fact]
        public void Deserializes_NotFound_Without_Table()
        {
            var json = "{\"status\":\"not_found\"}";
            var result = JsonSerializer.Deserialize<DeleteAssetResult>(json);
            Assert.Equal("not_found", result.Status);
            Assert.Equal("", result.Table);
        }

        [Fact]
        public void Deserializes_Forbidden()
        {
            var json = "{\"status\":\"forbidden\"}";
            var result = JsonSerializer.Deserialize<DeleteAssetResult>(json);
            Assert.Equal("forbidden", result.Status);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj --filter DeleteAssetResultTests`
Expected: FAIL — compile error, `DeleteAssetResult` does not exist.

- [ ] **Step 3: Add the DTO**

In `src/Selvagen.Core/Models/ApiResponses.cs`, immediately after the closing `}` of the `AssetInfo` class (currently line 97), insert:

```csharp
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
```

(The file already has `using System.Text.Json.Serialization;` — confirm it is present at the top; if not, add it.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj --filter DeleteAssetResultTests`
Expected: PASS — 3 tests passed.

- [ ] **Step 5: Commit**

```bash
git add src/Selvagen.Core/Models/ApiResponses.cs tests/Selvagen.Core.Tests/DeleteAssetResultTests.cs
git commit -m "feat(core): add DeleteAssetResult DTO"
```

---

### Task 3: `SelvagenClient.DeleteAssetByIdAsync` (+ remove old method)

**Files:**
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs` (add method ~near line 470; remove `DeleteAssetAsync` at lines 450-470)
- Test: `tests/Selvagen.Core.Tests/RestTests.cs`

- [ ] **Step 1: Write the failing test**

In `tests/Selvagen.Core.Tests/RestTests.cs`, add this method inside the `RestTests` class (after `TestUpdateModuleProperty_DoesNotThrow`, before the class closing brace):

```csharp
        [Fact]
        public async Task TestDeleteAssetById_AuthGuard()
        {
            var client = new SelvagenClient(_url, _key);
            // Not logged in: the call must fail the auth guard, not silently succeed.
            try
            {
                await client.DeleteAssetByIdAsync("00000000-0000-0000-0000-000000000000");
            }
            catch (InvalidOperationException ex)
            {
                Assert.Contains("Not authenticated", ex.Message);
            }
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj --filter TestDeleteAssetById_AuthGuard`
Expected: FAIL — compile error, `DeleteAssetByIdAsync` does not exist.

- [ ] **Step 3: Remove the old `DeleteAssetAsync` method**

In `src/Selvagen.Core/Api/SelvagenClient.cs`, delete the entire method (currently lines 450-470):

```csharp
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
```

- [ ] **Step 4: Add the new `DeleteAssetByIdAsync` method**

In the same place where the old method was, insert:

```csharp
        /// <summary>
        /// Delete an asset by its ID alone. Calls the delete_asset_by_id RPC,
        /// which finds the owning table under the caller's RLS and deletes it.
        /// Returns a result whose Status is "deleted" (with Table), "forbidden",
        /// or "not_found".
        /// </summary>
        public async Task<DeleteAssetResult> DeleteAssetByIdAsync(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) throw new ArgumentNullException(nameof(assetId));

            var body = JsonSerializer.Serialize(new { p_asset_id = assetId });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await SendAuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/delete_asset_by_id", content).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new SelvagenApiException($"Delete asset failed: {json}", (int)response.StatusCode);

            return JsonSerializer.Deserialize<DeleteAssetResult>(json);
        }
```

(`SendAuthorizedAsync` calls `EnsureValidTokenAsync`, which throws `InvalidOperationException("Not authenticated...")` when not logged in — that is what the Step 1 test asserts. `Encoding` and `JsonSerializer` are already imported in this file.)

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj --filter TestDeleteAssetById_AuthGuard`
Expected: PASS.

- [ ] **Step 6: Build the whole solution to confirm no dangling references to the removed method**

Run: `dotnet build C:\repos\selvagen-gh\Selvagen.sln -c Debug`
Expected: Build succeeded, 0 errors. (If an error names `DeleteAssetAsync`, a caller was missed — it should only have been the component, rewritten in Task 4. Note: Task 4 must land before the solution builds clean, since the current component still calls the removed method. Build `Selvagen.Core` alone here instead if needed: `dotnet build src/Selvagen.Core/Selvagen.Core.csproj -c Debug`.)

- [ ] **Step 7: Commit**

```bash
git add src/Selvagen.Core/Api/SelvagenClient.cs tests/Selvagen.Core.Tests/RestTests.cs
git commit -m "feat(core): add DeleteAssetByIdAsync RPC client, drop table-based DeleteAssetAsync"
```

---

### Task 4: Rewrite `SelvagenDeleteAssetComponent`

**Files:**
- Rewrite: `src/Selvagen.GH/Components/SelvagenDeleteAssetComponent.cs`

(No unit test: Grasshopper components require the GH runtime to instantiate and solve, and the repo has no component-solve tests. Behavior is verified on the canvas in Task 5.)

- [ ] **Step 1: Replace the file contents entirely**

Overwrite `src/Selvagen.GH/Components/SelvagenDeleteAssetComponent.cs` with:

```csharp
using System;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDeleteAssetComponent : SelvagenActionComponentBase
    {
        public SelvagenDeleteAssetComponent()
            : base("Delete Asset", "SvDelete",
                "Delete an asset (mesh, curve set, label set, animation, or color legend) by ID. The component finds which table it belongs to. [Excluir Asset]",
                "08 Assets")
        { }

        public override Guid ComponentGuid => new Guid("C39D4E5F-A6B7-8901-2CDE-F34567890123");

        // ── ISelvagenActionButton — destructive red button ─────────────────
        public override string ActionLabel        => "Delete";
        public override string ActionLabelRunning => "Deleting...";
        public override Color  ButtonGradientTop    => Color.FromArgb(200, 60, 60);
        public override Color  ButtonGradientBottom => Color.FromArgb(130, 30, 30);

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "ID of the asset to delete [ID do Asset]", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "OK", "Whether deletion succeeded", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Operation status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (!ActionRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(0, false);
                SetReady(DA, 1);
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(0, false);
                DA.SetData(1, "Not logged in");
                return;
            }

            if (string.IsNullOrEmpty(assetId))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Asset ID is required.");
                DA.SetData(0, false);
                DA.SetData(1, "Missing Asset ID");
                return;
            }

            try
            {
                IsRunning = true;
                ForceCanvasRefresh();

                PluginLogger.Log($"SelvagenDeleteAssetComponent: Deleting {assetId}...");
                var result = Task.Run(() => client.DeleteAssetByIdAsync(assetId)).GetAwaiter().GetResult();

                switch (result?.Status)
                {
                    case "deleted":
                        PluginLogger.Log($"SelvagenDeleteAssetComponent: Deleted {assetId} from {result.Table}.");
                        DA.SetData(0, true);
                        DA.SetData(1, $"Deleted {assetId} from {result.Table}");
                        break;
                    case "forbidden":
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "You don't have permission to delete this asset (editor role required).");
                        DA.SetData(0, false);
                        DA.SetData(1, "You don't have permission to delete this asset (editor role required).");
                        break;
                    case "not_found":
                    default:
                        DA.SetData(0, false);
                        DA.SetData(1, "Asset not found (check the ID, or it may already be deleted).");
                        break;
                }
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                PluginLogger.Log($"SelvagenDeleteAssetComponent Error: {msg}");
                DA.SetData(0, false);
                SetActionError(DA, 1, ex);
            }
            finally
            {
                IsRunning = false;
            }
        }

        protected override Bitmap Icon => IconLoader.Load("Delete");
    }
}
```

- [ ] **Step 2: Build the solution**

Run: `dotnet build C:\repos\selvagen-gh\Selvagen.sln -c Debug`
Expected: Build succeeded, 0 errors. (`SetReady`, `SetActionError`, `ForceCanvasRefresh`, `IsRunning`, `ActionRequested` all come from `SelvagenActionComponentBase`; the base supplies `CreateAttributes` → `SelvagenActionAttributes`, which paints the button.)

- [ ] **Step 3: Commit**

```bash
git add src/Selvagen.GH/Components/SelvagenDeleteAssetComponent.cs
git commit -m "feat(gh): Delete Asset embedded button + single Asset ID input"
```

---

### Task 5: Deploy & end-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Deploy to Grasshopper Libraries**

Run (PowerShell):

```powershell
Copy-Item -Path "C:\repos\selvagen-gh\src\Selvagen.GH\bin\Debug\net8.0-windows\*" -Destination "$env:APPDATA\Grasshopper\Libraries\Selvagen\" -Recurse -Force
```

Expected: files copied, no error.

- [ ] **Step 2: Restart Rhino and open Grasshopper**

Close Rhino fully, reopen, open Grasshopper. (Assemblies do not hot-reload.)

- [ ] **Step 3: Verify the component shape**

Drop "Delete Asset" from the Selvagen → 08 Assets tab. Confirm: a single `Asset ID` input, two outputs (`Success`, `Status`), and a red **Delete** button below the body.

- [ ] **Step 4: Verify a successful delete**

Log in (Login component). Use List Assets to get a real Asset ID (try a Color Legend, to confirm the new table coverage). Wire that ID into Delete's `Asset ID`. Click the button.
Expected: button reads "Deleting..." briefly; `Success = true`; `Status = "Deleted <id> from <table>"`. Re-run List Assets and confirm the asset is gone.

- [ ] **Step 5: Verify the not_found path**

Wire a bogus or already-deleted ID and click.
Expected: `Success = false`; `Status = "Asset not found (check the ID, or it may already be deleted)."`

- [ ] **Step 6: Verify the multi-table integrity guard (SQL, rolled back)**

This guard cannot be reached through normal data; force it with a transaction that inserts the same id into two tables and rolls back. Run via Supabase MCP `execute_sql` (`project_id` `aqzfsrebvjkegvfexcut`):

```sql
do $$
declare
  pid uuid;
  test_id uuid := 'ffffffff-ffff-ffff-ffff-ffffffffffff';
  res jsonb;
begin
  select id into pid from public.projects limit 1;
  insert into public.color_legends (id, project_id, name) values (test_id, pid, '__guardtest__');
  insert into public.label_sets   (id, project_id, name) values (test_id, pid, '__guardtest__');
  begin
    res := public.delete_asset_by_id(test_id);
    raise notice 'UNEXPECTED: no exception, got %', res;
  exception when others then
    raise notice 'OK: guard raised: %', sqlerrm;
  end;
  rollback;
end $$;
```

Expected: a notice `OK: guard raised: Data integrity violation: asset id ... found in multiple tables ...`, and (because of `rollback`) no rows persisted. If the inserts fail on a missing required column, add the minimal required columns for those tables and retry — the assertion is only that the function raises when the id is in two tables.

- [ ] **Step 7: Final commit (verification notes, if any artifacts changed)**

Nothing to commit if no files changed. Otherwise commit any incidental fixes made during verification with a descriptive message.

---

## Notes for the implementer

- **Run Tasks 2 and 3 before building the full solution.** The pre-Task-4 component still references the removed `DeleteAssetAsync`, so the *solution* won't compile clean until Task 4 lands. Task 3 Step 6 builds `Selvagen.Core` alone to verify the core layer in isolation.
- **`forbidden` cannot be unit-tested via the admin MCP connection** (it bypasses RLS). It is exercised manually in Task 5 only if a non-editor test session is available; otherwise its logic is covered by inspection and the deleted/not_found paths.
- **Status strings are user-visible** and intentionally match the spec. If you change wording, change it in one place only — `SolveInstance`.
