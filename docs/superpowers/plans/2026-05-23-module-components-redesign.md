# Module Components Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace 4 mega-components (Topography, Geology, Analyses, Optimizations) with 23 small, self-contained group components that each independently PATCH their module's DB row.

**Architecture:** Each new component inherits `SelvagenModuleComponentBase` and overrides `ModuleTable`, `CollectValues`, `ComponentGuid`, and `Icon`. Every component has 3 inputs minimum (ProjectID + group fields + Upload). The base class handles create-or-PATCH logic unchanged. A standalone Properties component uses a face dropdown to select the target module.

**Tech Stack:** C# / .NET (net48 + net8.0 + net8.0-windows), Grasshopper SDK 8.*, Supabase PostgREST PATCH

---

## File Structure

### New files (25)

| Path | Responsibility |
|------|---------------|
| `src/Selvagen.GH/Components/Topography/TopoBaseComponent.cs` | Base mesh/area/TDR fields → `topography` |
| `src/Selvagen.GH/Components/Topography/TopoContoursComponent.cs` | Outline/contour curves + interval → `topography` |
| `src/Selvagen.GH/Components/Topography/TopoUrbanizationComponent.cs` | Urbanization curves → `topography` |
| `src/Selvagen.GH/Components/Topography/TopoElevationComponent.cs` | Elevation mesh/curves/min/max → `topography` |
| `src/Selvagen.GH/Components/Topography/TopoSlopeComponent.cs` | Slope mesh/ref/rates/min/max → `topography` |
| `src/Selvagen.GH/Components/Topography/TopoAccess8Component.cs` | Access 8m mesh/ref/rate → `topography` |
| `src/Selvagen.GH/Components/Topography/TopoAccess5Component.cs` | Access 5m mesh/ref/rate → `topography` |
| `src/Selvagen.GH/Components/Topography/TopoDrainageComponent.cs` | Drainage curves/flow/concentration → `topography` |
| `src/Selvagen.GH/Components/Geology/GeoCoverageComponent.cs` | Coverage mesh/points/area/rate → `geology` |
| `src/Selvagen.GH/Components/Geology/GeoRockComponent.cs` | Rock mesh/curves/interval → `geology` |
| `src/Selvagen.GH/Components/Geology/GeoRippabilityComponent.cs` | Rippability mesh → `geology` |
| `src/Selvagen.GH/Components/Geology/GeoSoilComponent.cs` | Soil mesh/height min/max → `geology` |
| `src/Selvagen.GH/Components/Geology/GeoDepthComponent.cs` | Depth mesh/ref/usability → `geology` |
| `src/Selvagen.GH/Components/Analyses/AnlEarthworksComponent.cs` | Earthworks meshes/volumes/costs → `analyses` |
| `src/Selvagen.GH/Components/Analyses/AnlRetentionComponent.cs` | Retention height/area/cost → `analyses` |
| `src/Selvagen.GH/Components/Analyses/AnlRockComponent.cs` | Rock mesh/labels/height/volume → `analyses` |
| `src/Selvagen.GH/Components/Analyses/AnlAccessComponent.cs` | Access curves/labels/ref/rate → `analyses` |
| `src/Selvagen.GH/Components/Optimizations/OptAccessComponent.cs` | Access curves/labels/ref/rate → `optimizations` |
| `src/Selvagen.GH/Components/Optimizations/OptEarthTerrainComponent.cs` | Terrain mesh/volumes → `optimizations` |
| `src/Selvagen.GH/Components/Optimizations/OptEarthLotsComponent.cs` | Lots mesh/volumes → `optimizations` |
| `src/Selvagen.GH/Components/Optimizations/OptEarthTotalComponent.cs` | Total volumes/costs → `optimizations` |
| `src/Selvagen.GH/Components/Optimizations/OptRetentionComponent.cs` | Retention height/area/cost → `optimizations` |
| `src/Selvagen.GH/Components/Shared/SelvagenPropertiesComponent.cs` | JSON properties → any module (via dropdown) |
| `src/Selvagen.GH/Components/Shared/SelvagenPropertiesAttributes.cs` | Face dropdown for module selection |
| `src/Selvagen.GH/Icons/Properties.png` | Icon for Properties component |

### Modified files (2)

| Path | Change |
|------|--------|
| `src/Selvagen.GH/Components/SelvagenModuleComponentBase.cs` | Add `subcategory` constructor param; name-based Upload lookup |
| `docs/PLUGIN_GUIDE.md` | Replace 4 mega-component field tables with 23 component tables + migration guide |

### Deleted files (4)

| Path | Reason |
|------|--------|
| `src/Selvagen.GH/Components/SelvagenTopographyComponent.cs` | Replaced by 8 Topo* components |
| `src/Selvagen.GH/Components/SelvagenGeologyComponent.cs` | Replaced by 5 Geo* components |
| `src/Selvagen.GH/Components/SelvagenAnalysesComponent.cs` | Replaced by 4 Anl* components |
| `src/Selvagen.GH/Components/SelvagenOptimizationsComponent.cs` | Replaced by 5 Opt* components |

---

## Task 1: Base Class Modifications

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenModuleComponentBase.cs`

- [ ] **Step 1: Add subcategory parameter and name-based Upload lookup**

Open `src/Selvagen.GH/Components/SelvagenModuleComponentBase.cs`. Make two changes:

1. Add a `subcategory` parameter to the constructor (with default `"Modules"` so existing components still compile):

```csharp
protected SelvagenModuleComponentBase(string name, string nickname, string description, string subcategory = "Modules")
    : base(name, nickname, description, "Selvagen", subcategory) { }
```

2. In `SolveInstance`, replace the positional Upload lookup (line 40):

```csharp
// OLD:
DA.GetData(Params.Input.Count - 1, ref upload);

// NEW:
int uploadIndex = Params.Input.Count - 1;
for (int i = Params.Input.Count - 1; i >= 0; i--)
{
    if (Params.Input[i].Name == "Upload") { uploadIndex = i; break; }
}
DA.GetData(uploadIndex, ref upload);
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`

Expected: Build succeeded. The default `"Modules"` parameter means existing mega-components compile without changes.

- [ ] **Step 3: Commit**

```
git add src/Selvagen.GH/Components/SelvagenModuleComponentBase.cs
git commit -m "refactor(base): add subcategory param and name-based Upload lookup"
```

---

## Task 2: Topography Components (8 files)

**Files:**
- Create: `src/Selvagen.GH/Components/Topography/TopoBaseComponent.cs`
- Create: `src/Selvagen.GH/Components/Topography/TopoContoursComponent.cs`
- Create: `src/Selvagen.GH/Components/Topography/TopoUrbanizationComponent.cs`
- Create: `src/Selvagen.GH/Components/Topography/TopoElevationComponent.cs`
- Create: `src/Selvagen.GH/Components/Topography/TopoSlopeComponent.cs`
- Create: `src/Selvagen.GH/Components/Topography/TopoAccess8Component.cs`
- Create: `src/Selvagen.GH/Components/Topography/TopoAccess5Component.cs`
- Create: `src/Selvagen.GH/Components/Topography/TopoDrainageComponent.cs`

- [ ] **Step 1: Create `TopoBaseComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoBaseComponent : SelvagenModuleComponentBase
    {
        public TopoBaseComponent()
            : base("Topography Base", "TpBs",
                   "Upload topography base data (mesh, areas, TDR)", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("BaseMeshID", "BM", "Base mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("BaseArea2D", "BA2", "Base 2D area", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("BaseArea3D", "BA3", "Base 3D area", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("BaseTDR", "BTDR", "Base true dimension rate", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var baseMeshId))
                values["base_mesh_id"] = baseMeshId;
            if (TryGetNumber(DA, 2, out var baseArea2d))
                values["base_area_2d"] = baseArea2d;
            if (TryGetNumber(DA, 3, out var baseArea3d))
                values["base_area_3d"] = baseArea3d;
            if (TryGetNumber(DA, 4, out var baseTdr))
                values["base_true_dimension_rate"] = baseTdr;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoBase");
    }
}
```

- [ ] **Step 2: Create `TopoContoursComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoContoursComponent : SelvagenModuleComponentBase
    {
        public TopoContoursComponent()
            : base("Topography Contours", "TpCn",
                   "Upload topography contour data", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("OutlineCurvesID", "OC", "Outline curve set asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("ContoursCurvesID", "CC", "Contours curve set asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddTextParameter("ContoursLabelsID", "CL", "Contours text 3D set asset ID", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("ContourInterval", "CI", "Contour interval", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var outlineCurvesId))
                values["outline_curve_set_id"] = outlineCurvesId;
            if (TryGetText(DA, 2, out var contoursCurvesId))
                values["contours_curve_set_id"] = contoursCurvesId;
            if (TryGetText(DA, 3, out var contoursLabelsId))
                values["contours_text_3d_set_id"] = contoursLabelsId;
            if (TryGetNumber(DA, 4, out var contourInterval))
                values["contour_interval"] = contourInterval;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoContours");
    }
}
```

- [ ] **Step 3: Create `TopoUrbanizationComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoUrbanizationComponent : SelvagenModuleComponentBase
    {
        public TopoUrbanizationComponent()
            : base("Topography Urbanization", "TpUr",
                   "Upload topography urbanization data", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("UrbanCurvesID", "UC", "Urbanization curve set asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var urbanCurvesId))
                values["urbanization_curve_set_id"] = urbanCurvesId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoUrbanization");
    }
}
```

- [ ] **Step 4: Create `TopoElevationComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoElevationComponent : SelvagenModuleComponentBase
    {
        public TopoElevationComponent()
            : base("Topography Elevation", "TpEl",
                   "Upload topography elevation data", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("ElevMeshID", "EM", "Elevation mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("ElevCurvesID", "EC", "Elevation curve set asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("ElevMin", "Emn", "Minimum elevation", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("ElevMax", "Emx", "Maximum elevation", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var elevMeshId))
                values["elevation_mesh_id"] = elevMeshId;
            if (TryGetText(DA, 2, out var elevCurvesId))
                values["elevation_curve_set_id"] = elevCurvesId;
            if (TryGetNumber(DA, 3, out var elevMin))
                values["elevation_min"] = elevMin;
            if (TryGetNumber(DA, 4, out var elevMax))
                values["elevation_max"] = elevMax;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoElevation");
    }
}
```

- [ ] **Step 5: Create `TopoSlopeComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoSlopeComponent : SelvagenModuleComponentBase
    {
        public TopoSlopeComponent()
            : base("Topography Slope", "TpSl",
                   "Upload topography slope data", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000005");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("SlopeMeshID", "SM", "Slope mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("SlopeRef", "SR", "Slope reference value", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("SlopeRestRate", "SRR", "Slope restricted area rate", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("SlopeMin", "Smn", "Minimum slope", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("SlopeMax", "Smx", "Maximum slope", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var slopeMeshId))
                values["slope_mesh_id"] = slopeMeshId;
            if (TryGetNumber(DA, 2, out var slopeRef))
                values["slope_ref"] = slopeRef;
            if (TryGetNumber(DA, 3, out var slopeRestRate))
                values["slope_restricted_area_rate"] = slopeRestRate;
            if (TryGetNumber(DA, 4, out var slopeMin))
                values["slope_min"] = slopeMin;
            if (TryGetNumber(DA, 5, out var slopeMax))
                values["slope_max"] = slopeMax;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoSlope");
    }
}
```

- [ ] **Step 6: Create `TopoAccess8Component.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoAccess8Component : SelvagenModuleComponentBase
    {
        public TopoAccess8Component()
            : base("Topography Access 8", "TpA8",
                   "Upload topography access 8m data", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000006");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("Acc8MeshID", "A8M", "Access 8m mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Acc8Ref", "A8R", "Access 8m reference value", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Acc8Rate", "A8%", "Access 8m rate", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var acc8MeshId))
                values["access8_mesh_id"] = acc8MeshId;
            if (TryGetNumber(DA, 2, out var acc8Ref))
                values["access8_ref"] = acc8Ref;
            if (TryGetNumber(DA, 3, out var acc8Rate))
                values["access8_rate"] = acc8Rate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoAccess8");
    }
}
```

- [ ] **Step 7: Create `TopoAccess5Component.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoAccess5Component : SelvagenModuleComponentBase
    {
        public TopoAccess5Component()
            : base("Topography Access 5", "TpA5",
                   "Upload topography access 5m data", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000007");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("Acc5MeshID", "A5M", "Access 5m mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Acc5Ref", "A5R", "Access 5m reference value", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Acc5Rate", "A5%", "Access 5m rate", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var acc5MeshId))
                values["access5_mesh_id"] = acc5MeshId;
            if (TryGetNumber(DA, 2, out var acc5Ref))
                values["access5_ref"] = acc5Ref;
            if (TryGetNumber(DA, 3, out var acc5Rate))
                values["access5_rate"] = acc5Rate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoAccess5");
    }
}
```

- [ ] **Step 8: Create `TopoDrainageComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoDrainageComponent : SelvagenModuleComponentBase
    {
        public TopoDrainageComponent()
            : base("Topography Drainage", "TpDr",
                   "Upload topography drainage data", "Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000008");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("DrainCurvesID", "DC", "Drainage curve set asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("DrainFlowPaths", "DFP", "Total drainage flow paths", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("DrainConcRate", "DCR", "Drainage concentration rate", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var drainCurvesId))
                values["drainage_curve_set_id"] = drainCurvesId;
            if (TryGetInt(DA, 2, out var drainFlowPaths))
                values["drainage_total_flow_paths"] = drainFlowPaths;
            if (TryGetNumber(DA, 3, out var drainConcRate))
                values["drainage_concentration_rate"] = drainConcRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoDrainage");
    }
}
```

- [ ] **Step 9: Build to verify all 8 Topography components compile**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`

Expected: Build succeeded. All 8 files picked up automatically by SDK-style project (no csproj edits needed).

- [ ] **Step 10: Commit**

```
git add src/Selvagen.GH/Components/Topography/
git commit -m "feat(topo): add 8 composable Topography group components"
```

---

## Task 3: Geology Components (5 files)

**Files:**
- Create: `src/Selvagen.GH/Components/Geology/GeoCoverageComponent.cs`
- Create: `src/Selvagen.GH/Components/Geology/GeoRockComponent.cs`
- Create: `src/Selvagen.GH/Components/Geology/GeoRippabilityComponent.cs`
- Create: `src/Selvagen.GH/Components/Geology/GeoSoilComponent.cs`
- Create: `src/Selvagen.GH/Components/Geology/GeoDepthComponent.cs`

- [ ] **Step 1: Create `GeoCoverageComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoCoverageComponent : SelvagenModuleComponentBase
    {
        public GeoCoverageComponent()
            : base("Geology Coverage", "GeoCv",
                   "Upload geology coverage data", "Geology") { }

        protected override string ModuleTable => "geology";

        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("CovMeshID", "CovM", "Coverage mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("CovNumPoints", "CovNP", "Number of coverage points", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("CovArea", "CovA", "Coverage area", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("CovRate", "CovR", "Coverage rate", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var covMeshId))
                values["coverage_mesh_id"] = covMeshId;
            if (TryGetInt(DA, 2, out var covNumPoints))
                values["coverage_number_points"] = covNumPoints;
            if (TryGetNumber(DA, 3, out var covArea))
                values["coverage_area"] = covArea;
            if (TryGetNumber(DA, 4, out var covRate))
                values["coverage_rate"] = covRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoCoverage");
    }
}
```

- [ ] **Step 2: Create `GeoRockComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoRockComponent : SelvagenModuleComponentBase
    {
        public GeoRockComponent()
            : base("Geology Rock", "GeoRk",
                   "Upload geology rock data", "Geology") { }

        protected override string ModuleTable => "geology";

        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("RockMeshID", "RkM", "Rock mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("RockCurvesID", "RkC", "Rock curve set asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("RockContourInt", "RkCI", "Rock contour interval", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var rockMeshId))
                values["rock_mesh_id"] = rockMeshId;
            if (TryGetText(DA, 2, out var rockCurvesId))
                values["rock_curve_set_id"] = rockCurvesId;
            if (TryGetNumber(DA, 3, out var rockContourInt))
                values["rock_contour_interval"] = rockContourInt;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoRock");
    }
}
```

- [ ] **Step 3: Create `GeoRippabilityComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoRippabilityComponent : SelvagenModuleComponentBase
    {
        public GeoRippabilityComponent()
            : base("Geology Rippability", "GeoRp",
                   "Upload geology rippability data", "Geology") { }

        protected override string ModuleTable => "geology";

        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("RipMeshID", "RipM", "Rippability mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var ripMeshId))
                values["rippability_mesh_id"] = ripMeshId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoRippability");
    }
}
```

- [ ] **Step 4: Create `GeoSoilComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoSoilComponent : SelvagenModuleComponentBase
    {
        public GeoSoilComponent()
            : base("Geology Soil", "GeoSl",
                   "Upload geology soil data", "Geology") { }

        protected override string ModuleTable => "geology";

        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("SoilMeshID", "SoilM", "Soil mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("SoilHMin", "SHMin", "Soil minimum height", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("SoilHMax", "SHMax", "Soil maximum height", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var soilMeshId))
                values["soil_mesh_id"] = soilMeshId;
            if (TryGetNumber(DA, 2, out var soilHMin))
                values["soil_height_min"] = soilHMin;
            if (TryGetNumber(DA, 3, out var soilHMax))
                values["soil_height_max"] = soilHMax;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoSoil");
    }
}
```

- [ ] **Step 5: Create `GeoDepthComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoDepthComponent : SelvagenModuleComponentBase
    {
        public GeoDepthComponent()
            : base("Geology Depth", "GeoDp",
                   "Upload geology depth data", "Geology") { }

        protected override string ModuleTable => "geology";

        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000005");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("DepthMeshID", "DepM", "Depth mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("DepthRef", "DepR", "Depth reference value", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("DepthUsRate", "DepUR", "Depth usability rate", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var depthMeshId))
                values["depth_mesh_id"] = depthMeshId;
            if (TryGetNumber(DA, 2, out var depthRef))
                values["depth_ref"] = depthRef;
            if (TryGetNumber(DA, 3, out var depthUsRate))
                values["depth_usability_rate"] = depthUsRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoDepth");
    }
}
```

- [ ] **Step 6: Build**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`

Expected: Build succeeded.

- [ ] **Step 7: Commit**

```
git add src/Selvagen.GH/Components/Geology/
git commit -m "feat(geo): add 5 composable Geology group components"
```

---

## Task 4: Analyses Components (4 files)

**Files:**
- Create: `src/Selvagen.GH/Components/Analyses/AnlEarthworksComponent.cs`
- Create: `src/Selvagen.GH/Components/Analyses/AnlRetentionComponent.cs`
- Create: `src/Selvagen.GH/Components/Analyses/AnlRockComponent.cs`
- Create: `src/Selvagen.GH/Components/Analyses/AnlAccessComponent.cs`

- [ ] **Step 1: Create `AnlEarthworksComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlEarthworksComponent : SelvagenModuleComponentBase
    {
        public AnlEarthworksComponent()
            : base("Analyses Earthworks", "AnlEw",
                   "Upload analyses earthworks data", "Analyses") { }

        protected override string ModuleTable => "analyses";

        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("EarthTerrainMeshID", "ETM", "Terrain mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("EarthMassingMeshID", "EMM", "Massing mesh asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("EarthVolFill", "EVF", "Earth volume fill (m³)", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("EarthVolCut", "EVC", "Earth volume cut (m³)", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("EarthVolImport", "EVI", "Earth volume import (m³)", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("EarthVolExport", "EVE", "Earth volume export (m³)", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("EarthCostImport", "ECI", "Earth import cost", GH_ParamAccess.item);
            pManager[7].Optional = true;
            pManager.AddNumberParameter("EarthCostExport", "ECE", "Earth export cost", GH_ParamAccess.item);
            pManager[8].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var earthTerrain))
                values["earth_mesh_terrain_id"] = earthTerrain;
            if (TryGetText(DA, 2, out var earthMassing))
                values["earth_mesh_massing_id"] = earthMassing;
            if (TryGetNumber(DA, 3, out var evf))
                values["earth_vol_fill"] = evf;
            if (TryGetNumber(DA, 4, out var evc))
                values["earth_vol_cut"] = evc;
            if (TryGetNumber(DA, 5, out var evi))
                values["earth_vol_import"] = evi;
            if (TryGetNumber(DA, 6, out var eve))
                values["earth_vol_export"] = eve;
            if (TryGetNumber(DA, 7, out var eci))
                values["earth_cost_import"] = eci;
            if (TryGetNumber(DA, 8, out var ece))
                values["earth_cost_export"] = ece;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlEarthworks");
    }
}
```

- [ ] **Step 2: Create `AnlRetentionComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlRetentionComponent : SelvagenModuleComponentBase
    {
        public AnlRetentionComponent()
            : base("Analyses Retention", "AnlRt",
                   "Upload analyses retention wall data", "Analyses") { }

        protected override string ModuleTable => "analyses";

        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddNumberParameter("RetHMin", "RHn", "Retention wall minimum height (m)", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("RetHMax", "RHx", "Retention wall maximum height (m)", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("RetArea", "RA", "Retention wall area (m²)", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("RetCost", "RC", "Retention wall cost", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetNumber(DA, 1, out var retHMin))
                values["retention_height_min"] = retHMin;
            if (TryGetNumber(DA, 2, out var retHMax))
                values["retention_height_max"] = retHMax;
            if (TryGetNumber(DA, 3, out var retArea))
                values["retention_area"] = retArea;
            if (TryGetNumber(DA, 4, out var retCost))
                values["retention_cost"] = retCost;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlRetention");
    }
}
```

- [ ] **Step 3: Create `AnlRockComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlRockComponent : SelvagenModuleComponentBase
    {
        public AnlRockComponent()
            : base("Analyses Rock", "AnlRk",
                   "Upload analyses rock data", "Analyses") { }

        protected override string ModuleTable => "analyses";

        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("RockMeshID", "RM", "Rock mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("RockLabelsHID", "RLH", "Rock height labels asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddTextParameter("RockLabelsVID", "RLV", "Rock volume labels asset ID", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("RockHMin", "RkHn", "Rock minimum height (m)", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("RockHMax", "RkHx", "Rock maximum height (m)", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("RockTotalVolCut", "RTV", "Rock total volume cut (m³)", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var rockMesh))
                values["rock_mesh_id"] = rockMesh;
            if (TryGetText(DA, 2, out var rockLabelsH))
                values["rock_text_3d_set_height_id"] = rockLabelsH;
            if (TryGetText(DA, 3, out var rockLabelsV))
                values["rock_text_3d_set_vol_id"] = rockLabelsV;
            if (TryGetNumber(DA, 4, out var rockHMin))
                values["rock_height_min"] = rockHMin;
            if (TryGetNumber(DA, 5, out var rockHMax))
                values["rock_height_max"] = rockHMax;
            if (TryGetNumber(DA, 6, out var rockTotalVol))
                values["rock_total_vol_cut"] = rockTotalVol;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlRock");
    }
}
```

- [ ] **Step 4: Create `AnlAccessComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlAccessComponent : SelvagenModuleComponentBase
    {
        public AnlAccessComponent()
            : base("Analyses Access", "AnlAc",
                   "Upload analyses access data", "Analyses") { }

        protected override string ModuleTable => "analyses";

        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("AccCurvesID", "AC", "Access curves asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("AccLabelsID", "AL", "Access labels asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("AccRef", "AR", "Access reference value", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("AccRate", "ARt", "Access rate", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var accCurves))
                values["access_curve_set_id"] = accCurves;
            if (TryGetText(DA, 2, out var accLabels))
                values["access_text_3d_set_id"] = accLabels;
            if (TryGetNumber(DA, 3, out var accRef))
                values["access_ref"] = accRef;
            if (TryGetNumber(DA, 4, out var accRate))
                values["access_rate"] = accRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlAccess");
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```
git add src/Selvagen.GH/Components/Analyses/
git commit -m "feat(anl): add 4 composable Analyses group components"
```

---

## Task 5: Optimizations Components (5 files)

**Files:**
- Create: `src/Selvagen.GH/Components/Optimizations/OptAccessComponent.cs`
- Create: `src/Selvagen.GH/Components/Optimizations/OptEarthTerrainComponent.cs`
- Create: `src/Selvagen.GH/Components/Optimizations/OptEarthLotsComponent.cs`
- Create: `src/Selvagen.GH/Components/Optimizations/OptEarthTotalComponent.cs`
- Create: `src/Selvagen.GH/Components/Optimizations/OptRetentionComponent.cs`

- [ ] **Step 1: Create `OptAccessComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptAccessComponent : SelvagenModuleComponentBase
    {
        public OptAccessComponent()
            : base("Optimizations Access", "OptAc",
                   "Upload optimizations access data", "Optimizations") { }

        protected override string ModuleTable => "optimizations";

        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("AccCurvesID", "AC", "Access curves asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("AccLabelsID", "AL", "Access labels asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("AccRef", "AR", "Access reference value", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("AccRate", "ARt", "Access rate", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var accCurves))
                values["access_curve_set_id"] = accCurves;
            if (TryGetText(DA, 2, out var accLabels))
                values["access_text_3d_set_id"] = accLabels;
            if (TryGetNumber(DA, 3, out var accRef))
                values["access_ref"] = accRef;
            if (TryGetNumber(DA, 4, out var accRate))
                values["access_rate"] = accRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptAccess");
    }
}
```

- [ ] **Step 2: Create `OptEarthTerrainComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptEarthTerrainComponent : SelvagenModuleComponentBase
    {
        public OptEarthTerrainComponent()
            : base("Optimizations Earth Terrain", "OptET",
                   "Upload optimizations earth terrain data", "Optimizations") { }

        protected override string ModuleTable => "optimizations";

        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("TerrMeshID", "TerrM", "Terrain mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("TerrVolCompFill", "TerrCF", "Terrain volume compact fill", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("TerrVolBulkFill", "TerrBF", "Terrain volume bulking fill", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("TerrVolCut", "TerrCt", "Terrain volume cut", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("TerrVolImport", "TerrIm", "Terrain volume import", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("TerrVolExport", "TerrEx", "Terrain volume export", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var terrMeshId))
                values["earth_mesh_terrain_id"] = terrMeshId;
            if (TryGetNumber(DA, 2, out var terrVolCompFill))
                values["earth_terrain_vol_compact_fill"] = terrVolCompFill;
            if (TryGetNumber(DA, 3, out var terrVolBulkFill))
                values["earth_terrain_vol_bulking_fill"] = terrVolBulkFill;
            if (TryGetNumber(DA, 4, out var terrVolCut))
                values["earth_terrain_vol_cut"] = terrVolCut;
            if (TryGetNumber(DA, 5, out var terrVolImport))
                values["earth_terrain_vol_import"] = terrVolImport;
            if (TryGetNumber(DA, 6, out var terrVolExport))
                values["earth_terrain_vol_export"] = terrVolExport;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptEarthTerrain");
    }
}
```

- [ ] **Step 3: Create `OptEarthLotsComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptEarthLotsComponent : SelvagenModuleComponentBase
    {
        public OptEarthLotsComponent()
            : base("Optimizations Earth Lots", "OptEL",
                   "Upload optimizations earth lots data", "Optimizations") { }

        protected override string ModuleTable => "optimizations";

        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("LotsMeshID", "LotsM", "Lots mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("LotsVolCompFill", "LotsCF", "Lots volume compact fill", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("LotsVolBulkFill", "LotsBF", "Lots volume bulking fill", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("LotsVolCut", "LotsCt", "Lots volume cut", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("LotsVolImport", "LotsIm", "Lots volume import", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("LotsVolExport", "LotsEx", "Lots volume export", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var lotsMeshId))
                values["earth_mesh_lots_id"] = lotsMeshId;
            if (TryGetNumber(DA, 2, out var lotsVolCompFill))
                values["earth_lots_vol_compact_fill"] = lotsVolCompFill;
            if (TryGetNumber(DA, 3, out var lotsVolBulkFill))
                values["earth_lots_vol_bulking_fill"] = lotsVolBulkFill;
            if (TryGetNumber(DA, 4, out var lotsVolCut))
                values["earth_lots_vol_cut"] = lotsVolCut;
            if (TryGetNumber(DA, 5, out var lotsVolImport))
                values["earth_lots_vol_import"] = lotsVolImport;
            if (TryGetNumber(DA, 6, out var lotsVolExport))
                values["earth_lots_vol_export"] = lotsVolExport;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptEarthLots");
    }
}
```

- [ ] **Step 4: Create `OptEarthTotalComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptEarthTotalComponent : SelvagenModuleComponentBase
    {
        public OptEarthTotalComponent()
            : base("Optimizations Earth Total", "OptETt",
                   "Upload optimizations earth total data", "Optimizations") { }

        protected override string ModuleTable => "optimizations";

        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddNumberParameter("TotalVolCompFill", "TotCF", "Total volume compact fill", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("TotalVolBulkFill", "TotBF", "Total volume bulking fill", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("TotalVolCut", "TotCt", "Total volume cut", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("TotalVolImport", "TotIm", "Total volume import", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("TotalVolExport", "TotEx", "Total volume export", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("TotalCostImport", "TotCIm", "Total cost import", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("TotalCostExport", "TotCEx", "Total cost export", GH_ParamAccess.item);
            pManager[7].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetNumber(DA, 1, out var totalVolCompFill))
                values["earth_total_vol_compact_fill"] = totalVolCompFill;
            if (TryGetNumber(DA, 2, out var totalVolBulkFill))
                values["earth_total_vol_bulking_fill"] = totalVolBulkFill;
            if (TryGetNumber(DA, 3, out var totalVolCut))
                values["earth_total_vol_cut"] = totalVolCut;
            if (TryGetNumber(DA, 4, out var totalVolImport))
                values["earth_total_vol_import"] = totalVolImport;
            if (TryGetNumber(DA, 5, out var totalVolExport))
                values["earth_total_vol_export"] = totalVolExport;
            if (TryGetNumber(DA, 6, out var totalCostImport))
                values["earth_total_cost_import"] = totalCostImport;
            if (TryGetNumber(DA, 7, out var totalCostExport))
                values["earth_total_cost_export"] = totalCostExport;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptEarthTotal");
    }
}
```

- [ ] **Step 5: Create `OptRetentionComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptRetentionComponent : SelvagenModuleComponentBase
    {
        public OptRetentionComponent()
            : base("Optimizations Retention", "OptRt",
                   "Upload optimizations retention wall data", "Optimizations") { }

        protected override string ModuleTable => "optimizations";

        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000005");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddNumberParameter("RetHMin", "RetMin", "Retention height min", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("RetHMax", "RetMax", "Retention height max", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("RetArea", "RetA", "Retention area", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("RetCost", "RetC", "Retention cost", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetNumber(DA, 1, out var retHMin))
                values["retention_height_min"] = retHMin;
            if (TryGetNumber(DA, 2, out var retHMax))
                values["retention_height_max"] = retHMax;
            if (TryGetNumber(DA, 3, out var retArea))
                values["retention_area"] = retArea;
            if (TryGetNumber(DA, 4, out var retCost))
                values["retention_cost"] = retCost;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptRetention");
    }
}
```

- [ ] **Step 6: Build**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`

Expected: Build succeeded.

- [ ] **Step 7: Commit**

```
git add src/Selvagen.GH/Components/Optimizations/
git commit -m "feat(opt): add 5 composable Optimizations group components"
```

---

## Task 6: Properties Component + Dropdown Attributes

**Files:**
- Create: `src/Selvagen.GH/Components/Shared/SelvagenPropertiesComponent.cs`
- Create: `src/Selvagen.GH/Components/Shared/SelvagenPropertiesAttributes.cs`

The Properties component is different from the other 22: it targets any module via a face dropdown selector. The module selection is rendered on the component face (not an input parameter), persisted with the document, and also available via right-click menu for Cordyceps automation.

- [ ] **Step 1: Create `SelvagenPropertiesComponent.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Grasshopper.Kernel;
using GH_IO.Serialization;

namespace Selvagen.GH.Components
{
    public class SelvagenPropertiesComponent : SelvagenModuleComponentBase
    {
        internal static readonly string[] ModuleOptions = { "topography", "geology", "analyses", "optimizations" };
        internal static readonly string[] ModuleDisplayNames = { "Topography", "Geology", "Analyses", "Optimizations" };

        private string _selectedModule = "topography";

        public SelvagenPropertiesComponent()
            : base("Properties", "SvProps",
                   "Upload custom JSON properties to any module", "Shared") { }

        protected override string ModuleTable => _selectedModule;

        public override Guid ComponentGuid => new Guid("A1000005-0001-4000-8000-000000000001");

        public string SelectedModule
        {
            get => _selectedModule;
            set
            {
                if (_selectedModule != value && ModuleOptions.Contains(value))
                {
                    _selectedModule = value;
                    Message = ModuleDisplayNames[Array.IndexOf(ModuleOptions, value)];
                    ExpireSolution(true);
                }
            }
        }

        public override void CreateAttributes()
        {
            m_attributes = new SelvagenPropertiesAttributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("JSON", "J", "Custom properties as JSON string", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetJson(DA, 1, out var props))
                values["properties"] = props;
            return values;
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("SelectedModule", _selectedModule);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SelectedModule"))
                _selectedModule = reader.GetString("SelectedModule");
            Message = ModuleDisplayNames[Array.IndexOf(ModuleOptions, _selectedModule)];
            return base.Read(reader);
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);
            for (int i = 0; i < ModuleOptions.Length; i++)
            {
                var option = ModuleOptions[i];
                var display = ModuleDisplayNames[i];
                Menu_AppendItem(menu, display, (s, e) => SelectedModule = option, true, _selectedModule == option);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Properties");
    }
}
```

- [ ] **Step 2: Create `SelvagenPropertiesAttributes.cs`**

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Selvagen.GH.Components
{
    public class SelvagenPropertiesAttributes : GH_ComponentAttributes
    {
        private const int DropdownHeight = 22;
        private RectangleF _dropdownBounds;

        public SelvagenPropertiesAttributes(SelvagenPropertiesComponent owner) : base(owner) { }

        private SelvagenPropertiesComponent PropertiesOwner => (SelvagenPropertiesComponent)Owner;

        protected override void Layout()
        {
            base.Layout();
            var bounds = Bounds;
            _dropdownBounds = new RectangleF(
                bounds.X + 2,
                bounds.Bottom,
                bounds.Width - 4,
                DropdownHeight - 2);
            bounds.Height += DropdownHeight;
            Bounds = bounds;
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;

            var selectedIndex = Array.IndexOf(
                SelvagenPropertiesComponent.ModuleOptions,
                PropertiesOwner.SelectedModule);
            var displayName = selectedIndex >= 0
                ? SelvagenPropertiesComponent.ModuleDisplayNames[selectedIndex]
                : PropertiesOwner.SelectedModule;

            using (var fill = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (var border = new Pen(Color.FromArgb(160, 160, 160)))
            using (var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                graphics.FillRectangle(fill, _dropdownBounds);
                graphics.DrawRectangle(border,
                    _dropdownBounds.X, _dropdownBounds.Y,
                    _dropdownBounds.Width, _dropdownBounds.Height);

                var textRect = new RectangleF(
                    _dropdownBounds.X + 4, _dropdownBounds.Y,
                    _dropdownBounds.Width - 20, _dropdownBounds.Height);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                graphics.DrawString(displayName, GH_FontServer.Standard, textBrush, textRect, sf);
                graphics.DrawString("▼", GH_FontServer.Small, textBrush,
                    _dropdownBounds.Right - 16, _dropdownBounds.Y + 4);
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && _dropdownBounds.Contains(e.CanvasLocation))
            {
                var menu = new ToolStripDropDown();
                for (int i = 0; i < SelvagenPropertiesComponent.ModuleOptions.Length; i++)
                {
                    var option = SelvagenPropertiesComponent.ModuleOptions[i];
                    var display = SelvagenPropertiesComponent.ModuleDisplayNames[i];
                    var isSelected = PropertiesOwner.SelectedModule == option;
                    var item = new ToolStripMenuItem(display) { Checked = isSelected, Tag = option };
                    item.Click += (s, args) =>
                    {
                        PropertiesOwner.SelectedModule = ((ToolStripMenuItem)s).Tag.ToString();
                        sender.Refresh();
                    };
                    menu.Items.Add(item);
                }
                menu.Show(sender, sender.PointToClient(Cursor.Position));
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`

Expected: Build succeeded. The Properties component compiles with its custom attributes class.

- [ ] **Step 4: Commit**

```
git add src/Selvagen.GH/Components/Shared/
git commit -m "feat(shared): add Properties component with module dropdown selector"
```

---

## Task 7: Delete Old Mega-Components

**Files:**
- Delete: `src/Selvagen.GH/Components/SelvagenTopographyComponent.cs`
- Delete: `src/Selvagen.GH/Components/SelvagenGeologyComponent.cs`
- Delete: `src/Selvagen.GH/Components/SelvagenAnalysesComponent.cs`
- Delete: `src/Selvagen.GH/Components/SelvagenOptimizationsComponent.cs`

- [ ] **Step 1: Delete the 4 old component files**

```powershell
Remove-Item src/Selvagen.GH/Components/SelvagenTopographyComponent.cs
Remove-Item src/Selvagen.GH/Components/SelvagenGeologyComponent.cs
Remove-Item src/Selvagen.GH/Components/SelvagenAnalysesComponent.cs
Remove-Item src/Selvagen.GH/Components/SelvagenOptimizationsComponent.cs
```

- [ ] **Step 2: Build to verify nothing depends on deleted files**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`

Expected: Build succeeded. No other file references the deleted classes.

- [ ] **Step 3: Commit**

```
git add -u src/Selvagen.GH/Components/
git commit -m "refactor: delete 4 old mega-components (replaced by 23 group components)"
```

---

## Task 8: Update PLUGIN_GUIDE.md

**Files:**
- Modify: `docs/PLUGIN_GUIDE.md` (lines 234–357: replace field tables with new component tables + migration note)

- [ ] **Step 1: Replace the module component documentation**

Replace the existing 4 module component tables (Topography, Geology, Analyses, Optimizations sections between lines ~234–357) with the following content:

```markdown
### Module Components (Composable Groups)

Each module is composed of small, focused components. Every component has `ProjectID` (required) and `Upload` (boolean trigger) as bookend inputs, with group-specific optional data fields in between. Multiple components targeting the same module independently PATCH the same database row.

#### Topography (8 components — ribbon group: Topography)

| Component | Inputs (besides ProjectID & Upload) | DB Columns |
|---|---|---|
| Topography Base | BaseMeshID, BaseArea2D, BaseArea3D, BaseTDR | `base_mesh_id`, `base_area_2d`, `base_area_3d`, `base_true_dimension_rate` |
| Topography Contours | OutlineCurvesID, ContoursCurvesID, ContoursLabelsID, ContourInterval | `outline_curve_set_id`, `contours_curve_set_id`, `contours_text_3d_set_id`, `contour_interval` |
| Topography Urbanization | UrbanCurvesID | `urbanization_curve_set_id` |
| Topography Elevation | ElevMeshID, ElevCurvesID, ElevMin, ElevMax | `elevation_mesh_id`, `elevation_curve_set_id`, `elevation_min`, `elevation_max` |
| Topography Slope | SlopeMeshID, SlopeRef, SlopeRestRate, SlopeMin, SlopeMax | `slope_mesh_id`, `slope_ref`, `slope_restricted_area_rate`, `slope_min`, `slope_max` |
| Topography Access 8 | Acc8MeshID, Acc8Ref, Acc8Rate | `access8_mesh_id`, `access8_ref`, `access8_rate` |
| Topography Access 5 | Acc5MeshID, Acc5Ref, Acc5Rate | `access5_mesh_id`, `access5_ref`, `access5_rate` |
| Topography Drainage | DrainCurvesID, DrainFlowPaths, DrainConcRate | `drainage_curve_set_id`, `drainage_total_flow_paths`, `drainage_concentration_rate` |

#### Geology (5 components — ribbon group: Geology)

| Component | Inputs (besides ProjectID & Upload) | DB Columns |
|---|---|---|
| Geology Coverage | CovMeshID, CovNumPoints, CovArea, CovRate | `coverage_mesh_id`, `coverage_number_points`, `coverage_area`, `coverage_rate` |
| Geology Rock | RockMeshID, RockCurvesID, RockContourInt | `rock_mesh_id`, `rock_curve_set_id`, `rock_contour_interval` |
| Geology Rippability | RipMeshID | `rippability_mesh_id` |
| Geology Soil | SoilMeshID, SoilHMin, SoilHMax | `soil_mesh_id`, `soil_height_min`, `soil_height_max` |
| Geology Depth | DepthMeshID, DepthRef, DepthUsRate | `depth_mesh_id`, `depth_ref`, `depth_usability_rate` |

#### Analyses (4 components — ribbon group: Analyses)

| Component | Inputs (besides ProjectID & Upload) | DB Columns |
|---|---|---|
| Analyses Earthworks | EarthTerrainMeshID, EarthMassingMeshID, EarthVolFill, EarthVolCut, EarthVolImport, EarthVolExport, EarthCostImport, EarthCostExport | `earth_mesh_terrain_id`, `earth_mesh_massing_id`, `earth_vol_fill`, `earth_vol_cut`, `earth_vol_import`, `earth_vol_export`, `earth_cost_import`, `earth_cost_export` |
| Analyses Retention | RetHMin, RetHMax, RetArea, RetCost | `retention_height_min`, `retention_height_max`, `retention_area`, `retention_cost` |
| Analyses Rock | RockMeshID, RockLabelsHID, RockLabelsVID, RockHMin, RockHMax, RockTotalVolCut | `rock_mesh_id`, `rock_text_3d_set_height_id`, `rock_text_3d_set_vol_id`, `rock_height_min`, `rock_height_max`, `rock_total_vol_cut` |
| Analyses Access | AccCurvesID, AccLabelsID, AccRef, AccRate | `access_curve_set_id`, `access_text_3d_set_id`, `access_ref`, `access_rate` |

#### Optimizations (5 components — ribbon group: Optimizations)

| Component | Inputs (besides ProjectID & Upload) | DB Columns |
|---|---|---|
| Optimizations Access | AccCurvesID, AccLabelsID, AccRef, AccRate | `access_curve_set_id`, `access_text_3d_set_id`, `access_ref`, `access_rate` |
| Optimizations Earth Terrain | TerrMeshID, TerrVolCompFill, TerrVolBulkFill, TerrVolCut, TerrVolImport, TerrVolExport | `earth_mesh_terrain_id`, `earth_terrain_vol_compact_fill`, `earth_terrain_vol_bulking_fill`, `earth_terrain_vol_cut`, `earth_terrain_vol_import`, `earth_terrain_vol_export` |
| Optimizations Earth Lots | LotsMeshID, LotsVolCompFill, LotsVolBulkFill, LotsVolCut, LotsVolImport, LotsVolExport | `earth_mesh_lots_id`, `earth_lots_vol_compact_fill`, `earth_lots_vol_bulking_fill`, `earth_lots_vol_cut`, `earth_lots_vol_import`, `earth_lots_vol_export` |
| Optimizations Earth Total | TotalVolCompFill, TotalVolBulkFill, TotalVolCut, TotalVolImport, TotalVolExport, TotalCostImport, TotalCostExport | `earth_total_vol_compact_fill`, `earth_total_vol_bulking_fill`, `earth_total_vol_cut`, `earth_total_vol_import`, `earth_total_vol_export`, `earth_total_cost_import`, `earth_total_cost_export` |
| Optimizations Retention | RetHMin, RetHMax, RetArea, RetCost | `retention_height_min`, `retention_height_max`, `retention_area`, `retention_cost` |

#### Properties (1 component — ribbon group: Shared)

| Component | Inputs (besides ProjectID & Upload) | DB Columns |
|---|---|---|
| Properties | JSON (text), Module (face dropdown: Topography/Geology/Analyses/Optimizations) | `properties` |

The Properties component targets whichever module is selected via the dropdown on its face. It PATCHes only the `properties` jsonb column.

### Migration from v1 Components

The previous single-component-per-module design (Topography, Geology, Analyses, Optimizations) has been removed. Any `.gh` file using the old components will show "missing component" errors.

**To migrate:** replace each old component with the group components you need. Wire the same `ProjectID` to each group component. Each group component independently creates/updates the module record.
```

- [ ] **Step 2: Commit**

```
git add docs/PLUGIN_GUIDE.md
git commit -m "docs: update PLUGIN_GUIDE with 23 composable module components"
```

---

## Task 9: Final Build Verification

- [ ] **Step 1: Clean build all targets**

Run: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj --configuration Release`

Expected: Build succeeded for all three target frameworks (net48, net8.0, net8.0-windows).

- [ ] **Step 2: Run existing tests**

Run: `dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj`

Expected: All existing tests pass (no Core changes were made).

- [ ] **Step 3: Verify component count**

Run (PowerShell): `(Get-ChildItem -Path src/Selvagen.GH/Components -Recurse -Filter "*Component.cs" | Where-Object { $_.Name -ne "SelvagenModuleComponentBase.cs" -and $_.Name -ne "SelvagenUploadComponentBase.cs" }).Count`

Expected: The count includes the 22 new module components + Properties + existing non-module components (Login, Clients, Projects, ListAssets, UploadMesh, UploadAnimation, UploadCurves, UploadLabels, DeleteAsset = 9). Total: 32 component files.

- [ ] **Step 4: Final commit (if any fixes were needed)**

If Steps 1–3 revealed issues and fixes were applied, commit them:

```
git add -A
git commit -m "fix: resolve build issues from module components redesign"
```

---

## Notes

### Icons
Each new component references an icon via `IconLoader.Load("ComponentName")` (e.g., `IconLoader.Load("TopoBase")`). The `IconLoader` returns `null` for missing files, so GH shows a default icon. Icon PNG files (24x24) should be created in `src/Selvagen.GH/Icons/` following David Rutten's guidelines (2 colors max, dark grey outlines, pixel-grid aligned, family cohesion within each module group). The `.csproj` already auto-embeds all PNGs via `<EmbeddedResource Include="Icons\*.png" />`.

### Old Icon Cleanup
The 4 old icon files (`Topography.png`, `Geology.png`, `Analyses.png`, `Optimizations.png`) can be removed after the new icons are created. They're harmless to keep temporarily since no component references them after the deletion in Task 7.

### Existing Tests
No new unit tests are introduced. The 22 group components are pure boilerplate (register inputs → collect into dictionary) with all logic in the unchanged base class. The Properties component's dropdown is UI-only state. Integration testing via Cordyceps (pytest) can be added as a follow-up once icons are designed and the components are loaded in Grasshopper.
