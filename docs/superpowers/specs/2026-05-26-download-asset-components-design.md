# Download Asset Components

**Date:** 2026-05-26
**Status:** Draft

## Summary

Add four Download components that mirror the existing Upload components, allowing users to fetch assets from the platform back into Grasshopper as native Rhino geometry. Each asset type (Mesh, Curves, Labels, Animation) gets its own component with typed outputs.

## Motivation

The plugin currently supports **Upload** (Rhino → Platform) and **List** (browse assets by ID/name), but there is no way to **Download** (Platform → Rhino). Users need to load previously uploaded geometry back into Grasshopper for iteration, comparison, or reuse.

## Database Schema (GEN.BOARD)

| Table | Data column | Type | URL fallback | Rows |
|-------|-----------|------|-------------|------|
| `meshes` | `geometry_data` | jsonb (BufferGeometry) | `geometry_url` | 43 |
| `curve_sets` | `geometry_data` | jsonb (CurveSet) | `geometry_url` | 9 |
| `text_3d_sets` | `text_data` | jsonb (Text3DSet) | `geometry_url` | 6 |
| `animation_frames` | `geometry_data` | jsonb (AnimationFrameData) | — | 118 |

All geometry is stored in Three.js Y-up coordinates. The download path must reverse the coordinate transform back to Rhino Z-up.

## Architecture

### Layer 1: Reverse Coordinate Transform (`CoordinateHelper.cs`)

Add `FromYUp` methods that invert the existing `ToYUp`/`WriteYUp`:

- `FromYUp(double x, double y, double z) → Point3d`: `(x, y, z)_three → (x, -z, y)_rhino`
- `WriteFromYUp(double[] buffer, int offset, Point3f out)`: hot-loop variant for vertex arrays

### Layer 2: Reverse Converters

Add `From*` methods to existing converter classes (no new files):

**`MeshConverter.FromBufferGeometry(BufferGeometry bg) → Mesh`**
- Read position array → vertices (Y-up → Z-up)
- Read normal array → normals
- Read index array → faces (group indices into triangles)
- Read optional color array → vertex colors (denormalize 0-1 → 0-255)
- Return fully constructed `Rhino.Geometry.Mesh`

**`CurveConverter.FromCurveSet(CurveSet cs) → (List<PolylineCurve>, List<Color>, List<double>)`**
- For each CurveData: read flat points array → polyline vertices (Y-up → Z-up)
- If `closed`, close the polyline
- Parse `color` hex string → `System.Drawing.Color`
- Read `linewidth` → double

**`TextConverter.FromText3DSet(Text3DSet ts) → (List<Point3d>, List<string>, List<Plane>, List<Color>, List<double>)`**
- For each LabelData: position (Y-up → Z-up), text, optional rotation → Plane, optional color, optional fontSize

### Layer 3: API Response Models (`ApiResponses.cs`)

New models to deserialize full asset rows (existing `AssetInfo` only has id/name):

```csharp
public class MeshAssetFull
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("geometry_data")] public BufferGeometry GeometryData { get; set; }
    [JsonPropertyName("geometry_url")] public string GeometryUrl { get; set; }
}

public class CurveSetAssetFull
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("geometry_data")] public CurveSet GeometryData { get; set; }
    [JsonPropertyName("geometry_url")] public string GeometryUrl { get; set; }
}

public class Text3DSetAssetFull
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("text_data")] public Text3DSet TextData { get; set; }
    [JsonPropertyName("geometry_url")] public string GeometryUrl { get; set; }
}
```

### Layer 4: SelvagenClient Methods

New GET methods using PostgREST:

```
GetMeshAsync(id)       → GET /rest/v1/meshes?id=eq.{id}&select=id,name,type,geometry_data,geometry_url
GetCurveSetAsync(id)   → GET /rest/v1/curve_sets?id=eq.{id}&select=id,name,geometry_data,geometry_url
GetText3DSetAsync(id)  → GET /rest/v1/text_3d_sets?id=eq.{id}&select=id,name,text_data,geometry_url
GetAnimationFramesAsync(sequenceId) → GET /rest/v1/animation_frames?sequence_id=eq.{id}&select=frame_index,geometry_data,label&order=frame_index
GetAnimationSequenceInfoAsync(id)   → GET /rest/v1/animation_sequences?id=eq.{id}&select=id,name,fps,loop,base_asset_id,frame_count
```

Each returns a deserialized array (PostgREST always returns arrays); take `[0]` or throw if empty.

`geometry_url` handling: if `geometry_data`/`text_data` is null but `geometry_url` is present, fetch the URL to get the JSON payload. This is a fallback for large assets stored in Supabase Storage.

### Layer 5: GH Components

All four components live in `"Selvagen"` / `"08 Assets"`, share a `SelvagenDownloadComponentBase` (mirrors `SelvagenUploadComponentBase`).

**Download Mesh** (`SvDnMesh`)
- Input: `Asset ID` (AstID, text)
- Output: `Mesh` (M, mesh), `Name` (N, text), `Status` (S, text)
- Converts BufferGeometry → Rhino Mesh with vertex colors applied

**Download Curves** (`SvDnCrv`)
- Input: `Asset ID` (AstID, text)
- Output: `Curves` (Crv, curve list), `Colors` (C, colour list), `Linewidths` (W, number list), `Name` (N, text), `Status` (S, text)

**Download Labels** (`SvDnLbl`)
- Input: `Asset ID` (AstID, text)
- Output: `Planes` (Pl, plane list), `Texts` (Txt, text list), `Colors` (C, colour list), `Font Sizes` (Fs, number list), `Name` (N, text), `Status` (S, text)
- Outputs Planes (not just Points) to mirror the Upload Labels input, which takes Planes for position + rotation.

**Download Animation** (`SvDnAnim`)
- Input: `Sequence ID` (SeqID, text)
- Output: `Meshes` (M, mesh list), `Labels` (Lbl, text list), `FPS` (F, number), `Loop` (L, bool), `Name` (N, text), `Status` (S, text)
- Fetches sequence metadata (fps, loop, base_asset_id, frame_count) + all frames
- Also fetches the base mesh (via `base_asset_id`) to provide topology (indices, normals) for "positions"-format frames
- For each frame:
  - `format = "positions"`: clone base mesh, replace vertex positions (Y-up → Z-up)
  - `format = "buffer_geometry"`: full conversion via `MeshConverter.FromBufferGeometry`

### Component behavior

- **Auto-fetch**: components fire when Asset ID is provided (no manual "Go" toggle). These are read-only operations.
- **Caching**: cache the last fetched result keyed by asset ID. Only re-fetch when the ID changes.
- **Auth**: use `SessionManager.Current` like all other components. Show warning if not logged in.
- **Async pattern**: `Task.Run(...).GetAwaiter().GetResult()` matching existing components.

### Toolbar separator (GH_Exposure)

Upload components currently use `GH_Exposure.primary` (from `SelvagenUploadComponentBase`). Download components use `GH_Exposure.secondary` in their base class. This creates a visual separator line between the Upload and Download groups in the "08 Assets" tab.

Toolbar order in "08 Assets":
```
[ ListAssets | UploadMesh | UploadCurves | UploadLabels | UploadAnimation | ── | DownloadMesh | DownloadCurves | DownloadLabels | DownloadAnimation | Delete ]
```

### Icons

The current Upload icons are standalone (single centered icon, no badge). They must be converted to the **composite pattern** (base icon + small badge at bottom-right) used by the Topo/Geo/Anl/Opt families.

- Upload components: same base icon + `mdi:arrow-up-bold` badge
- Download components: same base icon + `mdi:arrow-down-bold` badge

Updated `generate_icons.py` entries:

```python
# Move from STANDALONE_ICONS to FAMILY_ICONS:
UPLOAD_DOWNLOAD_ICONS = {
    # Upload — base + up arrow
    "UploadMesh":           ("mdi:vector-triangle",   "mdi:arrow-up-bold"),
    "UploadCurves":         ("mdi:vector-curve",      "mdi:arrow-up-bold"),
    "UploadLabels":         ("mdi:format-text",       "mdi:arrow-up-bold"),
    "UploadAnimation":      ("mdi:animation-play",    "mdi:arrow-up-bold"),

    # Download — base + down arrow
    "DownloadMesh":         ("mdi:vector-triangle",   "mdi:arrow-down-bold"),
    "DownloadCurves":       ("mdi:vector-curve",      "mdi:arrow-down-bold"),
    "DownloadLabels":       ("mdi:format-text",        "mdi:arrow-down-bold"),
    "DownloadAnimation":    ("mdi:animation-play",     "mdi:arrow-down-bold"),
}
```

## Files Changed

| File | Change |
|------|--------|
| `Selvagen.Core/Converters/CoordinateHelper.cs` | Add `FromYUp` methods |
| `Selvagen.Core/Converters/MeshConverter.cs` | Add `FromBufferGeometry` |
| `Selvagen.Core/Converters/CurveConverter.cs` | Add `FromCurveSet` |
| `Selvagen.Core/Converters/TextConverter.cs` | Add `FromText3DSet` |
| `Selvagen.Core/Models/ApiResponses.cs` | Add `MeshAssetFull`, `CurveSetAssetFull`, `Text3DSetAssetFull` |
| `Selvagen.Core/Api/SelvagenClient.cs` | Add `GetMeshAsync`, `GetCurveSetAsync`, `GetText3DSetAsync`, `GetAnimationFramesAsync`, `GetAnimationSequenceInfoAsync` |
| `Selvagen.GH/Components/SelvagenDownloadComponentBase.cs` | New base class |
| `Selvagen.GH/Components/SelvagenDownloadMeshComponent.cs` | New component |
| `Selvagen.GH/Components/SelvagenDownloadCurvesComponent.cs` | New component |
| `Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs` | New component |
| `Selvagen.GH/Components/SelvagenDownloadAnimationComponent.cs` | New component |
| `Selvagen.GH/Icons/generate_icons.py` | Move Upload icons from standalone to composite; add Download icons |
| `Selvagen.GH/Icons/DownloadMesh.png` | Generated icon (base + down arrow) |
| `Selvagen.GH/Icons/DownloadCurves.png` | Generated icon (base + down arrow) |
| `Selvagen.GH/Icons/DownloadLabels.png` | Generated icon (base + down arrow) |
| `Selvagen.GH/Icons/DownloadAnimation.png` | Generated icon (base + down arrow) |
| `Selvagen.GH/Icons/UploadMesh.png` | Regenerated (base + up arrow badge) |
| `Selvagen.GH/Icons/UploadCurves.png` | Regenerated (base + up arrow badge) |
| `Selvagen.GH/Icons/UploadLabels.png` | Regenerated (base + up arrow badge) |
| `Selvagen.GH/Icons/UploadAnimation.png` | Regenerated (base + up arrow badge) |

## Out of Scope

- Downloading assets by URL from Supabase Storage (geometry_url fallback) — deferred unless inline data is missing for existing assets
- Batch download / multi-asset loading
- Preview/thumbnail generation from downloaded geometry
