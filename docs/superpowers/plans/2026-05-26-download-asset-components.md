# Download Asset Components Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add four Download components (Mesh, Curves, Labels, Animation) that fetch assets from the platform and convert them back into native Rhino geometry, mirroring the existing Upload components.

**Architecture:** Reverse converters (Y-up → Z-up) are added to existing converter classes. New API response models and client methods fetch full asset payloads via PostgREST. Four new GH components use a shared base class with `GH_Exposure.secondary` for toolbar separation from Upload components.

**Tech Stack:** C# / .NET 7, RhinoCommon, Grasshopper SDK, Supabase PostgREST, System.Text.Json, Python (Pillow + CairoSVG for icons)

**Spec:** `docs/superpowers/specs/2026-05-26-download-asset-components-design.md`

---

### Task 1: Update icon generation script and regenerate icons

**Files:**
- Modify: `src/Selvagen.GH/Icons/generate_icons.py`
- Regenerate: `src/Selvagen.GH/Icons/UploadMesh.png`, `UploadCurves.png`, `UploadLabels.png`, `UploadAnimation.png`
- Create: `src/Selvagen.GH/Icons/DownloadMesh.png`, `DownloadCurves.png`, `DownloadLabels.png`, `DownloadAnimation.png`

- [ ] **Step 1: Move Upload icons from STANDALONE to FAMILY and add Download icons**

In `src/Selvagen.GH/Icons/generate_icons.py`, remove the four Upload entries from `STANDALONE_ICONS`:

```python
# Remove these four lines from STANDALONE_ICONS:
#     "UploadMesh":       "mdi:vector-triangle",
#     "UploadCurves":     "mdi:vector-curve",
#     "UploadLabels":     "mdi:format-text",
#     "UploadAnimation":  "mdi:animation-play",
```

Then add a new `UPLOAD_DOWNLOAD_ICONS` dict after `FAMILY_ICONS` and before `STANDALONE_ICONS`:

```python
UPLOAD_DOWNLOAD_ICONS = {
    # Upload — base + up arrow
    "UploadMesh":           ("mdi:vector-triangle",  "mdi:arrow-up-bold"),
    "UploadCurves":         ("mdi:vector-curve",     "mdi:arrow-up-bold"),
    "UploadLabels":         ("mdi:format-text",      "mdi:arrow-up-bold"),
    "UploadAnimation":      ("mdi:animation-play",   "mdi:arrow-up-bold"),

    # Download — base + down arrow
    "DownloadMesh":         ("mdi:vector-triangle",  "mdi:arrow-down-bold"),
    "DownloadCurves":       ("mdi:vector-curve",     "mdi:arrow-down-bold"),
    "DownloadLabels":       ("mdi:format-text",      "mdi:arrow-down-bold"),
    "DownloadAnimation":    ("mdi:animation-play",   "mdi:arrow-down-bold"),
}
```

Update the `main()` function to also generate this new dict:

```python
def main():
    total = len(FAMILY_ICONS) + len(UPLOAD_DOWNLOAD_ICONS) + len(STANDALONE_ICONS)
    print(f"Generating {total} icons ({len(FAMILY_ICONS)} family, {len(UPLOAD_DOWNLOAD_ICONS)} upload/download, {len(STANDALONE_ICONS)} standalone)...\n")

    errors = []

    print("-- Family icons (base + badge) --")
    for comp, (base_id, badge_id) in FAMILY_ICONS.items():
        try:
            generate_composite(comp, base_id, badge_id)
        except Exception as e:
            errors.append((comp, str(e)))
            print(f"  ERROR {comp}: {e}")

    print("\n-- Upload/Download icons (base + arrow badge) --")
    for comp, (base_id, badge_id) in UPLOAD_DOWNLOAD_ICONS.items():
        try:
            generate_composite(comp, base_id, badge_id)
        except Exception as e:
            errors.append((comp, str(e)))
            print(f"  ERROR {comp}: {e}")

    print("\n-- Standalone icons --")
    for comp, icon_id in STANDALONE_ICONS.items():
        try:
            generate_standalone(comp, icon_id)
        except Exception as e:
            errors.append((comp, str(e)))
            print(f"  ERROR {comp}: {e}")

    print(f"\nDone: {total - len(errors)} succeeded, {len(errors)} failed")
    if errors:
        print("\nFailed:")
        for comp, err in errors:
            print(f"  {comp}: {err}")
```

- [ ] **Step 2: Install dependencies and run the icon generator**

```powershell
pip install Pillow cairosvg
python src\Selvagen.GH\Icons\generate_icons.py
```

Expected: all 8 upload/download icons generated successfully. Verify files exist:

```powershell
Get-ChildItem src\Selvagen.GH\Icons\*oad*.png
```

Expected: `UploadMesh.png`, `UploadCurves.png`, `UploadLabels.png`, `UploadAnimation.png`, `DownloadMesh.png`, `DownloadCurves.png`, `DownloadLabels.png`, `DownloadAnimation.png`

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.GH/Icons/generate_icons.py src/Selvagen.GH/Icons/UploadMesh.png src/Selvagen.GH/Icons/UploadCurves.png src/Selvagen.GH/Icons/UploadLabels.png src/Selvagen.GH/Icons/UploadAnimation.png src/Selvagen.GH/Icons/DownloadMesh.png src/Selvagen.GH/Icons/DownloadCurves.png src/Selvagen.GH/Icons/DownloadLabels.png src/Selvagen.GH/Icons/DownloadAnimation.png
```

```powershell
git commit -m "feat(icons): add upload/download arrow badges to asset icons"
```

---

### Task 2: Add reverse coordinate helpers

**Files:**
- Modify: `src/Selvagen.Core/Converters/CoordinateHelper.cs`

- [ ] **Step 1: Add FromYUp methods to CoordinateHelper**

Add these methods at the end of the `CoordinateHelper` class, after the existing `PlaneToYUpEuler` method:

```csharp
/// <summary>
/// Convert a Three.js Y-up coordinate to a Rhino Z-up Point3d.
/// Inverse of ToYUp: (x, y, z)_three → (x, -z, y)_rhino
/// </summary>
public static Point3d FromYUp(double x, double y, double z)
{
    return new Point3d(x, -z, y);
}

/// <summary>
/// Read a Y-up point from a flat buffer at the given offset and return a Z-up Point3d.
/// </summary>
public static Point3d FromYUp(double[] buffer, int offset)
{
    return new Point3d(buffer[offset], -buffer[offset + 2], buffer[offset + 1]);
}

/// <summary>
/// Convert a Three.js Y-up coordinate to a Rhino Z-up Vector3d.
/// </summary>
public static Vector3d VectorFromYUp(double x, double y, double z)
{
    return new Vector3d(x, -z, y);
}

/// <summary>
/// Reconstruct a Rhino Plane from Three.js Y-up Euler XYZ angles (radians).
/// Inverse of PlaneToYUpEuler.
/// </summary>
public static Plane FromYUpEuler(double[] euler, Point3d origin)
{
    double ex = euler[0], ey = euler[1], ez = euler[2];
    double cx = Math.Cos(ex), sx = Math.Sin(ex);
    double cy = Math.Cos(ey), sy = Math.Sin(ey);
    double cz = Math.Cos(ez), sz = Math.Sin(ez);

    // R = Rx(ex) · Ry(ey) · Rz(ez)  — Three.js default XYZ intrinsic order
    // Column 0 (X-axis in Y-up)
    double c0x = cy * cz;
    double c0y = cx * sz + sx * sy * cz;
    double c0z = sx * sz - cx * sy * cz;

    // Column 1 (Y-axis in Y-up)
    double c1x = -cy * sz;
    double c1y = cx * cz - sx * sy * sz;
    double c1z = sx * cz + cx * sy * sz;

    // Convert each column from Y-up to Z-up: (x, y, z) → (x, -z, y)
    var xAxis = new Vector3d(c0x, -c0z, c0y);
    var yAxis = new Vector3d(c1x, -c1z, c1y);

    return new Plane(origin, xAxis, yAxis);
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.Core/Converters/CoordinateHelper.cs
git commit -m "feat(converters): add reverse Y-up to Z-up coordinate helpers"
```

---

### Task 3: Add reverse mesh converter

**Files:**
- Modify: `src/Selvagen.Core/Converters/MeshConverter.cs`

- [ ] **Step 1: Add FromBufferGeometry to MeshConverter**

Add this method to the `MeshConverter` class, after the existing `ToBufferGeometry` method:

```csharp
/// <summary>
/// Convert a Three.js BufferGeometry model back to a Rhino Mesh.
/// Handles Y-up → Z-up coordinate swap and optional vertex colors.
/// </summary>
/// <param name="bg">BufferGeometry in Y-up coordinate system.</param>
/// <returns>Rhino Mesh in Z-up coordinate system.</returns>
public static Mesh FromBufferGeometry(BufferGeometry bg)
{
    if (bg == null)
        throw new ArgumentNullException(nameof(bg));

    var mesh = new Mesh();
    var posArr = bg.Data.Attributes.Position.Array;
    int vertCount = posArr.Length / 3;

    for (int i = 0; i < vertCount; i++)
        mesh.Vertices.Add(CoordinateHelper.FromYUp(posArr, i * 3));

    var idxArr = bg.Data.Index?.Array;
    if (idxArr != null)
    {
        for (int i = 0; i + 2 < idxArr.Length; i += 3)
            mesh.Faces.AddFace(idxArr[i], idxArr[i + 1], idxArr[i + 2]);
    }

    var normAttr = bg.Data.Attributes.Normal;
    if (normAttr?.Array != null && normAttr.Array.Length == vertCount * 3)
    {
        var normArr = normAttr.Array;
        for (int i = 0; i < vertCount; i++)
            mesh.Normals.Add(CoordinateHelper.VectorFromYUp(
                normArr[i * 3], normArr[i * 3 + 1], normArr[i * 3 + 2]));
    }
    else
    {
        mesh.Normals.ComputeNormals();
    }

    var colorAttr = bg.Data.Attributes.Color;
    if (colorAttr?.Array != null && colorAttr.Array.Length == vertCount * 3)
    {
        var colArr = colorAttr.Array;
        for (int i = 0; i < vertCount; i++)
        {
            int r = Math.Max(0, Math.Min(255, (int)Math.Round(colArr[i * 3] * 255.0)));
            int g = Math.Max(0, Math.Min(255, (int)Math.Round(colArr[i * 3 + 1] * 255.0)));
            int b = Math.Max(0, Math.Min(255, (int)Math.Round(colArr[i * 3 + 2] * 255.0)));
            mesh.VertexColors.Add(r, g, b);
        }
    }

    return mesh;
}
```

Also add this using at the top if not already present: `using Rhino.Geometry;` (already there).

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.Core/Converters/MeshConverter.cs
git commit -m "feat(converters): add FromBufferGeometry reverse mesh converter"
```

---

### Task 4: Add reverse curve converter

**Files:**
- Modify: `src/Selvagen.Core/Converters/CurveConverter.cs`

- [ ] **Step 1: Add FromCurveSet to CurveConverter**

Add this method to the `CurveConverter` class, after the existing `ToCurveSet` method:

```csharp
/// <summary>
/// Convert a CurveSet model back to Rhino polyline curves.
/// Handles Y-up → Z-up coordinate swap.
/// </summary>
public static void FromCurveSet(CurveSet cs,
    out List<PolylineCurve> curves,
    out List<Color> colors,
    out List<double> linewidths)
{
    if (cs == null)
        throw new ArgumentNullException(nameof(cs));

    curves = new List<PolylineCurve>();
    colors = new List<Color>();
    linewidths = new List<double>();

    foreach (var cd in cs.Curves)
    {
        if (cd?.Points == null || cd.Points.Length < 6) continue;

        var pts = new List<Point3d>();
        for (int i = 0; i + 2 < cd.Points.Length; i += 3)
            pts.Add(CoordinateHelper.FromYUp(cd.Points, i));

        if (cd.Closed && pts.Count > 1 && pts[0].DistanceTo(pts[pts.Count - 1]) > 1e-10)
            pts.Add(pts[0]);

        curves.Add(new PolylineCurve(pts));

        if (!string.IsNullOrEmpty(cd.Color) && cd.Color.StartsWith("#"))
            colors.Add(ColorTranslator.FromHtml(cd.Color));
        else
            colors.Add(Color.Black);

        linewidths.Add(cd.Linewidth ?? 1.0);
    }
}
```

Also add this using at the top of the file if not already present:

```csharp
using Rhino.Geometry;
```

(`PolylineCurve` lives in `Rhino.Geometry`. `Color` and `ColorTranslator` live in `System.Drawing`, already imported.)

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.Core/Converters/CurveConverter.cs
git commit -m "feat(converters): add FromCurveSet reverse curve converter"
```

---

### Task 5: Add reverse text converter

**Files:**
- Modify: `src/Selvagen.Core/Converters/TextConverter.cs`

- [ ] **Step 1: Add FromText3DSet to TextConverter**

Add this method to the `TextConverter` class, after the existing methods:

```csharp
/// <summary>
/// Convert a Text3DSet model back to Rhino planes, texts, colors, and font sizes.
/// Planes carry both position and orientation (mirrors the Upload Labels input).
/// </summary>
public static void FromText3DSet(Text3DSet ts,
    out List<Plane> planes,
    out List<string> texts,
    out List<Color> colors,
    out List<double> fontSizes)
{
    if (ts == null)
        throw new ArgumentNullException(nameof(ts));

    planes = new List<Plane>();
    texts = new List<string>();
    colors = new List<Color>();
    fontSizes = new List<double>();

    foreach (var label in ts.Labels)
    {
        if (label == null) continue;

        var origin = CoordinateHelper.FromYUp(
            label.Position[0], label.Position[1], label.Position[2]);

        Plane plane;
        if (label.Rotation != null && label.Rotation.Length == 3)
            plane = CoordinateHelper.FromYUpEuler(label.Rotation, origin);
        else
            plane = new Plane(origin, Vector3d.XAxis, Vector3d.YAxis);

        planes.Add(plane);
        texts.Add(label.Text ?? "");

        if (!string.IsNullOrEmpty(label.Color) && label.Color.StartsWith("#"))
            colors.Add(ColorTranslator.FromHtml(label.Color));
        else
            colors.Add(Color.Black);

        fontSizes.Add(label.FontSize ?? 0.0);
    }
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.Core/Converters/TextConverter.cs
git commit -m "feat(converters): add FromText3DSet reverse text converter"
```

---

### Task 6: Add API response models for full asset data

**Files:**
- Modify: `src/Selvagen.Core/Models/ApiResponses.cs`

- [ ] **Step 1: Add full asset response models**

Add these classes at the end of `ApiResponses.cs`, before the closing namespace brace, after `SelvagenApiException`:

```csharp
/// <summary>
/// Full mesh row from PostgREST, including geometry data.
/// </summary>
public class MeshAssetFull
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("geometry_data")]
    public BufferGeometry GeometryData { get; set; }

    [JsonPropertyName("geometry_url")]
    public string GeometryUrl { get; set; }
}

/// <summary>
/// Full curve set row from PostgREST, including geometry data.
/// </summary>
public class CurveSetAssetFull
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("geometry_data")]
    public CurveSet GeometryData { get; set; }

    [JsonPropertyName("geometry_url")]
    public string GeometryUrl { get; set; }
}

/// <summary>
/// Full text 3D set row from PostgREST, including text data.
/// </summary>
public class Text3DSetAssetFull
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("text_data")]
    public Text3DSet TextData { get; set; }

    [JsonPropertyName("geometry_url")]
    public string GeometryUrl { get; set; }
}

/// <summary>
/// Full animation sequence metadata from PostgREST.
/// </summary>
public class AnimationSequenceFull
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    [JsonPropertyName("loop")]
    public bool? Loop { get; set; }

    [JsonPropertyName("base_asset_id")]
    public string BaseAssetId { get; set; } = "";

    [JsonPropertyName("frame_count")]
    public int FrameCount { get; set; }
}

/// <summary>
/// Full animation frame row from PostgREST, including geometry data.
/// </summary>
public class AnimationFrameFull
{
    [JsonPropertyName("frame_index")]
    public int FrameIndex { get; set; }

    [JsonPropertyName("geometry_data")]
    public AnimationFrameData GeometryData { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.Core/Models/ApiResponses.cs
git commit -m "feat(models): add full asset response models for download"
```

---

### Task 7: Add download methods to SelvagenClient

**Files:**
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs`

- [ ] **Step 1: Add Get methods for each asset type**

Add these methods to `SelvagenClient.cs` in a new section after the `// ── Asset Queries` section (after `QueryAssetsAsync`):

```csharp
// ── Asset Downloads (full data) ─────────────────────────────────

/// <summary>
/// Fetch a single mesh including its geometry data.
/// </summary>
public async Task<MeshAssetFull> GetMeshAsync(string id)
{
    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

    var path = $"/rest/v1/meshes?id=eq.{id}&select=id,name,type,geometry_data,geometry_url";
    var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    if (!response.IsSuccessStatusCode)
        throw new SelvagenApiException($"Get mesh failed: {json}", (int)response.StatusCode);

    var results = JsonSerializer.Deserialize<MeshAssetFull[]>(json);
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

    var path = $"/rest/v1/curve_sets?id=eq.{id}&select=id,name,geometry_data,geometry_url";
    var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    if (!response.IsSuccessStatusCode)
        throw new SelvagenApiException($"Get curve set failed: {json}", (int)response.StatusCode);

    var results = JsonSerializer.Deserialize<CurveSetAssetFull[]>(json);
    if (results == null || results.Length == 0)
        throw new SelvagenApiException($"Curve set not found: {id}", 404);

    return results[0];
}

/// <summary>
/// Fetch a single text 3D set including its text data.
/// </summary>
public async Task<Text3DSetAssetFull> GetText3DSetAsync(string id)
{
    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

    var path = $"/rest/v1/text_3d_sets?id=eq.{id}&select=id,name,text_data,geometry_url";
    var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    if (!response.IsSuccessStatusCode)
        throw new SelvagenApiException($"Get text 3D set failed: {json}", (int)response.StatusCode);

    var results = JsonSerializer.Deserialize<Text3DSetAssetFull[]>(json);
    if (results == null || results.Length == 0)
        throw new SelvagenApiException($"Text 3D set not found: {id}", 404);

    return results[0];
}

/// <summary>
/// Fetch animation sequence metadata.
/// </summary>
public async Task<AnimationSequenceFull> GetAnimationSequenceInfoAsync(string id)
{
    if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

    var path = $"/rest/v1/animation_sequences?id=eq.{id}&select=id,name,fps,loop,base_asset_id,frame_count";
    var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    if (!response.IsSuccessStatusCode)
        throw new SelvagenApiException($"Get animation sequence failed: {json}", (int)response.StatusCode);

    var results = JsonSerializer.Deserialize<AnimationSequenceFull[]>(json);
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

    var path = $"/rest/v1/animation_frames?sequence_id=eq.{sequenceId}&select=frame_index,geometry_data,label&order=frame_index";
    var response = await SendAuthorizedAsync(HttpMethod.Get, path).ConfigureAwait(false);
    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    if (!response.IsSuccessStatusCode)
        throw new SelvagenApiException($"Get animation frames failed: {json}", (int)response.StatusCode);

    return JsonSerializer.Deserialize<AnimationFrameFull[]>(json) ?? new AnimationFrameFull[0];
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.Core\Selvagen.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.Core/Api/SelvagenClient.cs
git commit -m "feat(api): add download methods to SelvagenClient"
```

---

### Task 8: Create SelvagenDownloadComponentBase

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenDownloadComponentBase.cs`

- [ ] **Step 1: Create the base class**

Create `src/Selvagen.GH/Components/SelvagenDownloadComponentBase.cs`:

```csharp
using System;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public abstract class SelvagenDownloadComponentBase : GH_Component
    {
        protected SelvagenDownloadComponentBase(string name, string nickname, string description)
            : base(name, nickname, description, "Selvagen", "08 Assets") { }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => null;
    }
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenDownloadComponentBase.cs
git commit -m "feat(ui): add SelvagenDownloadComponentBase with secondary exposure"
```

---

### Task 9: Create Download Mesh component

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenDownloadMeshComponent.cs`

- [ ] **Step 1: Generate a GUID**

```powershell
[guid]::NewGuid()
```

Use the output as `ComponentGuid` in the next step.

- [ ] **Step 2: Create the component**

Create `src/Selvagen.GH/Components/SelvagenDownloadMeshComponent.cs` (replace the GUID with the one generated in step 1):

```csharp
using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadMeshComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private Rhino.Geometry.Mesh _cachedMesh;
        private string _cachedName;

        public SelvagenDownloadMeshComponent()
            : base("Download Mesh", "SvDnMesh",
                "Download a mesh from the platform as Rhino geometry. [Download de Malha]")
        { }

        public override Guid ComponentGuid => new Guid("REPLACE_WITH_GENERATED_GUID");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Mesh asset ID (from List Assets)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Downloaded Rhino mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Asset name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(assetId))
            {
                DA.SetData(2, "Provide an Asset ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(2, "Not logged in.");
                return;
            }

            if (assetId == _cachedId && _cachedMesh != null)
            {
                DA.SetData(0, _cachedMesh);
                DA.SetData(1, _cachedName);
                DA.SetData(2, $"Cached: {_cachedName}");
                return;
            }

            try
            {
                var asset = Task.Run(() => client.GetMeshAsync(assetId)).GetAwaiter().GetResult();

                if (asset.GeometryData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh has no inline geometry data.");
                    DA.SetData(2, "No geometry data.");
                    return;
                }

                var mesh = MeshConverter.FromBufferGeometry(asset.GeometryData);

                _cachedId = assetId;
                _cachedMesh = mesh;
                _cachedName = asset.Name;

                DA.SetData(0, mesh);
                DA.SetData(1, asset.Name);
                DA.SetData(2, $"Downloaded: {asset.Name}");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(2, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadMesh");
    }
}
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenDownloadMeshComponent.cs
git commit -m "feat(ui): add Download Mesh component"
```

---

### Task 10: Create Download Curves component

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenDownloadCurvesComponent.cs`

- [ ] **Step 1: Generate a GUID**

```powershell
[guid]::NewGuid()
```

- [ ] **Step 2: Create the component**

Create `src/Selvagen.GH/Components/SelvagenDownloadCurvesComponent.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadCurvesComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private List<PolylineCurve> _cachedCurves;
        private List<Color> _cachedColors;
        private List<double> _cachedLinewidths;
        private string _cachedName;

        public SelvagenDownloadCurvesComponent()
            : base("Download Curves", "SvDnCrv",
                "Download a curve set from the platform as Rhino curves. [Download de Curvas]")
        { }

        public override Guid ComponentGuid => new Guid("REPLACE_WITH_GENERATED_GUID");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Curve set asset ID (from List Assets)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "Crv", "Downloaded Rhino curves", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "Per-curve color", GH_ParamAccess.list);
            pManager.AddNumberParameter("Linewidths", "W", "Per-curve line width", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Asset name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(assetId))
            {
                DA.SetData(4, "Provide an Asset ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(4, "Not logged in.");
                return;
            }

            if (assetId == _cachedId && _cachedCurves != null)
            {
                DA.SetDataList(0, _cachedCurves);
                DA.SetDataList(1, _cachedColors);
                DA.SetDataList(2, _cachedLinewidths);
                DA.SetData(3, _cachedName);
                DA.SetData(4, $"Cached: {_cachedName} ({_cachedCurves.Count} curves)");
                return;
            }

            try
            {
                var asset = Task.Run(() => client.GetCurveSetAsync(assetId)).GetAwaiter().GetResult();

                if (asset.GeometryData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Curve set has no inline geometry data.");
                    DA.SetData(4, "No geometry data.");
                    return;
                }

                CurveConverter.FromCurveSet(asset.GeometryData,
                    out var curves, out var colors, out var linewidths);

                _cachedId = assetId;
                _cachedCurves = curves;
                _cachedColors = colors;
                _cachedLinewidths = linewidths;
                _cachedName = asset.Name;

                DA.SetDataList(0, curves);
                DA.SetDataList(1, colors);
                DA.SetDataList(2, linewidths);
                DA.SetData(3, asset.Name);
                DA.SetData(4, $"Downloaded: {asset.Name} ({curves.Count} curves)");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(4, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadCurves");
    }
}
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenDownloadCurvesComponent.cs
git commit -m "feat(ui): add Download Curves component"
```

---

### Task 11: Create Download Labels component

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs`

- [ ] **Step 1: Generate a GUID**

```powershell
[guid]::NewGuid()
```

- [ ] **Step 2: Create the component**

Create `src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadLabelsComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private List<Plane> _cachedPlanes;
        private List<string> _cachedTexts;
        private List<Color> _cachedColors;
        private List<double> _cachedFontSizes;
        private string _cachedName;

        public SelvagenDownloadLabelsComponent()
            : base("Download Labels", "SvDnLbl",
                "Download a label set from the platform as Rhino planes and text. [Download de Rótulos 3D]")
        { }

        public override Guid ComponentGuid => new Guid("REPLACE_WITH_GENERATED_GUID");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Text 3D set asset ID (from List Assets)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Planes", "Pl", "Label placement planes (position + rotation)", GH_ParamAccess.list);
            pManager.AddTextParameter("Texts", "Txt", "Label text strings", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "Per-label color", GH_ParamAccess.list);
            pManager.AddNumberParameter("Font Sizes", "Fs", "Per-label font size", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Asset name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(assetId))
            {
                DA.SetData(5, "Provide an Asset ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(5, "Not logged in.");
                return;
            }

            if (assetId == _cachedId && _cachedPlanes != null)
            {
                DA.SetDataList(0, _cachedPlanes);
                DA.SetDataList(1, _cachedTexts);
                DA.SetDataList(2, _cachedColors);
                DA.SetDataList(3, _cachedFontSizes);
                DA.SetData(4, _cachedName);
                DA.SetData(5, $"Cached: {_cachedName} ({_cachedPlanes.Count} labels)");
                return;
            }

            try
            {
                var asset = Task.Run(() => client.GetText3DSetAsync(assetId)).GetAwaiter().GetResult();

                if (asset.TextData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Label set has no inline text data.");
                    DA.SetData(5, "No text data.");
                    return;
                }

                TextConverter.FromText3DSet(asset.TextData,
                    out var planes, out var texts, out var colors, out var fontSizes);

                _cachedId = assetId;
                _cachedPlanes = planes;
                _cachedTexts = texts;
                _cachedColors = colors;
                _cachedFontSizes = fontSizes;
                _cachedName = asset.Name;

                DA.SetDataList(0, planes);
                DA.SetDataList(1, texts);
                DA.SetDataList(2, colors);
                DA.SetDataList(3, fontSizes);
                DA.SetData(4, asset.Name);
                DA.SetData(5, $"Downloaded: {asset.Name} ({planes.Count} labels)");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(5, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadLabels");
    }
}
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs
git commit -m "feat(ui): add Download Labels component"
```

---

### Task 12: Create Download Animation component

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenDownloadAnimationComponent.cs`

- [ ] **Step 1: Generate a GUID**

```powershell
[guid]::NewGuid()
```

- [ ] **Step 2: Create the component**

Create `src/Selvagen.GH/Components/SelvagenDownloadAnimationComponent.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadAnimationComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private List<Mesh> _cachedMeshes;
        private List<string> _cachedLabels;
        private double _cachedFps;
        private bool _cachedLoop;
        private string _cachedName;

        public SelvagenDownloadAnimationComponent()
            : base("Download Animation", "SvDnAnim",
                "Download an animation sequence from the platform as a list of meshes. [Download de Animação]")
        { }

        public override Guid ComponentGuid => new Guid("REPLACE_WITH_GENERATED_GUID");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Sequence ID", "SeqID", "Animation sequence ID (from List Assets)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Meshes", "M", "Per-frame meshes", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Lbl", "Per-frame labels", GH_ParamAccess.list);
            pManager.AddNumberParameter("FPS", "F", "Frames per second", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Loop", "L", "Loop playback", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Sequence name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string seqId = "";
            DA.GetData(0, ref seqId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(seqId))
            {
                DA.SetData(5, "Provide a Sequence ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(5, "Not logged in.");
                return;
            }

            if (seqId == _cachedId && _cachedMeshes != null)
            {
                DA.SetDataList(0, _cachedMeshes);
                DA.SetDataList(1, _cachedLabels);
                DA.SetData(2, _cachedFps);
                DA.SetData(3, _cachedLoop);
                DA.SetData(4, _cachedName);
                DA.SetData(5, $"Cached: {_cachedName} ({_cachedMeshes.Count} frames)");
                return;
            }

            try
            {
                var sequence = Task.Run(() => client.GetAnimationSequenceInfoAsync(seqId))
                    .GetAwaiter().GetResult();

                var baseMeshAsset = Task.Run(() => client.GetMeshAsync(sequence.BaseAssetId))
                    .GetAwaiter().GetResult();

                Mesh baseMesh = null;
                if (baseMeshAsset.GeometryData != null)
                    baseMesh = MeshConverter.FromBufferGeometry(baseMeshAsset.GeometryData);

                var frames = Task.Run(() => client.GetAnimationFramesAsync(seqId))
                    .GetAwaiter().GetResult();

                var meshes = new List<Mesh>();
                var labels = new List<string>();

                foreach (var frame in frames)
                {
                    if (frame.GeometryData == null)
                    {
                        meshes.Add(null);
                        labels.Add(frame.Label ?? "");
                        continue;
                    }

                    Mesh frameMesh;
                    if (frame.GeometryData.Format == "buffer_geometry" && frame.GeometryData.Geometry != null)
                    {
                        frameMesh = MeshConverter.FromBufferGeometry(frame.GeometryData.Geometry);
                    }
                    else if (frame.GeometryData.Positions != null && baseMesh != null)
                    {
                        frameMesh = baseMesh.DuplicateMesh();
                        var positions = frame.GeometryData.Positions;
                        int vertCount = Math.Min(frameMesh.Vertices.Count, positions.Length / 3);
                        for (int v = 0; v < vertCount; v++)
                        {
                            var pt = CoordinateHelper.FromYUp(positions, v * 3);
                            frameMesh.Vertices.SetVertex(v, pt);
                        }
                        frameMesh.Normals.ComputeNormals();
                    }
                    else
                    {
                        frameMesh = null;
                    }

                    meshes.Add(frameMesh);
                    labels.Add(frame.Label ?? "");
                }

                _cachedId = seqId;
                _cachedMeshes = meshes;
                _cachedLabels = labels;
                _cachedFps = sequence.Fps ?? 1.0;
                _cachedLoop = sequence.Loop ?? false;
                _cachedName = sequence.Name;

                DA.SetDataList(0, meshes);
                DA.SetDataList(1, labels);
                DA.SetData(2, _cachedFps);
                DA.SetData(3, _cachedLoop);
                DA.SetData(4, sequence.Name);
                DA.SetData(5, $"Downloaded: {sequence.Name} ({meshes.Count} frames)");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(5, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadAnimation");
    }
}
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenDownloadAnimationComponent.cs
git commit -m "feat(ui): add Download Animation component"
```

---

### Task 13: Build, deploy, and verify in Rhino

**Files:**
- No new code — integration test only.

- [ ] **Step 1: Full build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj --configuration Release
```

Expected: Build succeeded with no errors or warnings.

- [ ] **Step 2: Deploy to Grasshopper Libraries**

```powershell
$src = "src\Selvagen.GH\bin\Release\net7.0"
$dest = "$env:APPDATA\Grasshopper\Libraries\Selvagen"
if (!(Test-Path $dest)) { New-Item -ItemType Directory -Path $dest }
Copy-Item "$src\*" $dest -Recurse -Force -Confirm:$false
```

- [ ] **Step 3: Restart Rhino and verify**

Close and reopen Rhino + Grasshopper. Verify:

1. **Toolbar layout**: In the Selvagen tab → "08 Assets" group, Upload components appear first, then a separator line, then Download components, then Delete.
2. **Icons**: Upload icons now have a small up-arrow badge. Download icons have the same base with a down-arrow badge.
3. **Download Mesh**: Wire a List Assets (filter: Meshes) → select a mesh → wire Asset ID into Download Mesh. Verify a Rhino Mesh appears on the output, and the mesh geometry looks correct (not flipped/mirrored).
4. **Download Curves**: Wire a List Assets (filter: Curve Sets) → Download Curves. Verify curves, colors, and linewidths appear.
5. **Download Labels**: Wire a List Assets (filter: Text 3D Sets) → Download Labels. Verify planes, texts, colors appear. Connect planes to Upload Labels input to confirm round-trip compatibility.
6. **Download Animation**: Wire a List Assets (filter: Animation Sequences) → Download Animation. Verify meshes list, FPS, and Loop outputs.
7. **Caching**: Re-trigger the canvas (e.g., toggle a boolean). Verify the Status output shows "Cached:" on the second solve.
8. **Error state**: Disconnect the Asset ID wire. Verify the component shows "Provide an Asset ID." in Status.
