# Selvagen Grasshopper Plugin Guide

A Grasshopper plugin that connects Rhino to the Selvagen web platform. Upload meshes, curves, labels, and animations directly from Grasshopper definitions, and populate module data (Topography, Geology, Analyses, Optimizations) with a single component per module.

## Quick Start

```
[SvLogin] --> Client --> [SvProjects] --> ProjectID --+--> [SvUpMesh]
                                                      +--> [SvUpCrv]
                                                      +--> [SvUpLbl]
                                                      +--> [SvTopo / SvGeo / SvAnalyses / SvOptim]
```

1. Drop a **Selvagen Login** component and enter your email and password.
2. Toggle `L` to `true` to authenticate. The `Client` output carries your session.
3. Wire `Client` into **Selvagen Projects** to list your projects.
4. Pick a Project ID and wire it (along with Client) into any Upload or Module component.
5. Toggle `Go` to `true` to execute the upload.

All geometry is automatically converted from Rhino Z-up to Three.js Y-up before upload.

---

## Components Reference

### Auth

#### Selvagen Login (`SvLogin`)

Authenticate with the Selvagen platform.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Email | E | Text | Your account email |
| Password | P | Text | Your account password |
| Login | L | Boolean | Toggle `true` to authenticate |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| Client | C | Object | Authenticated client (wire to all other components) |
| Status | S | Text | `"Logged in as ..."` or error message |

The client handles token refresh automatically. You do not need to re-login during a session unless you restart Grasshopper.

---

### Data

#### Selvagen Clients (`SvClients`)

List clients (firms) associated with your engineering firm.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| Refresh | R | Boolean | Toggle `true` to re-fetch |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| IDs | ID | Text List | Client (firm) UUIDs |
| Names | N | Text List | Client legal names |

---

#### Selvagen Projects (`SvProjects`)

List projects accessible to your account.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| ClientID | Id | Text | (Optional) Filter by client firm ID |
| Refresh | R | Boolean | Toggle `true` to re-fetch |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| IDs | ID | Text List | Project UUIDs |
| Names | N | Text List | Project names |

---

#### Selvagen List Assets (`SvAssets`)

List existing geometry assets for a project.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| ProjectID | PID | Text | Target project UUID |
| AssetType | T | Text | `meshes`, `curves`, or `labels` (also accepts `curve_sets`, `text_3d_sets`, `mesh`, `text`) |
| Refresh | R | Boolean | Toggle `true` to re-fetch |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| IDs | ID | Text List | Asset UUIDs |
| Names | N | Text List | Asset display names |
| Types | T | Text List | Asset types (meshes only) |

---

#### Selvagen Delete Asset (`SvDelete`)

Delete a geometry asset by ID.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| AssetTable | T | Text | `meshes`, `curve_sets`, or `text_3d_sets` |
| AssetID | ID | Text | UUID of the asset to delete |
| Delete | Go | Boolean | Toggle `true` to execute |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| Success | OK | Boolean | `true` if deletion succeeded |
| Status | S | Text | Result message |

---

### Upload

All upload components convert Rhino geometry to web-compatible formats and push them to the platform.

#### Selvagen Upload Mesh (`SvUpMesh`)

Upload a single Rhino mesh. Quad faces are triangulated automatically. Normals are computed per-face.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| ProjectID | PID | Text | Target project UUID |
| Mesh | M | Mesh | Rhino mesh to upload |
| Name | N | Text | Display name |
| Upload | Go | Boolean | Toggle `true` to execute |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| MeshID | ID | Text | UUID of the created mesh |
| Status | S | Text | `"Uploaded: <name>"` or error |

---

#### Selvagen Upload Curves (`SvUpCrv`)

Upload a list of curves as a curve set. NURBS curves are tessellated to polylines.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| ProjectID | PID | Text | Target project UUID |
| Curves | Crv | Curve List | Rhino curves to upload |
| Name | N | Text | Display name |
| Upload | Go | Boolean | Toggle `true` to execute |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| CurveSetID | ID | Text | UUID of the created curve set |
| Status | S | Text | `"Uploaded: <name> (N curves)"` or error |

---

#### Selvagen Upload Labels (`SvUpLbl`)

Upload 3D text labels from paired point and text lists.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| ProjectID | PID | Text | Target project UUID |
| Points | P | Point3d List | Label positions |
| Texts | T | Text List | Label text strings (must match Points count) |
| Name | N | Text | Display name |
| Upload | Go | Boolean | Toggle `true` to execute |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| TextSetID | ID | Text | UUID of the created text 3D set |
| Status | S | Text | `"Uploaded: <name> (N labels)"` or error |

---

#### Selvagen Upload Animation (`SvUpAnim`)

Upload a sequence of meshes as an animation. The first mesh becomes the base mesh (full geometry). Subsequent frames store only vertex positions if the topology matches the base (position-only format, ~75% smaller), or fall back to full geometry if topology changes.

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| ProjectID | PID | Text | Target project UUID |
| Meshes | M | Mesh List | Ordered mesh frames (minimum 2) |
| Name | N | Text | Display name |
| FPS | FPS | Number | Frames per second (default `1.0`) |
| Loop | L | Boolean | Loop playback (default `false`) |
| Upload | Go | Boolean | Toggle `true` to execute |

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| SequenceID | ID | Text | UUID of the created animation sequence |
| Status | S | Text | Upload result with frame count and format |

The upload performs 4 steps: convert meshes, upload base mesh, create animation sequence record, then upload each frame. Progress is written to the log file.

---

### Modules

Module components manage the four domain-specific tables in the platform: **Topography**, **Geology**, **Analyses**, and **Optimizations**. Each component:

- **Auto-creates** the module record on first use (one record per project, idempotent).
- **Patches** all provided values in a single request.
- Accepts **geometry asset IDs** (from Upload components) and **scalar values** as typed inputs.
- Supports a **Properties** input for custom JSON data.

All data inputs are **optional**. Connect only the ones you need -- the component ignores unconnected inputs.

#### Common Inputs (all module components)

| Input | Nickname | Type | Description |
|-------|----------|------|-------------|
| Client | C | Object | Authenticated client |
| ProjectID | PID | Text | Target project UUID |
| ... | ... | ... | (module-specific data inputs, all optional) |
| Properties | Props | Text | Custom JSON string for the `properties` column |
| Upload | Go | Boolean | Toggle `true` to execute |

#### Common Outputs (all module components)

| Output | Nickname | Type | Description |
|--------|----------|------|-------------|
| RecordID | ID | Text | Module record UUID |
| Status | S | Text | `"Created: ..."` or `"Updated: ... (N fields)"` |

---

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

---

## Workflow Examples

### Example 1: Upload a Terrain Mesh and Populate Topography

```
[SvLogin] --> Client --+--> [SvProjects] --> ProjectID
                       |
  Rhino Mesh ----------+--> [SvUpMesh] --> MeshID
                       |                      |
                       +--> [SvTopo]  <-------+-- (wire MeshID into BaseMeshID)
                              ^
                              |-- BaseArea2D = area panel
                              |-- ElevMin = number slider
                              |-- ElevMax = number slider
                              +-- Go = toggle
```

1. Upload the terrain mesh with **SvUpMesh** to get its UUID.
2. Wire that UUID into **SvTopo**'s `BaseMeshID` input.
3. Connect scalar values (area, elevation range) from your Grasshopper definition.
4. Toggle `Go` to push everything to the platform in one request.

### Example 2: Upload Multiple Geometry Types for Analyses

```
[SvLogin] --> Client --> [SvProjects] --> ProjectID
                                            |
  Terrain Mesh --> [SvUpMesh] --> TerrMeshID |
  Massing Mesh --> [SvUpMesh] --> MassMeshID |
  Access Crv   --> [SvUpCrv] --> AccCurvesID |
  Rock Labels  --> [SvUpLbl] --> RockLabelsID|
                                            |
        [SvAnalyses] <-- all IDs + scalar values
```

Upload each geometry asset first, then wire all resulting IDs into the Analyses component alongside computed scalars (volumes, costs, rates).

### Example 3: Upload an Animation

```
  Frame meshes (list) --> [SvUpAnim]
                             ^
  Client ----+               |
  ProjectID -+               |
  Name ------+               |
  FPS = 2.0 -+               |
  Loop = true +              |
  Go = toggle +--------------+
```

Provide a flat list of meshes in frame order (minimum 2). The component automatically detects whether topology is consistent across frames and picks the optimal storage strategy.

---

## Configuration

The plugin reads its Supabase endpoint settings from:

```
%APPDATA%\Selvagen\selvagen.config.json
```

If the file does not exist, built-in defaults are used.

### Config File Format

```json
{
  "supabase_url": "https://your-project.supabase.co",
  "supabase_anon_key": "eyJ..."
}
```

### Logs

Plugin logs are written to:

```
%APPDATA%\Selvagen\Logs\selvagen.log
```

Check this file for detailed request/response information when debugging upload issues.

---

## Coordinate System

All geometry uploaded through this plugin is automatically converted:

```
Rhino (Z-up):    (X, Y, Z)
Three.js (Y-up): (X, Z, -Y)
```

You work in Rhino's native Z-up coordinate system. The conversion is transparent.

---

## Installation

### From Build Output

1. Build the solution: `dotnet build Selvagen.sln`
2. Copy the output folder matching your Rhino version into Grasshopper's libraries folder:

| Rhino Version | Build Output | Libraries Folder |
|---|---|---|
| Rhino 7 | `src/Selvagen.GH/bin/Debug/net48/` | `%APPDATA%\Grasshopper\Libraries\` |
| Rhino 8 | `src/Selvagen.GH/bin/Debug/net8.0-windows/` | `%APPDATA%\Grasshopper\Libraries\` |

3. Restart Rhino. The components appear under the **Selvagen** tab in Grasshopper.

### Verifying Installation

After loading, you should see these tabs on the Grasshopper ribbon under **Selvagen**:

- **Auth** -- Login
- **Data** -- Clients, Projects, List Assets, Delete Asset
- **Upload** -- Upload Mesh, Upload Curves, Upload Labels, Upload Animation
- **Modules** -- Topography, Geology, Analyses, Optimizations

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Login outputs `"Error: ..."` | Wrong credentials or Supabase URL | Check email/password. Verify config file if using a custom endpoint. |
| Upload shows `"Error: 403"` | RLS policy denied access | Your account must be a firm member (admin or engineer) on the project's firm. |
| Upload shows `"Error: 401"` | Expired or invalid token | Re-trigger the Login component. Token refresh is automatic, but a full re-login may be needed after long idle periods. |
| Module component shows `"Error: 23505"` | Unique constraint violation | A record already exists for this project. The component should handle this automatically -- if you see this, try toggling `Go` again. |
| Component shows `"Waiting..."` | `Go`/`Upload` input is `false` or Client is not connected | Ensure the Login component has run and the boolean trigger is `true`. |
| No output on Asset List | No assets of that type exist yet | Upload some geometry first, then refresh. |
| Geometry looks wrong on the platform | Coordinate system mismatch | The plugin handles Z-up to Y-up conversion. If geometry was already in Y-up, it will be double-converted. Work in Rhino's native Z-up. |
