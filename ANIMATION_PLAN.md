# Animation Pipeline - Implementation Plan

> **Status:** Implemented (Phases 1-4 complete)
> **Scope:** Grasshopper plugin (selvagen-gh) + Supabase Edge Functions + Platform renderer (selvagen)
> **Prerequisite:** P0 and P1 items from PRODUCT_ANALYSIS.md (done)

---

## 1. Problem Statement

Engineers use Grasshopper to produce sequential geometry -- earthworks cut stages, terrain optimization iterations, geological layer reveals. Today these are exported as static snapshots. The goal is to let them "record" a sequence of meshes from Grasshopper and play them back as a smooth animation on the Selvagen web platform.

**Typical use case:**
- A Grasshopper slider goes from 0 to 50
- Each step produces a terrain mesh (~500 faces, ~300 vertices)
- The engineer wants this to play as a 2-second animation on the platform at 25 fps

---

## 2. Strategy Analysis

### Strategy A: Full Mesh Per Frame

Store a complete Three.js BufferGeometry (positions + normals + indices) for every frame.

| Aspect | Assessment |
|--------|-----------|
| **Storage** | ~25-30 KB/frame for 300-vertex mesh (JSON). 50 frames = ~1.5 MB |
| **Upload** | Simple -- reuse existing `MeshConverter.ToBufferGeometry()` per frame |
| **Playback** | Swap entire geometry each frame. No interpolation possible without extra work |
| **Flexibility** | Handles topology changes between frames (vertex count, face count can differ) |
| **Complexity** | Lowest. Plugin just loops and uploads |

### Strategy B: Base Mesh + Position-Only Frames (Recommended)

Store the full mesh (indices + normals) once as a "base asset". Each frame stores only the position array. Normals are recomputed on the fly.

| Aspect | Assessment |
|--------|-----------|
| **Storage** | Base: ~25 KB + ~7 KB/frame (positions only). 50 frames = ~375 KB (75% smaller than A) |
| **Upload** | Plugin extracts position arrays after converting to Y-up. Slightly more logic |
| **Playback** | Swap `position` attribute buffer, call `computeVertexNormals()`. Supports CPU-lerp between frames for smooth interpolation |
| **Flexibility** | Requires same topology across all frames (same vertex count + index buffer). Falls back to Strategy A if topology changes |
| **Complexity** | Medium. Well-supported by Three.js (`BufferAttribute.needsUpdate = true`) |

### Strategy C: Morph Targets

Upload all frame positions as Three.js `morphAttributes.position` on a single geometry. Use `morphTargetInfluences` for GPU-accelerated interpolation.

| Aspect | Assessment |
|--------|-----------|
| **Storage** | Same as B (~7 KB/frame for positions) |
| **Upload** | All frames must be loaded as a single geometry blob. Large payload |
| **Playback** | GPU-interpolated, smoothest possible. Native Three.js support |
| **Flexibility** | Hard limit: all frames must share topology. All frames loaded into GPU memory at once. WebGL limits number of active morph targets (~8 simultaneous) |
| **Complexity** | Higher. Requires building the morph target array on the frontend. Doesn't map well to the per-frame DB schema |

### Strategy D: Vertex Animation Texture (VAT)

Encode all vertex positions across all frames into a DataTexture. Custom vertex shader reads position from texture based on time.

| Aspect | Assessment |
|--------|-----------|
| **Storage** | Most compact: RGBA float texture. ~4.8 KB/frame for 300 vertices (binary) |
| **Playback** | Fastest possible -- entirely GPU |
| **Flexibility** | Same topology constraint. Precision limited by texture format |
| **Complexity** | Highest. Requires custom shader material. Breaks MeshStandardMaterial compatibility (no PBR lighting). Not worth it for ~500-face meshes |

### Recommendation: Strategy B with Strategy A fallback

**Strategy B (Base Mesh + Position-Only Frames)** is the sweet spot:

1. **75% storage savings** over full-mesh-per-frame
2. **Smooth CPU-lerp interpolation** between frames (trivially cheap for 300 vertices)
3. **Maps perfectly to the existing DB schema** (`animation_sequences.base_asset_id` + `animation_frames.geometry_data`)
4. **Simple Three.js rendering** -- just update `position` attribute + `computeVertexNormals()`
5. **Graceful fallback** -- if topology changes, a frame can store a full BufferGeometry instead of just positions, and the renderer detects which format it got

---

## 3. Data Format Specification

### 3.1 Animation Sequence (DB: `animation_sequences`)

Uses the existing table as-is:

```
{
  id:            UUID (auto)
  project_id:    UUID (required)
  name:          string (e.g., "Earthworks Cut Stages")
  asset_type:    "mesh"                          -- for now, mesh-only
  base_asset_id: UUID -> meshes.id               -- the base mesh (full BufferGeometry)
  frame_count:   integer                         -- total number of frames
  fps:           number (default: 1.0)           -- playback speed
  loop:          boolean (default: false)         -- loop playback
  metadata:      JSON (optional)                 -- e.g., { "slider_name": "Cut Level" }
}
```

### 3.2 Animation Frame (DB: `animation_frames`)

Each frame's `geometry_data` stores one of two formats:

**Format 1: Position-only (default, used when topology matches base)**
```json
{
  "format": "positions",
  "positions": [x0, y0, z0, x1, y1, z1, ...],
  "label": "Step 12: Fill complete"
}
```

- `positions` array length MUST equal base mesh position array length
- Coordinates are in Y-up (already converted by the plugin)
- `label` is optional (duplicated in DB column for indexing)

**Format 2: Full BufferGeometry (fallback, used when topology changes)**
```json
{
  "format": "buffer_geometry",
  "geometry": {
    "metadata": { ... },
    "type": "BufferGeometry",
    "data": {
      "attributes": {
        "position": { "itemSize": 3, "type": "Float32Array", "array": [...] },
        "normal":   { "itemSize": 3, "type": "Float32Array", "array": [...] }
      },
      "index": { "type": "Uint16Array", "array": [...] }
    }
  }
}
```

The renderer checks `geometry_data.format` to decide how to handle each frame:
- `"positions"` -> swap position buffer on existing geometry
- `"buffer_geometry"` -> replace the entire geometry

### 3.3 Base Mesh

The base mesh is a standard entry in the `meshes` table, uploaded via the existing `plugin-upload-mesh` edge function. It stores the full BufferGeometry (positions, normals, indices) and serves as:
- The initial frame geometry (topology + positions)
- The shared index buffer for all position-only frames

---

## 4. Implementation Plan

### Phase 1: Plugin Core (Selvagen.Core)

#### 4.1 New Model: `AnimationFrameData`

```
File: src/Selvagen.Core/Models/AnimationFrameData.cs
```

```csharp
public class AnimationFrameData
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = "positions";

    [JsonPropertyName("positions")]
    public double[] Positions { get; set; }
}
```

Minimal model. The "positions" format stores only the Y-up converted vertex positions.

#### 4.2 New Converter: `AnimationConverter`

```
File: src/Selvagen.Core/Converters/AnimationConverter.cs
```

Responsibilities:
- Accept a list of Rhino Meshes (one per frame)
- Validate they share topology (same vertex count)
- Convert the first mesh to a full BufferGeometry (the base)
- Convert remaining meshes to position-only arrays (Y-up)
- If topology differs, fall back to full BufferGeometry per frame

Key method:
```csharp
public static AnimationConversionResult Convert(IList<Mesh> frames)
```

Returns:
```csharp
public class AnimationConversionResult
{
    public BufferGeometry BaseMesh { get; set; }
    public AnimationFrameData[] Frames { get; set; }
    public bool TopologyConsistent { get; set; }
}
```

#### 4.3 New API Methods in `SelvagenClient`

```csharp
// Create a sequence record and get back the sequence ID
Task<AnimationSequenceResult> CreateAnimationSequenceAsync(
    string projectId, string name, string baseMeshId,
    int frameCount, double fps, bool loop)

// Upload a batch of frames for a sequence
Task UploadAnimationFramesAsync(
    string sequenceId, AnimationFrameData[] frames)

// Upload frames individually (for progress tracking)
Task UploadAnimationFrameAsync(
    string sequenceId, int frameIndex, AnimationFrameData frameData, string label)
```

These use PostgREST directly (INSERT into `animation_sequences` and `animation_frames`).

### Phase 2: Plugin Components (Selvagen.GH)

#### 4.4 `SelvagenUploadAnimationComponent`

```
File: src/Selvagen.GH/Components/SelvagenUploadAnimationComponent.cs
Category: Selvagen > Upload
```

**Inputs:**
| Name | Type | Access | Description |
|------|------|--------|-------------|
| Client | Generic | item | Authenticated SelvagenClient |
| ProjectID | Text | item | Target project ID |
| Meshes | Mesh | list | List of meshes (one per frame, in order) |
| Name | Text | item | Animation name |
| FPS | Number | item | Frames per second (default: 1.0) |
| Loop | Boolean | item | Loop playback (default: false) |
| Upload | Boolean | item | Trigger upload |

**Outputs:**
| Name | Type | Description |
|------|------|-------------|
| SequenceID | Text | ID of the created animation sequence |
| Status | Text | Upload progress/status |

**Workflow:**
1. Validate inputs (non-empty mesh list, all meshes non-null)
2. Call `AnimationConverter.Convert(meshes)` to extract base mesh + frame positions
3. Upload base mesh via `UploadMeshAsync` -> get `baseMeshId`
4. Create animation sequence via `CreateAnimationSequenceAsync` -> get `sequenceId`
5. Upload frames in a loop via `UploadAnimationFrameAsync`
6. Output sequence ID and status

**Frame-by-frame approach matters** because:
- The user can see progress ("Uploading frame 23/50...")
- If upload fails mid-way, we know which frames succeeded
- Avoids a single massive JSON payload

### Phase 3: Supabase Edge Function

#### 4.5 `plugin-upload-animation` Edge Function

```
File: selvagen/supabase/functions/plugin-upload-animation/index.ts
Endpoint: POST /functions/v1/plugin-upload-animation
```

Alternatively, this can be done entirely via PostgREST (direct INSERT into `animation_sequences` and `animation_frames`), which avoids creating a new edge function. Since the plugin already has patterns for PostgREST calls (`CreateModuleRecordAsync`, `UpdateModulePropertyAsync`), **the PostgREST approach is simpler and consistent**.

However, if we want server-side validation of the frame format (checking position array lengths match the base mesh), an edge function is the right place. This can be added later as a refinement.

**Recommendation: Start with PostgREST, add validation edge function later.**

### Phase 4: Platform Renderer

#### 4.6 `AnimationSequenceLoader` Component

```
File: selvagen/src/components/AnimationSequenceLoader.tsx
```

A React Three Fiber component that:

1. **Loads the base mesh** from `animation_sequences.base_asset_id` -> `meshes.geometry_data`
2. **Fetches all frames** from `animation_frames` ordered by `frame_index`
3. **Pre-parses frame data** into typed arrays for fast buffer swaps
4. **Animates** using `useFrame()` hook:

```typescript
useFrame((_, delta) => {
  if (!playing) return

  time += delta
  const t = (time * fps) % frameCount
  const frameA = Math.floor(t)
  const frameB = (frameA + 1) % frameCount
  const alpha = t - frameA // 0..1 interpolation factor

  const posA = framePositions[frameA]
  const posB = framePositions[frameB]
  const positions = geometryRef.current.attributes.position.array

  // CPU lerp -- cheap for 300 vertices (~900 iterations)
  for (let i = 0; i < positions.length; i++) {
    positions[i] = posA[i] + (posB[i] - posA[i]) * alpha
  }

  geometryRef.current.attributes.position.needsUpdate = true
  geometryRef.current.computeVertexNormals()
})
```

**Performance for 300-vertex mesh:**
- Lerp loop: 900 float multiplies + adds = ~0.01ms per frame
- `computeVertexNormals`: ~500 face normal calculations = ~0.05ms per frame
- Total: ~0.06ms per frame. At 60fps rendering, this uses <0.4% of frame budget

#### 4.7 Playback Controls

A simple UI widget (play/pause, scrubber, speed control, loop toggle) shown when an animation layer is selected. This integrates with the existing slide layer system -- an `animation_sequence` is just another layer `asset_type`.

#### 4.8 Query Functions

```
File: selvagen/src/queries/animations.ts
```

```typescript
// Fetch animation sequence metadata
fetchAnimationSequence(sequenceId: string)

// Fetch all frames for a sequence (ordered by frame_index)
fetchAnimationFrames(sequenceId: string)
```

---

## 5. Grasshopper User Workflow

### Typical Canvas Setup

```
[Slider: 0 to 50]
    |
    v
[Grasshopper Definition]  -->  [Mesh Output]
    |                              |
    |                              v
    |                     [Data Recorder]  <-- records mesh per slider value
    |                              |
    |                              v
[Selvagen Login]          [List of 50 Meshes]
    |                              |
    v                              v
[Selvagen Upload Animation] <------+
    |
    v
  Sequence ID: "abc-123"
  Status: "Uploaded: 50/50 frames"
```

### Step-by-step:

1. **Build your definition** with a slider that drives geometry changes
2. **Use Grasshopper's Data Recorder** or a similar collector to capture the mesh at each slider position
3. **Wire the mesh list** into the `Selvagen Upload Animation` component
4. **Set FPS** (e.g., 25 for a 2-second animation of 50 frames)
5. **Toggle Upload** -- the component uploads the base mesh + all frames
6. **Open the platform** -- the animation plays in the project's 3D viewer

---

## 6. Size Estimates

For the typical case (300 vertices, 500 faces):

| Frames | Full Mesh (Strategy A) | Position-Only (Strategy B) | Savings |
|--------|----------------------|--------------------------|---------|
| 10 | 250 KB | 95 KB | 62% |
| 50 | 1.25 MB | 375 KB | 70% |
| 100 | 2.5 MB | 725 KB | 71% |
| 500 | 12.5 MB | 3.5 MB | 72% |

All comfortably within Supabase JSONB limits (max ~1GB per column) and PostgREST payload limits (default ~10MB per request). For the per-frame upload approach, individual payloads are ~7KB each.

---

## 7. Files to Create / Modify

### Selvagen.Core (plugin)

| File | Action | Purpose |
|------|--------|---------|
| `Models/AnimationFrameData.cs` | Create | Position-only frame model |
| `Models/ApiResponses.cs` | Modify | Add `AnimationSequenceResult` |
| `Converters/AnimationConverter.cs` | Create | Mesh[] -> base + frames conversion |
| `Api/SelvagenClient.cs` | Modify | Add animation create/upload methods |

### Selvagen.GH (plugin)

| File | Action | Purpose |
|------|--------|---------|
| `Components/SelvagenUploadAnimationComponent.cs` | Create | GH component for animation upload |

### Selvagen (platform) -- separate repo

| File | Action | Purpose |
|------|--------|---------|
| `src/components/AnimationSequenceLoader.tsx` | Create | Three.js animation renderer |
| `src/queries/animations.ts` | Create | TanStack Query functions |
| `src/routes/_app/$clientSlug.$projectSlug.tsx` | Modify | Add animation_sequence to LayerAwareAssets |
| `supabase/functions/_shared/validation.ts` | Modify | Add `validateAnimationFrame` |

---

## 8. Edge Cases and Considerations

### Topology Changes Mid-Animation

If a mesh gains/loses vertices between frames (e.g., mesh boolean operation), the converter detects the mismatch and falls back to full BufferGeometry per frame for those specific frames. The renderer checks `frame.format` and handles both paths.

### Large Frame Counts (500+)

For animations with many frames:
- Upload frames in batches of 50 to avoid connection timeouts
- Frontend lazy-loads frames (fetch first N, prefetch ahead of playback position)
- Consider adding a `geometry_url` path later for binary frame storage in Supabase Storage

### Curve and Label Animations

The `animation_sequences.asset_type` field supports `"curve_set"` and `"text_3d_set"`. The same pattern works:
- Base asset is a curve_set or text_3d_set
- Frames store position arrays for curves or updated positions for labels
- This is a natural extension once mesh animation works

### Scrubbing / Seeking

CPU-lerp supports instant seeking -- just compute the position for any arbitrary time value. No need to replay from frame 0 (unlike delta compression).

---

## 9. Implementation Order

1. **AnimationFrameData model + AnimationConverter** (Core) -- the data pipeline
2. **API methods in SelvagenClient** (Core) -- PostgREST calls
3. **SelvagenUploadAnimationComponent** (GH) -- the user-facing component
4. **Build + test plugin** -- verify uploads to DB
5. **AnimationSequenceLoader** (Platform) -- Three.js renderer
6. **Query functions + layer integration** (Platform) -- wire into UI
7. **Playback controls** (Platform) -- play/pause/scrub UI
