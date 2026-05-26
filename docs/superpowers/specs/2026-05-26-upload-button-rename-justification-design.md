# Upload Button, Label Sets Rename, and Justification

**Date:** 2026-05-26
**Status:** Draft

## Summary

Three related changes to the Selvagen Grasshopper plugin:
1. Replace the wired boolean `Upload` input with an on-canvas "Upload" button (one-shot)
2. Rename "Text 3D Sets" to "Label Sets" across plugin, database, and platform
3. Add a `Justification` property to label sets, mirroring Grasshopper's Text Tag 3D alignment

## Change 1: Integrated Upload Button

### Current State

Upload components (Mesh, Curves, Labels, Animation) require a `Boolean Toggle` wired to the `Upload` / `Go` input. The toggle stays armed after upload — any canvas recompute re-fires the upload unless the user manually resets it.

### Target State

Remove the `Upload` boolean input parameter. Render an on-canvas **"Upload"** button directly on the component (same pattern as the "Update" button on `SelvagenSelectableComponentBase`). The button is **one-shot**: click fires the upload once, then auto-resets. No accidental re-uploads on canvas recompute.

### Implementation

**New class: `SelvagenUploadAttributes`** (custom `GH_ComponentAttributes`)
- Renders a rounded "Upload" button at the bottom of the component
- Button color: distinct from Update (use a green/teal accent to convey "push/send")
- On click: sets `_uploadRequested = true` on the component, calls `ExpireSolution(true)`
- 100ms press animation (same as Update button)

**Modified: `SelvagenUploadComponentBase`**
- Add `_uploadRequested` flag (default `false`)
- Add `RequestUpload()` method (sets flag, expires solution)
- Override `CreateAttributes()` to use `SelvagenUploadAttributes`
- Remove the `Upload` / `Go` boolean input from all upload components

**Component lifecycle state machine:**

```
State: IDLE ("Ready to upload.")
  │
  ├── [Click Upload Button]
  │     → _uploadRequested = true
  │     → ExpireSolution(true)
  │
  ▼
State: UPLOADING ("Uploading...")
  │   (SolveInstance runs, _uploadRequested is true)
  │   → Immediately reset _uploadRequested = false (prevents re-trigger)
  │   → Execute upload via Task.Run(...).GetAwaiter().GetResult()
  │
  ├── [Success] → State: IDLE, Status = "Uploaded: {name}"
  └── [Error]   → State: IDLE, Status = "Error: {message}"
```

**Threading note:** The upload executes synchronously on the solver thread via `Task.Run(...).GetAwaiter().GetResult()`, matching the existing pattern used by all other components in this plugin. This briefly freezes the UI during upload — acceptable for the current internal use case. Migrating to `GH_TaskCapableComponent` for true async is out of scope.

**Key invariant:** `_uploadRequested` is reset to `false` at the START of the upload branch in `SolveInstance`, before the async call. This ensures that even if the upload throws or the solver re-enters, the flag is already cleared and won't re-trigger.

**Visual feedback before blocking call:** Because the upload blocks the UI thread, the component must force a canvas repaint to show "Uploading..." state *before* entering the blocking call. Call `Grasshopper.Instances.ActiveCanvas?.Refresh()` (or `OnDisplayExpired(false)`) immediately after resetting the flag and before the `Task.Run(...)` call. Without this, the button appears unpressed during the freeze and users may think the app crashed.

**Modified: Each upload component's `SolveInstance`**
- Replace `if (!upload || ...)` guard with `if (!_uploadRequested || ...)`
- Reset `_uploadRequested = false` immediately upon entering the upload branch
- Idle state output: `"Ready to upload."` instead of `"Waiting..."`

**Modified: Each upload component's `RegisterInputParams`**
- Remove the `AddBooleanParameter("Upload", "Go", ...)` line
- Adjust `DA.GetData` indices accordingly

### Breaking Change

Existing `.gh` files that wire a Boolean Toggle to the `Go` input will lose that connection. The component will still work — users just click the on-canvas button instead. This is an intentional UX improvement.

## Change 2: Rename Text 3D Sets → Label Sets

### Scope

The name "Text 3D Sets" is misleading — the data is 2D text positioned with a plane, not 3D geometry. Rename to "Label Sets" everywhere.

### Plugin Changes (this repo)

**Models:**
- `Text3DSet` → `LabelSet` (file: `Text3DSet.cs` → `LabelSet.cs`)
- `Text3DSetAssetFull` → `LabelSetAssetFull` (in `ApiResponses.cs`)
- JSON property names stay the same (`labels`, `text`, `position`, etc.) — no serialization change

**Converters:**
- `TextConverter` → `LabelConverter` (file: `TextConverter.cs` → `LabelConverter.cs`)
- `FromPlanesAndTexts()` → `ToLabelSet()`
- `FromPointsAndTexts()` → `ToLabelSetFromPoints()`
- `FromTextDots()` → `ToLabelSetFromDots()`
- `FromText3DSet()` → `FromLabelSet()`

**Client methods:**
- `UploadText3DAsync()` → `UploadLabelSetAsync()`
- `ListText3DSetsAsync()` → `ListLabelSetsAsync()` — REST path: `/rest/v1/label_sets`
- `GetText3DSetAsync()` → `GetLabelSetAsync()` — REST path: `/rest/v1/label_sets`

**Components:**
- `SelvagenUploadLabelsComponent` — update internal references (class name stays same)
- `SelvagenDownloadLabelsComponent` — update internal references
- `SelvagenListAssetsComponent` — filter option `"text_3d_sets"` → `"label_sets"`, display `"Text 3D Sets"` → `"Label Sets"`

**Icons:**
- No icon changes needed — already named `UploadLabels.png` / `DownloadLabels.png`

### Database Migration (Supabase)

```sql
-- Rename table
ALTER TABLE text_3d_sets RENAME TO label_sets;

-- Rename FK columns in referencing tables
ALTER TABLE topography RENAME COLUMN contours_text_3d_set_id TO contours_label_set_id;
ALTER TABLE analyses RENAME COLUMN access_text_3d_set_id TO access_label_set_id;
ALTER TABLE analyses RENAME COLUMN rock_text_3d_set_height_id TO rock_label_set_height_id;
ALTER TABLE analyses RENAME COLUMN rock_text_3d_set_vol_id TO rock_label_set_vol_id;
ALTER TABLE optimizations RENAME COLUMN access_text_3d_set_id TO access_label_set_id;
```

The migration must explicitly notify PostgREST to reload its schema cache:

```sql
NOTIFY pgrst, 'reload schema';
```

Without this, PostgREST will continue serving the old table name and return 404s on `/rest/v1/label_sets` until the next container restart. FK constraints follow the column rename automatically.

### Platform Changes (handoff doc)

The web application references `text_3d_sets` in:
- Database queries and type definitions
- `Text3DSetLoader` component/module
- API routes and edge function names
- UI labels and component names

A handoff document will be generated listing every reference to update.

### Edge Function

The upload edge function `plugin-upload-text3d` should be renamed to `plugin-upload-labels`. This is a breaking change — the plugin and platform are deployed together by the same internal team, so coordinated release is sufficient. No deprecation layer is needed, but the handoff doc must flag the edge function rename so both sides deploy simultaneously.

## Change 3: Justification Property

### Design Principle

The Grasshopper integer (0-8) is a GH-specific enum. The database and platform should **not** store this integer. Instead, the converter maps the integer to explicit `anchorX`/`anchorY` string values before serialization. This keeps the JSON schema platform-agnostic — the web app reads `anchorX: "center", anchorY: "top"` without needing to know Grasshopper's enum.

### Data Flow

```
GH Input (int 0-8) → LabelConverter → JSON { "anchorX": "center", "anchorY": "top" }
JSON { "anchorX": "center", "anchorY": "top" } → LabelConverter → GH Output (int 0-8)
```

The `LabelData` C# model already has `AnchorX` and `AnchorY` string properties — no new fields needed on the model. The `Justification` integer exists only as a GH component input/output parameter, never in the serialized JSON.

### Justification Mapping

Grasshopper's Text Tag 3D justification is an integer 0-8:

| Value | Description | anchorX | anchorY |
|-------|-------------|---------|---------|
| 0 | Bottom Left | `left` | `bottom` |
| 1 | Bottom Center | `center` | `bottom` |
| 2 | Bottom Right | `right` | `bottom` |
| 3 | Middle Left | `left` | `middle` |
| 4 | Middle Center | `center` | `middle` |
| 5 | Middle Right | `right` | `middle` |
| 6 | Top Left | `left` | `top` |
| 7 | Top Center | `center` | `top` |
| 8 | Top Right | `right` | `top` |

### LabelConverter Mapping Methods

New static methods in `LabelConverter`:

- `JustificationToAnchors(int justification) → (string anchorX, string anchorY)` — used by upload path. Clamps input to 0-8.
- `AnchorsToJustification(string anchorX, string anchorY) → int` — used by download path. Handles partial nulls:

| anchorX | anchorY | Result | Note |
|---------|---------|--------|------|
| `"left"` | `"bottom"` | `0` | Exact match |
| `null` | `"bottom"` | `1` | Missing X defaults to center |
| `"right"` | `null` | `5` | Missing Y defaults to middle |
| `null` | `null` | `4` | System default (Middle Center) |
| unknown | unknown | `4` | Unrecognized strings default to center |

### Upload Labels Component

Add optional `Justification` input (`GH_ParamAccess.list`, integer). Uses the same last-value-repeating pattern as the existing `Color` input: `justifications[Math.Min(index, justifications.Count - 1)]`. The converter calls `JustificationToAnchors()` to set `anchorX`/`anchorY` on each `LabelData`. If the list is empty or not provided, defaults to `4` (Middle Center) which matches GH default.

**Note on `GH_ParamAccess.list` (not `.item`):** The upload component processes ALL labels in a single `SolveInstance` call to create one `LabelSet` for upload. Using `.item` would cause GH to call `SolveInstance` once per label, uploading separate single-label sets — completely wrong. The `.list` access with manual index matching is the established pattern in this plugin (see Color/Thickness on Upload Curves, Color on Upload Labels).

### Download Labels Component

Add `Justification` output (`GH_ParamAccess.list`, integer). The reverse converter calls `AnchorsToJustification()` to map `anchorX`/`anchorY` strings back to the integer.

### Database

No schema migration needed. The `anchorX`/`anchorY` strings are already part of the JSONB schema inside the `text_data` column. Adding values to previously-null fields is backward compatible.

## Files Changed (Plugin)

| File | Change |
|------|--------|
| `Selvagen.Core/Models/Text3DSet.cs` → `LabelSet.cs` | Rename class (no new properties — `AnchorX`/`AnchorY` already exist) |
| `Selvagen.Core/Models/ApiResponses.cs` | Rename `Text3DSetAssetFull` → `LabelSetAssetFull` |
| `Selvagen.Core/Converters/TextConverter.cs` → `LabelConverter.cs` | Rename class/methods, add justification mapping |
| `Selvagen.Core/Api/SelvagenClient.cs` | Rename methods, update REST paths |
| `Selvagen.GH/Components/SelvagenUploadComponentBase.cs` | Add `_uploadRequested` flag, `RequestUpload()`, `CreateAttributes()` |
| `Selvagen.GH/Components/SelvagenUploadAttributes.cs` | New: on-canvas Upload button |
| `Selvagen.GH/Components/SelvagenUploadMeshComponent.cs` | Remove `Go` input, use flag |
| `Selvagen.GH/Components/SelvagenUploadCurvesComponent.cs` | Remove `Go` input, use flag |
| `Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs` | Remove `Go` input, add Justification input |
| `Selvagen.GH/Components/SelvagenUploadAnimationComponent.cs` | Remove `Go` input, use flag |
| `Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs` | Add Justification output |
| `Selvagen.GH/Components/SelvagenListAssetsComponent.cs` | Rename filter option |
| `Selvagen.GH/Components/SelvagenDeleteAssetComponent.cs` | Update table name reference if any |

## Platform Handoff Document

After implementation, a handoff document will be generated at `docs/handoff-label-sets-rename.md` listing:
1. Every `text_3d_sets` / `text_3d` / `Text3D` reference in the web platform to rename
2. The edge function rename (`plugin-upload-text3d` → `plugin-upload-labels`)
3. The new `justification` field in the JSON schema
4. The justification → anchorX/anchorY mapping table

## Out of Scope

- Renaming the JSONB field `text_data` inside the table to something else (e.g., `label_data`) — would require data migration, not worth it
- Adding justification to animation frames
- Backward compatibility shim for old `text_3d_sets` REST path
