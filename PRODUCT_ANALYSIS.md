# Selvagen Grasshopper Plugin - Product Analysis

> **Date:** 2026-03-05
> **Scope:** Analysis of the Grasshopper plugin (`selvagen-gh`) integration with the Selvagen platform (`selvagen`), cross-referenced with the Supabase database schema and the Notion project timeline.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current Architecture Overview](#2-current-architecture-overview)
3. [What Is Working Well](#3-what-is-working-well)
4. [Gap Analysis: Plugin vs Platform](#4-gap-analysis-plugin-vs-platform)
5. [Issues and Risks](#5-issues-and-risks)
6. [Priority Backlog](#6-priority-backlog)
7. [Recommended Improvement Paths](#7-recommended-improvement-paths)
8. [Appendix: Data Model Reference](#appendix-a-data-model-reference)

---

## 1. Executive Summary

The Selvagen Grasshopper plugin is in a **solid early-stage state**. It correctly implements the core "push geometry" workflow -- authentication, project listing, and uploading meshes, curves, and text labels to the Selvagen platform via Supabase. The architecture is clean, with proper separation between the core library and the Grasshopper UI layer.

However, the plugin currently covers only a **fraction of the platform's capabilities**. The database schema reveals a rich domain model with 4 analysis modules (topography, geology, analyses, optimizations), a slide/layer presentation system, animation sequences, and a multi-tenant firm/client structure -- most of which are unreachable from the plugin today.

**Key metrics from the live database:**
| Table | Rows | Notes |
|-------|------|-------|
| Projects | 4 | Active projects in the system |
| Meshes | 3 | All using inline `geometry_data` (no URL uploads) |
| Curve Sets | 3 | All using inline `geometry_data` |
| Text 3D Sets | 3 | All using inline `text_data` |
| Topography / Geology / Analyses / Optimizations | 0 each | Module records not yet populated |
| Slides | 22 | Active slide records (web-created) |
| Slide Layers | 4 | Layers referencing assets |

This tells us the plugin has been used for initial uploads but the full module workflow (creating module records, linking assets to module fields, managing slides) is not yet established through the plugin.

---

## 2. Current Architecture Overview

### Data Flow

```
Rhino/Grasshopper (Z-up)
  |
  v
GH Component (SelvagenUploadMesh/Curves/Labels)
  |
  v
Converter (MeshConverter / CurveConverter / TextConverter)
  + CoordinateHelper: Z-up -> Y-up transform: (X, Y, Z) -> (X, Z, -Y)
  |
  v
Data Model (BufferGeometry / CurveSet / Text3DSet)
  |
  v
SelvagenClient (HTTP POST via System.Net.Http)
  |
  v
Supabase Edge Functions (/plugin-upload-mesh, /plugin-upload-curves, /plugin-upload-text3d)
  |
  v
PostgreSQL (JSONB columns: geometry_data / text_data)
  |
  v
Web Platform (Three.js / React Three Fiber rendering)
```

### Plugin Components (10 total)

| Component | Category | Purpose |
|-----------|----------|---------|
| SelvagenLogin | Auth | Email/password -> JWT token |
| SelvagenClients | Navigation | List firm clients |
| SelvagenProjects | Navigation | List projects (optionally by client) |
| SelvagenProjectModules | Navigation | List module records per project |
| SelvagenUploadMesh | Upload | Rhino Mesh -> BufferGeometry -> DB |
| SelvagenUploadCurves | Upload | Rhino Curves -> CurveSet -> DB |
| SelvagenUploadLabels | Upload | Points + Texts -> Text3DSet -> DB |
| SelvagenAddModuleProperty | Data | PATCH a field on a module record |

### Platform Project Timeline (from Notion)

| Module | Status | Planned | Reality | Relevant to Plugin |
|--------|--------|---------|---------|---------------------|
| 01 - Backend Architecture | Done | Jan 26-30 | Jan 30 - Feb 12 | Foundation |
| 02 - Frontend Architecture | Done | Feb 2-6 | Feb 6 - Feb 15 | - |
| 03 - Authentication | Done | Feb 9-15 | Feb 12 - Feb 19 | Login flow |
| 04 - Backend/Frontend Connection | Done | Feb 27 - Mar 13 | Feb 16 - Feb 27 | API contracts |
| **05 - Project Frontend & CRUD** | **In Progress** | Mar 14-30 | Feb 20 - Mar 16 | Slides, layers, module properties |
| **06 - 3D Animations** | **Planning** | Mar 31 - Apr 17 | Mar 16 - Mar 27 | **Animation export from GH** |
| 07 - Testing | Planning | Apr 18-24 | Mar 29 - Apr 11 | Plugin e2e tests |
| 08 - Hosting | Backlog | - | - | - |
| 09 - Security | Backlog | - | - | Credential management |

**Critical observation**: Module 06 explicitly includes a task "Definicao no Grasshopper para exportar animacoes para o banco de dados" (Grasshopper definition to export animations to the database) -- currently Not Started.

---

## 3. What Is Working Well

### 3.1 Clean Separation of Concerns

The two-project structure (`Selvagen.Core` + `Selvagen.GH`) is exemplary:
- **Core** owns data models, converters, and the API client -- testable without Grasshopper dependencies
- **GH** owns only UI components and plugin metadata
- This allows the Core to be reused for other Rhino integration scenarios (e.g., a Rhino command-line plugin, or a script)

### 3.2 Multi-Target Strategy

The plugin correctly targets both `net48` (Rhino 7) and `net8.0-windows` (Rhino 8). Conditional package references handle the different .NET runtimes cleanly, with no `#if` directive pollution. This is non-trivial to set up correctly and was well executed.

### 3.3 Geometry Format Compliance

The converters produce JSON that directly matches what the web platform expects:
- **BufferGeometry** follows the Three.js `BufferGeometryLoader` contract (metadata, attributes with itemSize/type/array, index)
- **CurveSet** uses flat point arrays with closed/color/linewidth options
- **Text3DSet** uses position tuples with anchor/rotation options
- The coordinate system conversion (Z-up to Y-up) is handled consistently in `CoordinateHelper`

### 3.4 Serialization Testing

The test suite validates that all three geometry models serialize to the exact JSON structure the platform expects. This is the most important thing to test in this plugin and it's properly covered:
- `BufferGeometryModelTests` - validates Three.js format compliance
- `CurveSetModelTests` - validates points array structure and optional field omission
- `Text3DSetModelTests` - validates label position tuples

### 3.5 Database Security Model

The Supabase RLS policies are consistently applied across all tables. The pattern is:
- **SELECT**: firm members OR client members can view
- **INSERT/UPDATE/DELETE**: only firm `admin` or `engineer` roles
- All policies join through `projects -> firm_members` to verify the authenticated user's membership

This means the plugin's JWT token correctly scopes access -- users can only touch their firm's data.

### 3.6 Edge Functions as Plugin API

The dedicated `/plugin-*` edge functions provide a clean contract for the GH plugin without exposing the full PostgREST surface. They validate geometry format, enforce RLS, and return consistent response shapes. This is a good architectural decision that decouples plugin evolution from web app evolution.

---

## 4. Gap Analysis: Plugin vs Platform

This section identifies platform capabilities that the plugin cannot currently access.

### 4.1 Slide and Layer Management (LOW PRIORITY)

**Platform capability**: The web app has a full slide/layer system for composing 3D presentations. Each project module has multiple slides (22 in DB), and slides contain layers that reference assets (meshes, curve_sets, text_3d_sets).

**Plugin gap**: The plugin can upload assets but cannot create slides, add layers, set camera states, or reorder slides.

**Assessment**: This is intentionally low priority. Presentation composition is an inherently visual, interactive task -- the web platform's drag-and-drop UI with live 3D preview is the right tool for this job. The plugin should focus on what Grasshopper does best: geometry generation and data pushing.

### 4.2 Module Record Creation (HIGH IMPACT)

**Platform capability**: Each project has one record per module (topography, geology, analyses, optimizations). These records have ~15-30 typed fields each, linking to specific assets (e.g., `topography.base_mesh_id`, `geology.rock_curve_set_id`).

**Plugin gap**:
- `SelvagenProjectModules` can list existing module records
- `SelvagenAddModuleProperty` can PATCH individual fields
- But there is **no component to CREATE a module record** (INSERT)
- The 0 rows in all four module tables confirms this hasn't been exercised

**Impact**: The intended workflow -- upload a mesh, then link it as `topography.base_mesh_id` -- requires the module record to exist first.

### 4.3 Animation Sequences (UPCOMING - Module 06)

**Platform capability**: The database has `animation_sequences` and `animation_frames` tables supporting frame-by-frame geometry animation with configurable FPS, loop, and metadata.

**Plugin gap**: No components exist for:
- Creating animation sequences
- Uploading animation frames (geometry per frame)
- Managing frame metadata

**Impact**: This is explicitly planned in Notion as a Module 06 task. The `animation_sequences` and `animation_frames` tables are already in the database schema, ready for the plugin to populate.

### 4.4 Large File Upload via Storage URL (MEDIUM IMPACT)

**Platform capability**: Both `meshes` and `curve_sets` tables have a `geometry_url` column alongside `geometry_data`. The web platform supports uploading to Supabase Storage and referencing by URL.

**Plugin gap**: All uploads go through inline `geometry_data` (JSONB). The database confirms all 9 existing assets use inline data, zero use `geometry_url`. There is no component or client method for:
- Uploading to Supabase Storage
- Referencing geometry by URL

**Impact**: Inline JSONB has practical size limits. Large terrain meshes (500K+ vertices) may hit Supabase Edge Function payload limits (~6MB default) or cause slow DB operations. A Storage URL path would handle arbitrarily large files.

### 4.5 Asset Listing and Deletion (MEDIUM IMPACT)

**Plugin gap**: The plugin is **write-only** for assets. There are no components to:
- List existing meshes/curves/labels for a project
- Delete or replace an asset
- Download previously uploaded geometry back into Rhino

**Impact**: Engineers cannot see what's already uploaded, leading to duplicate uploads and no way to clean up. The "replace existing asset" workflow requires going to the web app or using raw API calls.

### 4.6 Project Creation (LOW IMPACT)

**Plugin gap**: The plugin can list projects but cannot create new ones. Project creation requires the web app.

**Impact**: Low -- project creation is infrequent and involves metadata (client assignment, project code) better handled in a web form.

---

## 5. Issues and Risks

### 5.1 CRITICAL: Hardcoded Credentials

**File**: `src/Selvagen.Core/Api/SelvagenConfig.cs`

The Supabase URL and anon key are compiled into the assembly as `public static readonly` strings. While the anon key is designed to be public (it only grants access through RLS), the pattern is problematic:
- Cannot rotate the key without recompiling and redistributing the plugin
- Cannot point to a different environment (staging, dev)
- Sets a bad precedent -- if a service role key were ever added here, it would be a critical exposure

**Recommendation**: Load from a `selvagen.config.json` file in the user's Grasshopper Libraries folder, with fallback defaults.

### 5.2 HIGH: Sync-over-Async Blocks the Grasshopper Canvas

All network calls use `Task.Run(() => ...).GetAwaiter().GetResult()`, which blocks the Grasshopper evaluation thread. During uploads of large meshes, the canvas freezes with no feedback.

**Recommendation**: Use `GH_Document.ScheduleSolution()` for deferred, non-blocking evaluation. This is the standard pattern used by other async Grasshopper plugins (e.g., Speckle).

### 5.3 HIGH: No JWT Token Refresh

The `SelvagenClient` stores a JWT from login but never refreshes it. Supabase JWTs expire after 1 hour by default. After expiration, all API calls silently fail with 401 errors.

**Recommendation**: Store the refresh token from the auth response and implement automatic refresh when the access token approaches expiry.

### 5.4 MEDIUM: Hardcoded Logger Path

**File**: `src/Selvagen.GH/PluginLogger.cs`

The log file path is hardcoded to `c:\repos\selvagen-gh\debug.log`. On any other machine, this either fails silently or creates files in unexpected locations.

**Recommendation**: Use `Path.Combine(Grasshopper.Folders.DefaultAssemblyFolder, "Selvagen", "selvagen.log")` or `%APPDATA%\Grasshopper\Logs\`.

### 5.5 MEDIUM: Missing Converter Unit Tests

Serialization of data models is tested, but the conversion logic itself (Rhino Mesh -> BufferGeometry, Curve -> CurveSet) has zero test coverage. These converters are the most critical code in the plugin -- a bug here means corrupted geometry on the platform.

**Recommendation**: Add parameterized tests with known input geometry (constructed programmatically from RhinoCommon) and verified output arrays.

### 5.6 LOW: Hardcoded Module Table Names

`SelvagenProjectModulesComponent` hardcodes the list `["topography", "geology", "analyses", "optimizations"]`. If the platform adds new modules, the plugin must be recompiled.

**Recommendation**: Add an API endpoint that returns available module types for a project, or at minimum make the list configurable.

### 5.7 LOW: No Input Validation on Upload Components

Upload components don't validate mesh integrity (degenerate triangles, empty meshes) or curve sanity (zero-length curves, extremely high point counts) before converting and uploading. This can lead to confusing errors from the edge function.

**Recommendation**: Add pre-upload validation with clear Grasshopper warning messages.

---

## 6. Priority Backlog

Prioritized by impact on the engineering workflow, aligned with the platform timeline.

### P0 -- Critical (Before Module 05 completes)

| # | Item | Justification | Effort |
|---|------|---------------|--------|
| 1 | **Externalize Supabase credentials** | Security risk; blocks multi-environment deployment | Small |
| 2 | **Implement JWT token refresh** | Sessions expire after 1h; engineers lose work mid-session | Small |
| 3 | **Fix logger path** | Plugin crashes or misbehaves on any machine except the developer's | Small |

### P1 -- High (Aligned with Module 05 - Project Frontend CRUD)

| # | Item | Justification | Effort |
|---|------|---------------|--------|
| 4 | **Add "Create Module Record" component** | Required to populate topography/geology/analyses/optimizations | Medium |
| 5 | **Add "List Assets" component** | Engineers need to see what's uploaded before linking to modules | Medium |
| 6 | **Implement async non-blocking uploads** | Canvas freezes during network calls; poor UX for large meshes | Medium |

### P2 -- Medium (Aligned with Module 06 - Animations)

| # | Item | Justification | Effort |
|---|------|---------------|--------|
| 7 | **Add animation sequence + frame upload components** | Explicitly required by Module 06 Notion task | Large |
| 8 | **Implement Storage URL upload path** | Large meshes and animation frames need file-based upload | Medium |
| 9 | **Add "Delete Asset" component** | Engineers need to clean up test uploads and replace geometry | Small |

### P3 -- Low (Aligned with Module 07 - Testing)

| # | Item | Justification | Effort |
|---|------|---------------|--------|
| 10 | **Add converter unit tests** | Geometry correctness is the plugin's core value proposition | Medium |
| 11 | **Add end-to-end integration tests** | Validate full upload->render pipeline | Large |
| 12 | **Add input validation to upload components** | Better error messages; prevent corrupted uploads | Small |
| 13 | **Make module table list dynamic/configurable** | Future-proofing for new analysis modules | Small |

### P4 -- Backlog (Post-launch)

| # | Item | Justification | Effort |
|---|------|---------------|--------|
| 14 | **Download geometry from platform to Rhino** | Bi-directional sync; enables collaborative workflows | Large |
| 15 | **Batch upload support** | Upload all project geometry in one operation | Medium |
| 16 | **Upload progress feedback** | Visual progress bar during large uploads | Medium |
| 17 | **Yak package manager distribution** | Standard Rhino plugin distribution channel | Medium |
| 18 | **Slide/layer management from Grasshopper** | Presentation composition is better suited to the web UI; low priority | Medium |

---

## 7. Recommended Improvement Paths

### Path A: "Complete the CRUD Loop" (Recommended for next sprint)

**Goal**: Enable the full Grasshopper-to-Platform workflow without switching to the web app for data operations.

**Steps**:
1. Fix P0 items (credentials, token refresh, logger) -- foundational hygiene
2. Add **CreateModuleRecord** component -- enables `INSERT` into topography/geology/etc.
3. Add **ListAssets** component -- returns meshes, curve_sets, and text_3d_sets for a project
4. Modify **AddModuleProperty** to accept asset IDs from ListAssets output, enabling the workflow:
   ```
   Upload Mesh -> Get Mesh ID -> Set topography.base_mesh_id = Mesh ID
   ```

**Why this path**: It unlocks the domain-specific value of the platform. Currently the plugin is a generic geometry uploader; after this, it becomes a geotechnical workflow tool.

### Path B: "Animation Pipeline" (Aligned with Module 06)

**Goal**: Export Grasshopper animation sequences to the platform.

**Steps**:
1. Add **CreateAnimationSequence** component (inputs: project_id, name, asset_type, fps, loop)
2. Add **UploadAnimationFrame** component (inputs: sequence_id, frame_index, geometry_data)
3. Implement Storage URL upload for large frame payloads
4. Create a GH definition template that iterates a slider and uploads each frame

**Why this path**: The Notion task "Definicao no Grasshopper para exportar animacoes" is explicitly scheduled for Module 06 (planned Mar 31 - Apr 17). The database tables (`animation_sequences`, `animation_frames`) are already provisioned.

### Path C: "Production Hardening" (Aligned with Module 07/09)

**Goal**: Make the plugin production-ready for external distribution.

**Steps**:
1. Implement async non-blocking uploads with `ScheduleSolution()`
2. Add comprehensive converter tests (MeshConverter, CurveConverter, TextConverter)
3. Add pre-upload geometry validation
4. Package as a Yak plugin for Rhino package manager distribution
5. Add telemetry/error reporting (opt-in) for debugging deployed instances

**Why this path**: Modules 07 (Testing) and 09 (Security) on the timeline address exactly these concerns.

### Path D: "Presentation from Grasshopper" (Low priority -- only if demanded)

**Goal**: Allow engineers to compose the slide presentation directly from Grasshopper.

**Steps**:
1. Add **CreateSlide** component (inputs: project_id, module, title, view_order)
2. Add **AddSlideLayer** component (inputs: slide_id, asset_type, asset_id)
3. Add **SetCameraState** component (inputs: slide_id, camera position/target/up)

**Why this is low priority**: Presentation composition is an inherently visual, interactive task. The web platform's UI -- with drag-and-drop layers, live 3D preview, and camera controls -- is a far better tool for this than Grasshopper's node-based wiring. The plugin should focus on what Grasshopper does best (geometry generation and data pushing) and leave presentation design to the web app.

---

## Appendix A: Data Model Reference

### Database Schema: Module Fields That Accept Asset References

The following fields in module tables accept foreign keys to asset tables. These represent the specific "slots" where geometry from Grasshopper should be linked.

#### Topography (12 asset slots)

| Field | Asset Table | Purpose |
|-------|-------------|---------|
| `base_mesh_id` | meshes | Base terrain surface |
| `elevation_mesh_id` | meshes | Elevation heatmap |
| `slope_mesh_id` | meshes | Slope analysis |
| `access5_mesh_id` | meshes | 5% accessibility |
| `access8_mesh_id` | meshes | 8% accessibility |
| `outline_curve_set_id` | curve_sets | Site boundary |
| `contours_curve_set_id` | curve_sets | Contour lines |
| `elevation_curve_set_id` | curve_sets | Elevation contours |
| `urbanization_curve_set_id` | curve_sets | Urban boundaries |
| `drainage_curve_set_id` | curve_sets | Water flow paths |
| `contours_text_3d_set_id` | text_3d_sets | Contour labels |

#### Geology (6 asset slots)

| Field | Asset Table | Purpose |
|-------|-------------|---------|
| `soil_mesh_id` | meshes | Soil layer surface |
| `rock_mesh_id` | meshes | Rock layer surface |
| `depth_mesh_id` | meshes | Depth heatmap |
| `coverage_mesh_id` | meshes | Coverage analysis |
| `rippability_mesh_id` | meshes | Rippability analysis |
| `rock_curve_set_id` | curve_sets | Rock contour lines |

#### Analyses (6 asset slots)

| Field | Asset Table | Purpose |
|-------|-------------|---------|
| `earth_mesh_terrain_id` | meshes | Earthworks terrain |
| `earth_mesh_massing_id` | meshes | Earthworks massing |
| `rock_mesh_id` | meshes | Rock cuts |
| `access_curve_set_id` | curve_sets | Access roads |
| `rock_text_3d_set_height_id` | text_3d_sets | Rock height labels |
| `rock_text_3d_set_vol_id` | text_3d_sets | Rock volume labels |
| `access_text_3d_set_id` | text_3d_sets | Access labels |

#### Optimizations (5 asset slots)

| Field | Asset Table | Purpose |
|-------|-------------|---------|
| `earth_mesh_terrain_id` | meshes | Optimized terrain |
| `earth_mesh_lots_id` | meshes | Lot earthworks |
| `access_curve_set_id` | curve_sets | Optimized access |
| `access_text_3d_set_id` | text_3d_sets | Access labels |

**Total: 29 geometry slots across 4 modules** -- each one a potential Grasshopper upload target.

### Edge Function API (Plugin Endpoints)

| Method | Endpoint | Status |
|--------|----------|--------|
| POST | `/functions/v1/plugin-upload-mesh` | Implemented |
| POST | `/functions/v1/plugin-upload-curves` | Implemented |
| POST | `/functions/v1/plugin-upload-text3d` | Implemented |
| GET | `/functions/v1/plugin-projects` | Implemented |
| - | `/functions/v1/plugin-list-assets` | **Not yet implemented** |
| - | `/functions/v1/plugin-create-module` | **Not yet implemented** |
| - | `/functions/v1/plugin-slides` | **Not yet implemented** |
| - | `/functions/v1/plugin-animations` | **Not yet implemented** |

### RLS Policy Pattern (Consistent Across All Tables)

```
SELECT: firm_member(project.firm_id) OR client_member(project.client_id)
INSERT: firm_member(admin | engineer)
UPDATE: firm_member(admin | engineer)
DELETE: firm_member(admin | engineer)  [projects: admin only]
```

---

*Generated by cross-referencing the selvagen-gh plugin codebase, the selvagen platform codebase, the Supabase database schema (project: aqzfsrebvjkegvfexcut), and the Notion project timeline (Plataforma Selvagen).*
