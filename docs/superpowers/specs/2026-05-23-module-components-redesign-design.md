# Module Components Redesign — Composable Group Components

**Date:** 2026-05-23
**Status:** Draft
**Scope:** Replace 4 mega-components (Topography, Geology, Analyses, Optimizations) with ~23 small, self-contained group components + 1 shared Properties component.

---

## Problem

The current module components have 17–30 input pegs each (~100 total across 4 components), most optional. On the Grasshopper canvas, users see a flat wall of inputs with no grouping or progressive disclosure. In any given project, users wire only a subset of fields — the rest are visual clutter.

Secondary issues:
- Field names are hardcoded as string literals with no shared schema (data dictionary deferred to a follow-up initiative).
- `access_*` (4 fields) and `retention_*` (4 fields) are duplicated verbatim between Analyses and Optimizations.
- Positional index coupling between `RegisterInputParams` and `CollectValues` is fragile.
- `Properties` (jsonb) is buried as one input among 28.

## Design

### Core principle: self-contained group components

Each logical group within a module (e.g., "Slope" within Topography) becomes its own GH component with:
- `ProjectID` (required text input)
- Group-specific data fields (1–8 inputs, all optional)
- `Upload` (boolean trigger, always last)

Each component independently PATCHes the module's DB row. Multiple components targeting the same module/project combination patch different columns, so there are no conflicts. Grasshopper solves components sequentially within a solution.

### Base class

`SelvagenModuleComponentBase` is reused as-is. Each new component overrides:
- `ModuleTable` — returns the Supabase table name (e.g., `"topography"`)
- `CollectValues(IGH_DataAccess DA)` — returns a `Dictionary<string, object>` with only its group's fields
- `ComponentGuid` — unique GUID per component
- `Icon` — unique icon per component

The existing create-or-PATCH flow (find row for `(module_table, project_id)`, create if missing, PATCH provided fields) handles everything without modification.

### Properties component

A single standalone `SelvagenPropertiesComponent` that works with any module:
- `ProjectID` (required text)
- `Module` (dropdown selector: Topography / Geology / Analyses / Optimizations) — follows the existing dropdown pattern from the Client/Project selectors (see `docs/superpowers/specs/2026-05-14-grasshopper-dropdown-selectors-design.md`)
- `JSON` (text input, parsed as JSON)
- `Upload` (boolean trigger)

PATCHes only the `properties` jsonb column on the selected module's table.

### Cross-module duplication

Analyses and Optimizations share identical field sets for `access_*` (4 fields) and `retention_*` (4 fields). These are kept as **separate component classes** for now (e.g., `AnlAccessComponent` and `OptAccessComponent`), each hardcoding its own `ModuleTable`. A code comment notes the duplication for future consolidation when a Data Dictionary is introduced.

---

## Component Inventory

### Topography (8 components → ribbon group "Topography")

| Component | Class Name | Fields | DB Columns |
|---|---|---|---|
| Topography Base | `TopoBaseComponent` | BaseMeshID, BaseArea2D, BaseArea3D, BaseTDR | `base_mesh_id`, `base_area_2d`, `base_area_3d`, `base_true_dimension_rate` |
| Topography Contours | `TopoContoursComponent` | OutlineCurvesID, ContoursCurvesID, ContoursLabelsID, ContourInterval | `outline_curve_set_id`, `contours_curve_set_id`, `contours_text_3d_set_id`, `contour_interval` |
| Topography Urbanization | `TopoUrbanizationComponent` | UrbanCurvesID | `urbanization_curve_set_id` |
| Topography Elevation | `TopoElevationComponent` | ElevMeshID, ElevCurvesID, ElevMin, ElevMax | `elevation_mesh_id`, `elevation_curve_set_id`, `elevation_min`, `elevation_max` |
| Topography Slope | `TopoSlopeComponent` | SlopeMeshID, SlopeRef, SlopeRestRate, SlopeMin, SlopeMax | `slope_mesh_id`, `slope_ref`, `slope_restricted_area_rate`, `slope_min`, `slope_max` |
| Topography Access 8 | `TopoAccess8Component` | Acc8MeshID, Acc8Ref, Acc8Rate | `access8_mesh_id`, `access8_ref`, `access8_rate` |
| Topography Access 5 | `TopoAccess5Component` | Acc5MeshID, Acc5Ref, Acc5Rate | `access5_mesh_id`, `access5_ref`, `access5_rate` |
| Topography Drainage | `TopoDrainageComponent` | DrainCurvesID, DrainFlowPaths, DrainConcRate | `drainage_curve_set_id`, `drainage_total_flow_paths`, `drainage_concentration_rate` |

### Geology (5 components → ribbon group "Geology")

| Component | Class Name | Fields | DB Columns |
|---|---|---|---|
| Geology Coverage | `GeoCoverageComponent` | CovMeshID, CovNumPoints, CovArea, CovRate | `coverage_mesh_id`, `coverage_number_points`, `coverage_area`, `coverage_rate` |
| Geology Rock | `GeoRockComponent` | RockMeshID, RockCurvesID, RockContourInt | `rock_mesh_id`, `rock_curve_set_id`, `rock_contour_interval` |
| Geology Rippability | `GeoRippabilityComponent` | RipMeshID | `rippability_mesh_id` |
| Geology Soil | `GeoSoilComponent` | SoilMeshID, SoilHMin, SoilHMax | `soil_mesh_id`, `soil_height_min`, `soil_height_max` |
| Geology Depth | `GeoDepthComponent` | DepthMeshID, DepthRef, DepthUsRate | `depth_mesh_id`, `depth_ref`, `depth_usability_rate` |

### Analyses (4 components → ribbon group "Analyses")

| Component | Class Name | Fields | DB Columns |
|---|---|---|---|
| Analyses Earthworks | `AnlEarthworksComponent` | EarthTerrainMeshID, EarthMassingMeshID, EarthVolFill, EarthVolCut, EarthVolImport, EarthVolExport, EarthCostImport, EarthCostExport | `earth_mesh_terrain_id`, `earth_mesh_massing_id`, `earth_vol_fill`, `earth_vol_cut`, `earth_vol_import`, `earth_vol_export`, `earth_cost_import`, `earth_cost_export` |
| Analyses Retention | `AnlRetentionComponent` | RetHMin, RetHMax, RetArea, RetCost | `retention_height_min`, `retention_height_max`, `retention_area`, `retention_cost` |
| Analyses Rock | `AnlRockComponent` | RockMeshID, RockLabelsHID, RockLabelsVID, RockHMin, RockHMax, RockTotalVolCut | `rock_mesh_id`, `rock_text_3d_set_height_id`, `rock_text_3d_set_vol_id`, `rock_height_min`, `rock_height_max`, `rock_total_vol_cut` |
| Analyses Access | `AnlAccessComponent` | AccCurvesID, AccLabelsID, AccRef, AccRate | `access_curve_set_id`, `access_text_3d_set_id`, `access_ref`, `access_rate` |

### Optimizations (5 components → ribbon group "Optimizations")

| Component | Class Name | Fields | DB Columns |
|---|---|---|---|
| Optimizations Access | `OptAccessComponent` | AccCurvesID, AccLabelsID, AccRef, AccRate | `access_curve_set_id`, `access_text_3d_set_id`, `access_ref`, `access_rate` |
| Optimizations Earth Terrain | `OptEarthTerrainComponent` | TerrMeshID, TerrVolCompFill, TerrVolBulkFill, TerrVolCut, TerrVolImport, TerrVolExport | `earth_mesh_terrain_id`, `earth_terrain_vol_compact_fill`, `earth_terrain_vol_bulking_fill`, `earth_terrain_vol_cut`, `earth_terrain_vol_import`, `earth_terrain_vol_export` |
| Optimizations Earth Lots | `OptEarthLotsComponent` | LotsMeshID, LotsVolCompFill, LotsVolBulkFill, LotsVolCut, LotsVolImport, LotsVolExport | `earth_mesh_lots_id`, `earth_lots_vol_compact_fill`, `earth_lots_vol_bulking_fill`, `earth_lots_vol_cut`, `earth_lots_vol_import`, `earth_lots_vol_export` |
| Optimizations Earth Total | `OptEarthTotalComponent` | TotalVolCompFill, TotalVolBulkFill, TotalVolCut, TotalVolImport, TotalVolExport, TotalCostImport, TotalCostExport | `earth_total_vol_compact_fill`, `earth_total_vol_bulking_fill`, `earth_total_vol_cut`, `earth_total_vol_import`, `earth_total_vol_export`, `earth_total_cost_import`, `earth_total_cost_export` |
| Optimizations Retention | `OptRetentionComponent` | RetHMin, RetHMax, RetArea, RetCost | `retention_height_min`, `retention_height_max`, `retention_area`, `retention_cost` |

### Shared (1 component → ribbon group "Shared")

| Component | Class Name | Fields | DB Columns |
|---|---|---|---|
| Properties | `SelvagenPropertiesComponent` | ProjectID, Module (dropdown), JSON, Upload | `properties` |

**Total: 23 components** (8 + 5 + 4 + 5 + 1)

---

## Ribbon Organization

Tab: **Selvagen**

| Group | Components | Changed? |
|---|---|---|
| Auth | Login | No |
| Data | Clients, Projects, List Assets, Delete Asset | No |
| Upload | Mesh, Curves, Labels, Animation | No |
| Topography | Topo Base, Topo Contours, Topo Urbanization, Topo Elevation, Topo Slope, Topo Access 8, Topo Access 5, Topo Drainage | **New** (replaces "Modules") |
| Geology | Geo Coverage, Geo Rock, Geo Rippability, Geo Soil, Geo Depth | **New** |
| Analyses | Anl Earthworks, Anl Retention, Anl Rock, Anl Access | **New** |
| Optimizations | Opt Access, Opt Earth Terrain, Opt Earth Lots, Opt Earth Total, Opt Retention | **New** |
| Shared | Properties | **New** |

All new module sub-components use `GH_Exposure.secondary`.

---

## Naming Convention

Each component registers with a **full descriptive name** as the primary name (e.g., "Topography Base", "Geology Coverage", "Analyses Earthworks"). Grasshopper's built-in display toggle lets users switch to short names or icon-only view.

Short nicknames for wiring: follow the existing codebase pattern (2–4 character abbreviations like "BM", "BA2", "SM").

---

## Icon Design

Each component gets a unique 24x24 icon following David Rutten's guidelines ([ieatbugsforbreakfast.com/2012/07/12/on-icons/](https://ieatbugsforbreakfast.com/2012/07/12/on-icons/)):

- **2 colors max** per icon
- **Dark grey outlines**, not black
- **No near-vertical/near-horizontal lines** (anti-aliasing issues)
- **Pixel-grid aligned** — create images at exact target size
- **Faint drop shadows** for depth; subtle gradients on large areas
- **Functional, not decorative** — icons are navigation cues

**Family cohesion:** All icons within a module share a visual theme (e.g., Topography icons share a terrain silhouette base shape). Each group icon adds a small meaningful differentiator (contour lines for Contours, angle marker for Slope, water drop for Drainage, etc.). Icons must be distinguishable in peripheral vision.

Properties component gets a distinct icon (purple-toned) to signal its cross-module nature.

---

## Outputs

Every component outputs:
- `RecordID` (text) — the module record UUID
- `Status` (text) — operation result (e.g., "Updated: topography (5 fields)")

Unchanged from current behavior.

---

## Breaking Changes

The 4 old mega-components (`SelvagenTopographyComponent`, `SelvagenGeologyComponent`, `SelvagenAnalysesComponent`, `SelvagenOptimizationsComponent`) are **deleted**. Any `.gh` file referencing them will show "missing component" errors.

**Mitigation:**
- The sample `grasshopper-sample/Selvagen_test.gh` does not use any module components — no update needed.
- `docs/PLUGIN_GUIDE.md` field tables (lines 234–357) will be updated with new component names and a migration guide.
- Version bump to signal the breaking change.

---

## Deferred

- **Data Dictionary:** A single source of truth for field names, types, and grouping in `Selvagen.Core`. When built, it would eliminate hardcoded string literals, enable merging duplicated groups, and potentially support runtime schema discovery from Supabase. Separate initiative.
- **Cross-component deduplication:** `AnlAccessComponent` and `OptAccessComponent` (and the retention pair) share identical field sets. Will be consolidated when the Data Dictionary provides a shared field registry.

---

## File Impact

### New files (~24)
- `src/Selvagen.GH/Components/Topography/TopoBaseComponent.cs`
- `src/Selvagen.GH/Components/Topography/TopoContoursComponent.cs`
- `src/Selvagen.GH/Components/Topography/TopoUrbanizationComponent.cs`
- `src/Selvagen.GH/Components/Topography/TopoElevationComponent.cs`
- `src/Selvagen.GH/Components/Topography/TopoSlopeComponent.cs`
- `src/Selvagen.GH/Components/Topography/TopoAccess8Component.cs`
- `src/Selvagen.GH/Components/Topography/TopoAccess5Component.cs`
- `src/Selvagen.GH/Components/Topography/TopoDrainageComponent.cs`
- `src/Selvagen.GH/Components/Geology/GeoCoverageComponent.cs`
- `src/Selvagen.GH/Components/Geology/GeoRockComponent.cs`
- `src/Selvagen.GH/Components/Geology/GeoRippabilityComponent.cs`
- `src/Selvagen.GH/Components/Geology/GeoSoilComponent.cs`
- `src/Selvagen.GH/Components/Geology/GeoDepthComponent.cs`
- `src/Selvagen.GH/Components/Analyses/AnlEarthworksComponent.cs`
- `src/Selvagen.GH/Components/Analyses/AnlRetentionComponent.cs`
- `src/Selvagen.GH/Components/Analyses/AnlRockComponent.cs`
- `src/Selvagen.GH/Components/Analyses/AnlAccessComponent.cs`
- `src/Selvagen.GH/Components/Optimizations/OptAccessComponent.cs`
- `src/Selvagen.GH/Components/Optimizations/OptEarthTerrainComponent.cs`
- `src/Selvagen.GH/Components/Optimizations/OptEarthLotsComponent.cs`
- `src/Selvagen.GH/Components/Optimizations/OptEarthTotalComponent.cs`
- `src/Selvagen.GH/Components/Optimizations/OptRetentionComponent.cs`
- `src/Selvagen.GH/Components/Shared/SelvagenPropertiesComponent.cs`
- 23 new icon files in `src/Selvagen.GH/Icons/`

### Modified files
- `src/Selvagen.GH/Components/SelvagenModuleComponentBase.cs` — no logic changes, but the "Upload is last" convention may need a defensive check (name-based lookup instead of positional)
- `src/Selvagen.GH/SelvagenInfo.cs` — update `GH_Exposure` category registrations for new ribbon groups
- `docs/PLUGIN_GUIDE.md` — update field tables and add migration guide

### Deleted files
- `src/Selvagen.GH/Components/SelvagenTopographyComponent.cs`
- `src/Selvagen.GH/Components/SelvagenGeologyComponent.cs`
- `src/Selvagen.GH/Components/SelvagenAnalysesComponent.cs`
- `src/Selvagen.GH/Components/SelvagenOptimizationsComponent.cs`
