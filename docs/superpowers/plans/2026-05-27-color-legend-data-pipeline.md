# Color Legend Data Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an end-to-end pipeline for color legend data from Grasshopper → Supabase → Platform, enabling the Legend canvas component to display GH-authored color scales.

**Architecture:** New `color_legends` Supabase table with FK columns on module tables. New GH upload component + optional Legend ID input on 19 existing module components. Platform Legend component fetches legend data via React Query, with auto-binding detection in the Inspector.

**Tech Stack:** C# (.NET 8 / .NET 4.8), Grasshopper SDK, Supabase (PostgREST, RLS), TypeScript, TanStack Query, TanStack Start, React

**Repos:**
- GH Plugin: `C:\repos\selvagen-gh`
- Platform: `C:\repos\selvagen`

**Spec:** `docs/superpowers/specs/2026-05-27-color-legend-data-pipeline-design.md`

---

## File Structure

### New Files

| File | Repo | Purpose |
|------|------|---------|
| `supabase/migrations/20260527_color_legends.sql` | platform | Table, FKs, RLS, trigger, realtime |
| `src/Selvagen.Core/Models/ColorLegend.cs` | GH | Payload + response models |
| `src/Selvagen.GH/Components/SelvagenUploadColorLegendComponent.cs` | GH | Upload component |
| `src/queries/color-legends.ts` | platform | List + detail query hooks |
| `src/lib/legend-binding.ts` | platform | Module binding detection utility |

### Modified Files

| File | Repo | Change |
|------|------|--------|
| `src/Selvagen.Core/Api/SelvagenClient.cs` | GH | Add legend CRUD methods |
| 19 module component `.cs` files (see Task 6) | GH | Add optional Legend ID input |
| `src/components/canvas/components/LegendComponent.tsx` | platform | Fetch + merge legend data |
| `src/components/canvas/Inspector.tsx` | platform | Legend picker + auto-binding |
| `src/components/canvas/types.ts` | platform | Add `'legend_id'` to bindingFields |

---

## Task 1: Supabase Migration

**Files:**
- Create: `C:\repos\selvagen\supabase\migrations\20260527_color_legends.sql`

- [ ] **Step 1: Write the migration file**

```sql
-- =============================================================
-- Color Legends table + module FK columns
-- =============================================================

-- 1. Create the color_legends table
CREATE TABLE color_legends (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  project_id  UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  name        TEXT NOT NULL,
  variant     TEXT NOT NULL CHECK (variant IN ('gradient', 'discrete')),
  colors      TEXT[] NOT NULL,
  labels      TEXT[],
  domain_min  REAL,
  domain_max  REAL,
  unit        TEXT,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

  UNIQUE (project_id, name)
);

-- 2. RLS
ALTER TABLE color_legends ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Project members can view color legends"
  ON color_legends FOR SELECT
  USING (
    project_id IN (
      SELECT p.id FROM projects p
      JOIN firms f ON f.id IN (p.firm_id, p.client_id)
      JOIN firm_members fm ON fm.firm_id = f.id
      WHERE fm.user_id = auth.uid()
    )
  );

CREATE POLICY "Project engineers can manage color legends"
  ON color_legends FOR ALL
  USING (
    project_id IN (
      SELECT p.id FROM projects p
      JOIN firms f ON f.id IN (p.firm_id, p.client_id)
      JOIN firm_members fm ON fm.firm_id = f.id
      WHERE fm.user_id = auth.uid()
        AND fm.role IN ('admin', 'engineer')
    )
  );

-- 3. updated_at trigger
CREATE TRIGGER set_color_legends_updated_at
  BEFORE UPDATE ON color_legends
  FOR EACH ROW
  EXECUTE FUNCTION update_updated_at();

-- 4. Realtime
ALTER PUBLICATION supabase_realtime ADD TABLE color_legends;

-- 5. FK columns on topography
ALTER TABLE topography ADD COLUMN elevation_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE topography ADD COLUMN contours_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE topography ADD COLUMN slope_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE topography ADD COLUMN access5_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE topography ADD COLUMN access8_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;

-- 6. FK columns on geology
ALTER TABLE geology ADD COLUMN soil_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE geology ADD COLUMN rock_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE geology ADD COLUMN coverage_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE geology ADD COLUMN depth_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE geology ADD COLUMN rippability_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;

-- 7. FK columns on analyses
ALTER TABLE analyses ADD COLUMN rock_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE analyses ADD COLUMN access_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE analyses ADD COLUMN earthworks_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE analyses ADD COLUMN retention_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;

-- 8. FK columns on optimizations
ALTER TABLE optimizations ADD COLUMN access_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE optimizations ADD COLUMN earth_terrain_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE optimizations ADD COLUMN earth_lots_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE optimizations ADD COLUMN earth_total_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
ALTER TABLE optimizations ADD COLUMN retention_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL;
```

- [ ] **Step 2: Apply the migration**

Use the Supabase MCP tool `apply_migration` or run:
```bash
cd C:\repos\selvagen
npx supabase db push
```
Expected: Migration applies cleanly. Verify via `list_tables` that `color_legends` appears.

- [ ] **Step 3: Verify the table exists**

Run SQL via Supabase MCP:
```sql
SELECT column_name, data_type FROM information_schema.columns
WHERE table_name = 'color_legends' ORDER BY ordinal_position;
```
Expected: 10 columns (id, project_id, name, variant, colors, labels, domain_min, domain_max, unit, created_at, updated_at).

- [ ] **Step 4: Verify FK columns on module tables**

```sql
SELECT column_name FROM information_schema.columns
WHERE table_name = 'topography' AND column_name LIKE '%_legend_id';
```
Expected: 5 columns (elevation_legend_id, contours_legend_id, slope_legend_id, access5_legend_id, access8_legend_id).

- [ ] **Step 5: Commit**

```bash
cd C:\repos\selvagen
git add supabase/migrations/20260527_color_legends.sql
git commit -m "feat(db): add color_legends table and module FK columns"
```

---

## Task 2: Regenerate TypeScript Types

**Files:**
- Modify: `C:\repos\selvagen\src\types\database.types.ts`

- [ ] **Step 1: Regenerate types from Supabase**

Use Supabase MCP `generate_typescript_types` or run:
```bash
cd C:\repos\selvagen
npx supabase gen types typescript --project-id aqzfsrebvjkegvfexcut > src/types/database.types.ts
```

- [ ] **Step 2: Verify color_legends type exists**

Open `src/types/database.types.ts` and confirm it contains a `color_legends` table definition with all columns. Also verify that `topography`, `geology`, `analyses`, `optimizations` now include the `_legend_id` columns.

- [ ] **Step 3: Commit**

```bash
cd C:\repos\selvagen
git add src/types/database.types.ts
git commit -m "chore: regenerate database types with color_legends"
```

---

## Task 3: GH Core — ColorLegend Models

**Files:**
- Create: `C:\repos\selvagen-gh\src\Selvagen.Core\Models\ColorLegend.cs`

- [ ] **Step 1: Create the model file**

```csharp
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
```

- [ ] **Step 2: Build to verify compilation**

```powershell
cd C:\repos\selvagen-gh
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Selvagen.Core/Models/ColorLegend.cs
git commit -m "feat(core): add ColorLegend models"
```

---

## Task 4: GH Core — SelvagenClient Legend Methods

**Files:**
- Modify: `C:\repos\selvagen-gh\src\Selvagen.Core\Api\SelvagenClient.cs`

- [ ] **Step 1: Add ListColorLegendsAsync**

Add after the existing `ListAnimationSequencesAsync` method:

```csharp
public async Task<ColorLegendInfo[]> ListColorLegendsAsync(string projectId)
{
    await EnsureValidTokenAsync();
    var url = $"{_supabaseUrl}/rest/v1/color_legends?project_id=eq.{projectId}&select=id,name,variant,colors,labels,domain_min,domain_max,unit&order=name";
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    AddAuthHeaders(request);
    var response = await _http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new SelvagenApiException($"List color legends failed: {response.StatusCode}", body);
    return JsonSerializer.Deserialize<ColorLegendInfo[]>(body, _jsonOptions);
}
```

- [ ] **Step 2: Add UpsertColorLegendAsync**

```csharp
public async Task<ColorLegendInfo> UpsertColorLegendAsync(string projectId, string name, ColorLegendPayload payload)
{
    await EnsureValidTokenAsync();
    payload.ProjectId = projectId;
    payload.Name = name;
    var url = $"{_supabaseUrl}/rest/v1/color_legends?on_conflict=project_id,name";
    var json = JsonSerializer.Serialize(payload, _jsonOptions);
    var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };
    AddAuthHeaders(request);
    request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
    var response = await _http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new SelvagenApiException($"Upsert color legend failed: {response.StatusCode}", body);
    var results = JsonSerializer.Deserialize<ColorLegendInfo[]>(body, _jsonOptions);
    return results[0];
}
```

- [ ] **Step 3: Add DeleteColorLegendAsync**

```csharp
public async Task DeleteColorLegendAsync(string legendId)
{
    await EnsureValidTokenAsync();
    var url = $"{_supabaseUrl}/rest/v1/color_legends?id=eq.{legendId}";
    var request = new HttpRequestMessage(HttpMethod.Delete, url);
    AddAuthHeaders(request);
    var response = await _http.SendAsync(request);
    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        throw new SelvagenApiException($"Delete color legend failed: {response.StatusCode}", body);
    }
}
```

- [ ] **Step 4: Build to verify**

```powershell
cd C:\repos\selvagen-gh
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Selvagen.Core/Api/SelvagenClient.cs
git commit -m "feat(core): add color legend CRUD methods to SelvagenClient"
```

---

## Task 5: GH — Upload Color Legend Component

**Files:**
- Create: `C:\repos\selvagen-gh\src\Selvagen.GH\Components\SelvagenUploadColorLegendComponent.cs`

- [ ] **Step 1: Create the component**

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Models;
using Selvagen.GH.Utils;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadColorLegendComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadColorLegendComponent()
            : base("Upload Color Legend", "SvUpLgd",
                "Upload a color legend to the platform. [Upload de Legenda de Cores]")
        { }

        public override Guid ComponentGuid => new Guid("A6B7C8D9-E0F1-4234-8567-890ABCDEF456");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID",
                "Target project ID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N",
                "Legend display name [Nome]", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Variant", "V",
                "0 = gradient, 1 = discrete [Variante]", GH_ParamAccess.item, 0);
            pManager.AddColourParameter("Colors", "C",
                "List of colors [Cores]", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "L",
                "Per-color labels (discrete only) [Rótulos]", GH_ParamAccess.list);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("Domain Min", "Min",
                "Start of value range (gradient only) [Domínio Mín]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("Domain Max", "Max",
                "End of value range (gradient only) [Domínio Máx]", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddTextParameter("Unit", "U",
                "Display unit, e.g. %, °, m [Unidade]", GH_ParamAccess.item);
            pManager[7].Optional = true;

            var variantParam = Params.Input[2] as Grasshopper.Kernel.Parameters.Param_Integer;
            variantParam?.AddNamedValue("Gradient", 0);
            variantParam?.AddNamedValue("Discrete", 1);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Legend ID", "LgdID",
                "ID of the created/updated legend [ID da Legenda]", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S",
                "Upload status [Status]", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            int variant = 0;
            var colors = new List<Color>();

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref name);
            DA.GetData(2, ref variant);
            DA.GetDataList(3, colors);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || colors.Count == 0
                || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Provide Project ID, Name, and Colors before uploading.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var hexColors = new string[colors.Count];
                for (int i = 0; i < colors.Count; i++)
                {
                    var c = colors[i];
                    hexColors[i] = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }

                var labels = new List<string>();
                DA.GetDataList(4, labels);

                double domainMin = 0, domainMax = 0;
                bool hasMin = DA.GetData(5, ref domainMin);
                bool hasMax = DA.GetData(6, ref domainMax);

                string unit = null;
                DA.GetData(7, ref unit);

                var payload = new ColorLegendPayload
                {
                    Variant = variant == 0 ? "gradient" : "discrete",
                    Colors = hexColors,
                    Labels = labels.Count > 0 ? labels.ToArray() : null,
                    DomainMin = hasMin ? (float)domainMin : null,
                    DomainMax = hasMax ? (float)domainMax : null,
                    Unit = string.IsNullOrEmpty(unit) ? null : unit,
                };

                var result = Task.Run(() =>
                    client.UpsertColorLegendAsync(projectId, name, payload))
                    .GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({result.Variant})");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, 1, ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        protected override Bitmap Icon => IconLoader.Load("UploadColorLegend");
    }
}
```

- [ ] **Step 2: Create the icon**

Create a 24×24 PNG icon at `src/Selvagen.GH/Icons/UploadColorLegend.png`. Follow the existing icon style (monochrome white on transparent, similar to UploadMesh). Design suggestion: a rectangular gradient swatch with an upload arrow.

- [ ] **Step 3: Build to verify**

```powershell
cd C:\repos\selvagen-gh
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Selvagen.GH/Components/SelvagenUploadColorLegendComponent.cs
git add src/Selvagen.GH/Icons/UploadColorLegend.png
git commit -m "feat(gh): add Upload Color Legend component"
```

---

## Task 6: GH — Add Legend ID to Module Components

**Files (19 components to modify):**

| File | Legend Column |
|------|-------------|
| `src/Selvagen.GH/Components/Topography/TopoElevationComponent.cs` | `elevation_legend_id` |
| `src/Selvagen.GH/Components/Topography/TopoContoursComponent.cs` | `contours_legend_id` |
| `src/Selvagen.GH/Components/Topography/TopoSlopeComponent.cs` | `slope_legend_id` |
| `src/Selvagen.GH/Components/Topography/TopoAccess5Component.cs` | `access5_legend_id` |
| `src/Selvagen.GH/Components/Topography/TopoAccess8Component.cs` | `access8_legend_id` |
| `src/Selvagen.GH/Components/Geology/GeoSoilComponent.cs` | `soil_legend_id` |
| `src/Selvagen.GH/Components/Geology/GeoRockComponent.cs` | `rock_legend_id` |
| `src/Selvagen.GH/Components/Geology/GeoCoverageComponent.cs` | `coverage_legend_id` |
| `src/Selvagen.GH/Components/Geology/GeoDepthComponent.cs` | `depth_legend_id` |
| `src/Selvagen.GH/Components/Geology/GeoRippabilityComponent.cs` | `rippability_legend_id` |
| `src/Selvagen.GH/Components/Analyses/AnlRockComponent.cs` | `rock_legend_id` |
| `src/Selvagen.GH/Components/Analyses/AnlAccessComponent.cs` | `access_legend_id` |
| `src/Selvagen.GH/Components/Analyses/AnlEarthworksComponent.cs` | `earthworks_legend_id` |
| `src/Selvagen.GH/Components/Analyses/AnlRetentionComponent.cs` | `retention_legend_id` |
| `src/Selvagen.GH/Components/Optimizations/OptAccessComponent.cs` | `access_legend_id` |
| `src/Selvagen.GH/Components/Optimizations/OptEarthTerrainComponent.cs` | `earth_terrain_legend_id` |
| `src/Selvagen.GH/Components/Optimizations/OptEarthLotsComponent.cs` | `earth_lots_legend_id` |
| `src/Selvagen.GH/Components/Optimizations/OptEarthTotalComponent.cs` | `earth_total_legend_id` |
| `src/Selvagen.GH/Components/Optimizations/OptRetentionComponent.cs` | `retention_legend_id` |

The modification pattern is identical for all 19. Two changes per file:

**Change 1:** In `RegisterInputParams`, add a Legend ID parameter **immediately before** the Upload parameter:

```csharp
pManager.AddTextParameter("Legend ID", "LgdID",
    "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
pManager[INDEX].Optional = true;
```

Where `INDEX` is one less than the Upload parameter's index. The Upload button (boolean) must remain the last input.

**Change 2:** In `CollectValues`, add one line to include the legend ID in the PATCH dictionary:

```csharp
if (TryGetText(DA, LEGEND_INDEX, out var legendId))
    values["COLUMN_NAME"] = legendId;
```

Where `LEGEND_INDEX` is the index of the new Legend ID parameter, and `COLUMN_NAME` is from the table above.

- [ ] **Step 1: Modify TopoSlopeComponent (template)**

**File:** `C:\repos\selvagen-gh\src\Selvagen.GH\Components\Topography\TopoSlopeComponent.cs`

In `RegisterInputParams`, insert two new lines **immediately before** the existing `AddBooleanParameter("Upload", ...)` line. Do NOT remove or re-register the Upload parameter — just insert before it:

```csharp
// INSERT these two lines before the existing Upload line:
pManager.AddTextParameter("Legend ID", "LgdID",
    "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
pManager[6].Optional = true;
// The existing Upload registration follows (now at index 7 instead of 6)
```

In `CollectValues`, add after the existing entries. The Legend ID is at the index just before Upload — check the parameter count to find it:

```csharp
// Legend ID is at index (Params.Input.Count - 2), one before Upload
if (TryGetText(DA, Params.Input.Count - 2, out var legendId))
    values["slope_legend_id"] = legendId;
```

**Note:** Using `Params.Input.Count - 2` instead of a hardcoded index makes this pattern safe regardless of how many other parameters each component has. Upload is always last (found by name in the base class), Legend ID is always second-to-last.

- [ ] **Step 2: Modify all remaining 18 components**

Apply the same two-change pattern to each file in the table above:

**Change 1 (RegisterInputParams):** Insert these two lines before the existing `Upload` boolean parameter:
```csharp
pManager.AddTextParameter("Legend ID", "LgdID",
    "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
pManager[INDEX].Optional = true;  // INDEX = position of the new param
```

**Change 2 (CollectValues):** Add this line at the end of the method, using the column name from the table:
```csharp
if (TryGetText(DA, Params.Input.Count - 2, out var legendId))
    values["COLUMN_NAME"] = legendId;
```

`Params.Input.Count - 2` works for all components because Legend ID is always second-to-last (before Upload).

- [ ] **Step 4: Build the full solution**

```powershell
cd C:\repos\selvagen-gh
dotnet build
```
Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add src/Selvagen.GH/Components/
git commit -m "feat(gh): add optional Legend ID input to all 19 module components"
```

---

## Task 7: GH — Build, Deploy & Verify

**Files:**
- No new files — build + deploy step

- [ ] **Step 1: Build Release**

```powershell
cd C:\repos\selvagen-gh
dotnet build -c Release
```

- [ ] **Step 2: Deploy to Grasshopper Libraries**

```powershell
$src = "C:\repos\selvagen-gh\src\Selvagen.GH\bin\Release\net8.0"
$dst = "$env:APPDATA\Grasshopper\Libraries\Selvagen"
Copy-Item "$src\*" "$dst\" -Recurse -Force
```

- [ ] **Step 3: Restart Rhino and verify**

1. Close and reopen Rhino + Grasshopper
2. Search for "Upload Color Legend" in the component search — should appear under Selvagen > 08 Assets
3. Place it on canvas — should show inputs: Project ID, Name, Variant (with dropdown), Colors, Labels, Domain Min, Domain Max, Unit
4. Place any module component (e.g., Topo Slope) — should now show Legend ID input before Upload
5. Wire up a test: Login → Project → Upload Color Legend → wire Legend ID to Topo Slope → Upload

- [ ] **Step 4: Verify data in Supabase**

After uploading, run via Supabase MCP:
```sql
SELECT * FROM color_legends ORDER BY created_at DESC LIMIT 1;
```
Expected: Row with the uploaded name, variant, colors, etc.

```sql
SELECT slope_legend_id FROM topography WHERE project_id = '<test-project-id>';
```
Expected: The legend ID should match.

---

**Note on DataContext:** The spec mentions updating module queries to SELECT `_legend_id` columns. No changes needed — the existing queries in `src/queries/modules.ts` and `src/queries/domain-rows.ts` use `select('*')`, so the new columns are automatically included in the DataContext after the migration runs.

---

## Task 8: Platform — Color Legend Query Hooks

**Files:**
- Create: `C:\repos\selvagen\src\queries\color-legends.ts`

- [ ] **Step 1: Create the query file**

```typescript
import { queryOptions } from '@tanstack/react-query'
import { createServerFn } from '@tanstack/react-start'
import { createClient } from '@/lib/supabase/server'

const fetchColorLegends = createServerFn({ method: 'GET' })
  .validator((input: { projectId: string }) => input)
  .handler(async ({ data }) => {
    const client = await createClient()
    const { data: legends, error } = await client
      .from('color_legends')
      .select('*')
      .eq('project_id', data.projectId)
      .order('name')
    if (error) throw error
    return legends
  })

const fetchColorLegend = createServerFn({ method: 'GET' })
  .validator((input: { legendId: string; projectId: string }) => input)
  .handler(async ({ data }) => {
    const client = await createClient()
    const { data: legend, error } = await client
      .from('color_legends')
      .select('*')
      .eq('id', data.legendId)
      .eq('project_id', data.projectId)
      .single()
    if (error) throw error
    return legend
  })

export const colorLegends = {
  list: (projectId: string) =>
    queryOptions({
      queryKey: [projectId, 'color-legends'] as const,
      queryFn: () => fetchColorLegends({ data: { projectId } }),
    }),

  detail: (legendId: string, projectId: string) =>
    queryOptions({
      queryKey: [projectId, 'color-legends', legendId] as const,
      queryFn: () => fetchColorLegend({ data: { legendId, projectId } }),
      enabled: !!legendId,
    }),
}
```

- [ ] **Step 2: Verify TypeScript compiles**

```powershell
cd C:\repos\selvagen
npx tsc --noEmit
```
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
cd C:\repos\selvagen
git add src/queries/color-legends.ts
git commit -m "feat: add color legend query hooks"
```

---

## Task 9: Platform — Legend Binding Utility

**Files:**
- Create: `C:\repos\selvagen\src\lib\legend-binding.ts`

- [ ] **Step 1: Create the utility file**

```typescript
import type { DataContext } from '@/components/canvas/types'

export interface ModuleBinding {
  namespace: string
  field: string
}

export function findModuleBindings(
  legendId: string,
  ctx: DataContext,
): ModuleBinding[] {
  const matches: ModuleBinding[] = []
  for (const [namespace, moduleData] of Object.entries(ctx)) {
    if (!moduleData || typeof moduleData !== 'object') continue
    for (const [field, value] of Object.entries(
      moduleData as Record<string, unknown>,
    )) {
      if (field.endsWith('_legend_id') && value === legendId)
        matches.push({ namespace, field })
    }
  }
  return matches
}

export function bindingToExpression(binding: ModuleBinding): string {
  return `\${${binding.namespace}.${binding.field}}`
}

export function legendFieldLabel(field: string): string {
  return field
    .replace(/_legend_id$/, '')
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
}
```

- [ ] **Step 2: Verify compiles**

```powershell
cd C:\repos\selvagen
npx tsc --noEmit
```

- [ ] **Step 3: Commit**

```bash
cd C:\repos\selvagen
git add src/lib/legend-binding.ts
git commit -m "feat: add legend module binding detection utility"
```

---

## Task 10: Platform — Update Legend Component

**Files:**
- Modify: `C:\repos\selvagen\src\components\canvas\components\LegendComponent.tsx`

- [ ] **Step 1: Add legend_id to bindingFields**

Find the component definition's `bindingFields` array and add `'legend_id'`:

```typescript
bindingFields: ['title', 'startLabel', 'endLabel', 'legend_id'],
```

- [ ] **Step 2: Add data-fetching logic**

At the top of the Legend render function (or the component body), add the query hook:

```typescript
import { useQuery } from '@tanstack/react-query'
import { colorLegends } from '@/queries/color-legends'

// Inside the component, extract legendId and projectId:
const legendId = (resolved.legend_id as string) ?? (config.legend_id as string) ?? null
const projectId = /* get from context — same as Viewport3D gets it */

const { data: legend } = useQuery({
  ...colorLegends.detail(legendId!, projectId!),
  enabled: !!legendId && !!projectId,
})
```

- [ ] **Step 3: Merge fetched legend data with manual config**

Replace the existing config reads with merged values:

```typescript
const variant = legend?.variant ?? (config.variant as string) ?? 'gradient'
const colors = legend?.colors ?? (config.colors as string[]) ?? []
const labels = legend?.labels ?? (config.labels as string[]) ?? []
const title = (resolved.title as string)
  ?? legend?.name
  ?? (config.title as string)
  ?? ''

const startLabel = (resolved.startLabel as string)
  ?? (legend
    ? `${legend.domain_min ?? ''}${legend.unit ?? ''}`
    : (config.startLabel as string) ?? '')

const endLabel = (resolved.endLabel as string)
  ?? (legend
    ? `${legend.domain_max ?? ''}${legend.unit ?? ''}`
    : (config.endLabel as string) ?? '')
```

- [ ] **Step 4: Verify compiles and renders**

```powershell
cd C:\repos\selvagen
npx tsc --noEmit
npm run dev
```

Open the app, navigate to a slide with a Legend component. It should still render with manual config (no legend_id set yet).

- [ ] **Step 5: Commit**

```bash
cd C:\repos\selvagen
git add src/components/canvas/components/LegendComponent.tsx
git commit -m "feat: Legend component fetches and merges color_legends data"
```

---

## Task 11: Platform — Legend Inspector with Auto-Binding

**Files:**
- Modify: `C:\repos\selvagen\src\components\canvas\Inspector.tsx`

- [ ] **Step 1: Add Legend Inspector panel**

In the Inspector component, add a case for the `legend` component type that renders a custom panel. Add this alongside the existing TextInspector, IconInspector, etc.:

```typescript
import { useQuery } from '@tanstack/react-query'
import { colorLegends } from '@/queries/color-legends'
import {
  findModuleBindings,
  bindingToExpression,
  legendFieldLabel,
  type ModuleBinding,
} from '@/lib/legend-binding'

function LegendInspector({
  component,
  projectId,
  dataContext,
  onUpdate,
}: {
  component: SlideComponent
  projectId: string
  dataContext: DataContext
  onUpdate: (patch: Partial<SlideComponent>) => void
}) {
  const { data: legends = [] } = useQuery(colorLegends.list(projectId))

  const currentLegendId =
    component.bindings?.legend_id
      ? resolveExpression(component.bindings.legend_id, dataContext)
      : (component.config?.legend_id as string | undefined)

  function onLegendSelected(legendId: string | null) {
    if (!legendId) {
      const { legend_id: _, ...restBindings } = component.bindings ?? {}
      const { legend_id: __, ...restConfig } = (component.config ?? {}) as Record<string, unknown>
      onUpdate({ bindings: restBindings, config: restConfig })
      return
    }

    const matches = findModuleBindings(legendId, dataContext)

    if (matches.length === 1) {
      const expr = bindingToExpression(matches[0])
      onUpdate({
        bindings: { ...component.bindings, legend_id: expr },
        config: { ...component.config, legend_id: undefined },
      })
    } else if (matches.length > 1) {
      // Multiple matches — store static for now, user disambiguates
      onUpdate({
        bindings: { ...component.bindings, legend_id: undefined },
        config: { ...component.config, legend_id: legendId },
      })
    } else {
      onUpdate({
        bindings: { ...component.bindings, legend_id: undefined },
        config: { ...component.config, legend_id: legendId },
      })
    }
  }

  function onBindingSelected(binding: ModuleBinding) {
    const expr = bindingToExpression(binding)
    onUpdate({
      bindings: { ...component.bindings, legend_id: expr },
      config: { ...component.config, legend_id: undefined },
    })
  }

  const matches = currentLegendId
    ? findModuleBindings(currentLegendId, dataContext)
    : []
  const isBound = !!component.bindings?.legend_id && matches.length > 0
  const selectedLegend = legends.find((l) => l.id === currentLegendId)

  return (
    <div className="space-y-3">
      <div>
        <label className="text-xs text-muted-foreground">Color Legend</label>
        <select
          className="w-full rounded border bg-background px-2 py-1 text-sm"
          value={currentLegendId ?? ''}
          onChange={(e) => onLegendSelected(e.target.value || null)}
        >
          <option value="">None</option>
          {legends.map((l) => (
            <option key={l.id} value={l.id}>
              {l.name}
            </option>
          ))}
        </select>

        {currentLegendId && isBound && (
          <p className="mt-1 text-xs text-emerald-500">
            🔗 Bound to {matches[0].namespace} → {legendFieldLabel(matches[0].field)}
          </p>
        )}
        {currentLegendId && !isBound && (
          <p className="mt-1 text-xs text-muted-foreground">
            Static reference — not bound to a module
          </p>
        )}
      </div>

      {/* Disambiguation UI for multiple matches */}
      {currentLegendId && !isBound && matches.length > 1 && (
        <div>
          <label className="text-xs text-muted-foreground">
            This legend is referenced by multiple modules. Pick a binding:
          </label>
          <div className="mt-1 space-y-1">
            {matches.map((m) => (
              <button
                key={`${m.namespace}.${m.field}`}
                className="block w-full rounded border px-2 py-1 text-left text-xs hover:bg-accent"
                onClick={() => onBindingSelected(m)}
              >
                🔗 {m.namespace} → {legendFieldLabel(m.field)}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Preview */}
      {selectedLegend && (
        <div>
          <label className="text-xs text-muted-foreground">Preview</label>
          {selectedLegend.variant === 'gradient' ? (
            <div className="flex items-center gap-2">
              <div
                className="h-5 flex-1 rounded"
                style={{
                  background: `linear-gradient(to right, ${selectedLegend.colors.join(', ')})`,
                }}
              />
              <span className="text-xs text-muted-foreground">
                {selectedLegend.domain_min}{selectedLegend.unit ?? ''} →{' '}
                {selectedLegend.domain_max}{selectedLegend.unit ?? ''}
              </span>
            </div>
          ) : (
            <div className="flex flex-wrap gap-1">
              {selectedLegend.colors.map((c, i) => (
                <div key={i} className="flex items-center gap-1">
                  <div
                    className="h-3 w-4 rounded-sm"
                    style={{ backgroundColor: c }}
                  />
                  <span className="text-xs text-muted-foreground">
                    {selectedLegend.labels?.[i] ?? ''}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Wire LegendInspector into the Inspector dispatch**

In the Inspector's component-type switch/conditional, add the legend case. Find where TextInspector, IconInspector, Viewport3DInspector are conditionally rendered and add:

```typescript
{component.type === 'legend' && (
  <LegendInspector
    component={component}
    projectId={projectId}
    dataContext={dataContext}
    onUpdate={handleUpdate}
  />
)}
```

- [ ] **Step 3: Verify in browser**

1. Run `npm run dev`
2. Navigate to a slide, add a Legend component
3. Open the Inspector — should show the "Color Legend" dropdown
4. If legends exist for the project (from Task 7), they should appear in the dropdown
5. Selecting one should show the preview and binding status

- [ ] **Step 4: Commit**

```bash
cd C:\repos\selvagen
git add src/components/canvas/Inspector.tsx
git commit -m "feat: Legend Inspector with dropdown picker and auto-binding detection"
```

---

## Task 12: End-to-End Verification

- [ ] **Step 1: Full pipeline test**

1. Open Rhino + Grasshopper with the deployed plugin
2. Wire up: Login → Project selector → Upload Color Legend (gradient, 3 colors, domain 0–100, unit "%") → wire Legend ID to a module component (e.g., Topo Slope) → Upload both
3. Open the platform, navigate to the same project
4. Add a Legend component to a slide
5. Open Inspector → select the legend from the dropdown
6. Verify: chain link icon appears, "Bound to topography → slope" hint, gradient preview renders, Legend component on canvas shows the color scale

- [ ] **Step 2: Test discrete legend**

1. In GH, upload a discrete legend (3 colors + 3 labels, no domain)
2. In the platform, select it in the Legend Inspector
3. Verify discrete color swatches + labels render correctly

- [ ] **Step 3: Test backward compatibility**

1. Existing Legend components with only manual config (no legend_id) should still render exactly as before
2. Existing module components without Legend ID wired should still upload successfully
3. Verify no regressions in other component types
