# Custom Properties Redesign

**Date:** 2026-05-28
**Status:** Draft

## Summary

Replace the existing module-scoped `Custom Properties` component (which PATCHes a free-form JSON column on one of four module tables) with a project-scoped key/value system backed by its own Supabase table. Ship three new GH components — `Upload Custom Property`, `List Custom Properties`, `Exclude Custom Property` — designed for non-expert users: typed key/value pairs, snake_case keys, in-canvas dropdowns and buttons, no JSON authoring.

## Motivation

The current `SelvagenPropertiesComponent` has three problems for non-expert users:

1. **JSON authoring required.** Users must hand-write valid JSON or build it via a C# Script — both are programming tasks.
2. **Destructive PATCH semantics.** The component PATCHes the entire `properties` column. Adding a second property tomorrow overwrites the first one from today unless the user re-sends the whole accumulated JSON.
3. **Module-scoped.** Properties belong to a module record (topography/geology/analyses/optimizations). There's no way to attach a property directly to a project, even though most "custom" data is project-level.

The redesign moves to dedicated rows in a project-scoped table, with key-based upsert semantics so each upload accumulates rather than overwrites.

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Value typing | `text` column + `value_type` tag (`text`/`number`/`boolean`) | Richer than text-only, simpler than JSONB |
| Duplicate key behavior | Upsert by `(project_id, key)` | Standard "property" semantics; accumulates without overwriting siblings |
| Upload input shape | List-by-default; one Type per Upload component | Batch in a single PostgREST call; same-type batches surface the "different datasets" distinction in the canvas |
| Delete identifier | `(project_id, key)` | Matches user mental model ("delete soil_ph from project X"); supports batch via `key=in.(…)` |
| Key format | snake_case enforced (`^[a-z][a-z0-9_]*$`) | Visual consistency with built-in fields like `topography.contour_height`; prevents free-form labels polluting platform-side display |
| Bad-key UX | Red Error + "did you mean: …?" suggestion; no auto-correct | Educates without surprising |
| Old component | Deleted entirely; `properties` column DROPped from all four module tables | Clean slate; no two-systems-doing-the-same-thing confusion |

## Database Schema (GEN.BOARD)

### New table: `custom_properties`

```sql
CREATE TABLE custom_properties (
  id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  key        TEXT NOT NULL,
  value      TEXT NOT NULL DEFAULT '',
  value_type TEXT NOT NULL DEFAULT 'text',
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

  CONSTRAINT custom_properties_key_format
    CHECK (key ~ '^[a-z][a-z0-9_]*$'),
  CONSTRAINT custom_properties_key_length
    CHECK (length(key) BETWEEN 1 AND 200),
  CONSTRAINT custom_properties_value_type_allowed
    CHECK (value_type IN ('text', 'number', 'boolean')),

  UNIQUE (project_id, key)
);

CREATE INDEX idx_custom_properties_project_id ON custom_properties (project_id);

-- updated_at trigger (same shape as other tables)
CREATE TRIGGER custom_properties_set_updated_at
  BEFORE UPDATE ON custom_properties
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- RLS: same pattern as meshes, curve_sets, label_sets, color_legends
ALTER TABLE custom_properties ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Project members can manage custom properties"
  ON custom_properties FOR ALL
  USING (project_id IN (SELECT accessible_project_ids()));
```

### Cleanup: drop the now-defunct columns

```sql
ALTER TABLE topography    DROP COLUMN IF EXISTS properties;
ALTER TABLE geology       DROP COLUMN IF EXISTS properties;
ALTER TABLE analyses      DROP COLUMN IF EXISTS properties;
ALTER TABLE optimizations DROP COLUMN IF EXISTS properties;

NOTIFY pgrst, 'reload schema';
```

Migration file: `docs/migrations/2026-05-28-add-custom-properties-table.sql`.

## Architecture

### Layer 1: Models (`Selvagen.Core/Models/`)

New file `CustomProperty.cs`:

```csharp
public class CustomPropertyInfo
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("project_id")] public string ProjectId { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; }
    [JsonPropertyName("value")] public string Value { get; set; }
    [JsonPropertyName("value_type")] public string ValueType { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
}

public class CustomPropertyUpsert
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; }
    [JsonPropertyName("key")]        public string Key { get; set; }
    [JsonPropertyName("value")]      public string Value { get; set; }
    [JsonPropertyName("value_type")] public string ValueType { get; set; }
}
```

### Layer 2: API methods (`Selvagen.Core/Api/SelvagenClient.cs`)

```csharp
// GET /rest/v1/custom_properties?project_id=eq.{id}&order=key
Task<CustomPropertyInfo[]> ListCustomPropertiesAsync(string projectId);

// POST /rest/v1/custom_properties?on_conflict=project_id,key
// Prefer: resolution=merge-duplicates,return=representation
// Body: array of CustomPropertyUpsert
Task<CustomPropertyInfo[]> UpsertCustomPropertiesAsync(
    string projectId,
    CustomPropertyUpsert[] properties);

// DELETE /rest/v1/custom_properties?project_id=eq.{id}&key=in.(k1,k2,…)
// Prefer: count=exact
// Returns the count of rows actually deleted (from Content-Range header)
Task<int> DeleteCustomPropertiesAsync(string projectId, string[] keys);
```

All three follow the precedent set by `UpsertColorLegendAsync` / `DeleteColorLegendAsync` — direct PostgREST calls, no Edge Function required.

### Layer 3: Shared action-button base (`Selvagen.GH/Components/`)

Refactor the existing in-canvas button mechanism so both Upload and Delete can use it.

**New file:** `SelvagenActionComponentBase.cs`

Generalizes `SelvagenUploadComponentBase`'s flag → recompute → consume-once pattern:

```csharp
public abstract class SelvagenActionComponentBase : GH_Component
{
    private bool _actionRequested;

    public bool IsRunning { get; protected set; }

    public bool ActionRequested
    {
        get
        {
            if (!_actionRequested) return false;
            _actionRequested = false;
            return true;
        }
    }

    public void RequestAction()
    {
        _actionRequested = true;
        ExpireSolution(true);
    }

    // Subclasses tell the attributes class what to paint
    public abstract string ActionLabel { get; }   // "Upload" or "Delete"
    public abstract System.Drawing.Color ActionColor { get; }
    // ... protected helpers, CreateAttributes, etc.
}
```

Existing `SelvagenUploadComponentBase` becomes a thin subclass that returns `"Upload"` + green. New `SelvagenDeleteActionComponentBase` (or just inlined into the Delete component if the surface is small enough) returns `"Delete"` + red.

The `SelvagenUploadAttributes` painter is generalized to read the label/color from the host component.

**Fallback:** if the refactor proves more invasive than expected during implementation, the fallback is a parallel `SelvagenDeleteAttributes` class that copies `SelvagenUploadAttributes` and changes the paint colors. User-visible behavior identical either way.

### Layer 4: New GH components (`Selvagen.GH/Components/Shared/`)

All three live in `"Selvagen"` / `"07 Shared"`. `GH_Exposure.primary`.

#### `SelvagenUploadCustomPropertyComponent` (`SvUpProp`)

GUID: `A1000006-0001-4000-8000-000000000001`

| # | Input | Nick | Type | Access | Optional |
|---|---|---|---|---|---|
| 0 | Project ID | PrjID | Text | item | no |
| 1 | Key | K | Text | list | no |
| 2 | Value | V | Text | list | no |

| # | Output | Nick | Notes |
|---|---|---|---|
| 0 | Preview | Prev | List of `"key = value"` strings, one per zipped pair |
| 1 | Status | S | |
| 2 | Record IDs | IDs | List of UUIDs from PostgREST `return=representation` |

**Inline canvas chrome:**
- Type dropdown (`text` / `number` / `boolean`), persisted via `Write`/`Read`. Selected type shown in the component `Message` chip.
- Upload button at the bottom (existing pattern, painted by shared attributes).

**SolveInstance flow:**
1. If `!UploadRequested`, set Status = `"Ready to upload."`, populate Preview, return.
2. Pull Project ID + Keys[] + Values[] from DA.
3. If `Keys.Count ≠ Values.Count`, yellow Warning + truncate to `min`.
4. Validate each key against `^[a-z][a-z0-9_]*$` + length 1–200. On failure → red Error with suggestion, abort.
5. Validate each value against active Type. On failure → red Error, abort.
6. Build `CustomPropertyUpsert[]`. Call `UpsertCustomPropertiesAsync`. Populate outputs.

#### `SelvagenListCustomPropertiesComponent` (`SvListProps`)

GUID: `A1000006-0001-4000-8000-000000000002`
Base: `SelvagenSelectableComponentBase<CustomPropertyInfo>` (same as `SelvagenListAssetsComponent`).

| # | Input | Nick |
|---|---|---|
| 0 | Project ID | PrjID |

| # | Output | Nick | Notes |
|---|---|---|---|
| 0 | Selected Key | K | The key picked in the inline dropdown |
| 1 | Selected Value | V | |
| 2 | Selected Type | T | |
| 3 | All Keys | Ks | Parallel lists — every property's key |
| 4 | All Values | Vs | …value |
| 5 | All Types | Ts | …type |

`SelvagenSelectableComponentBase<T>` provides the dropdown + caching + refresh. The display name (what the dropdown shows) is the key.

When no item is selected (e.g., empty project, list not yet refreshed), `Selected Key` / `Selected Value` / `Selected Type` are emitted as empty strings — same convention as `SelvagenListAssetsComponent`.

#### `SelvagenDeleteCustomPropertyComponent` (`SvDelProp`)

GUID: `A1000006-0001-4000-8000-000000000003`
Base: `SelvagenActionComponentBase` (with `ActionLabel = "Delete"`, `ActionColor = red`).

| # | Input | Nick | Type | Access |
|---|---|---|---|---|
| 0 | Project ID | PrjID | Text | item |
| 1 | Key | K | Text | list |

| # | Output | Nick |
|---|---|---|
| 0 | Success | OK |
| 1 | Status | S |

No `Delete?` toggle input — the in-canvas red Delete button replaces it (same pattern as Upload).

**SolveInstance flow:**
1. If `!ActionRequested`, Status = `"Ready to delete."`, return.
2. Pull inputs. Empty Keys → Warning `"Nothing to delete."`, return.
3. Call `DeleteCustomPropertiesAsync`. Status = `"Deleted N properties"`.

### Layer 5: Removed code

```
DELETE: Selvagen.GH/Components/Shared/SelvagenPropertiesComponent.cs
DELETE: Selvagen.GH/Components/Shared/SelvagenPropertiesAttributes.cs
```

### Layer 6: Icons

Follow the established composite convention (base motif top-left + component-specific badge bottom-right), as in `FAMILY_ICONS` in `generate_icons.py`. The shared base for the Custom Property family is `mdi:tune` (the same icon currently used standalone for `Properties`).

Updates to `generate_icons.py`:

```python
# Add a new section to FAMILY_ICONS:
CUSTOM_PROPERTY_BASE = "mdi:tune"
FAMILY_ICONS.update({
    "UploadCustomProperty": (CUSTOM_PROPERTY_BASE, "mdi:arrow-up-bold"),
    "ListCustomProperties": (CUSTOM_PROPERTY_BASE, "mdi:format-list-bulleted"),
    "DeleteCustomProperty": (CUSTOM_PROPERTY_BASE, "mdi:trash-can-outline"),
})

# Remove from STANDALONE_ICONS:
# "Properties": "mdi:tune",
```

Generated PNGs: `UploadCustomProperty.png`, `ListCustomProperties.png`, `DeleteCustomProperty.png`. Existing `Properties.png` can be deleted (no longer referenced).

## Data Flow

**Upload**
1. User wires Project ID + Keys + Values; picks Type in inline dropdown; presses canvas Upload button.
2. Component validates all keys + all values; on any failure → red Error + Status set + abort batch (no partial upserts).
3. On success: build `CustomPropertyUpsert[]` → `SelvagenClient.UpsertCustomPropertiesAsync` → PostgREST `POST` with `on_conflict=project_id,key` + `Prefer: resolution=merge-duplicates,return=representation` → response array → Status + Record IDs populated.

**List**
1. User wires Project ID.
2. On project change or manual refresh, component fetches via `ListCustomPropertiesAsync` → caches.
3. Inline dropdown populated with `key` values (sorted).
4. All six outputs computed: `Selected *` from dropdown choice, `All *` from full cache.

**Delete**
1. User wires Project ID + Key(s); presses canvas Delete button.
2. No client-side validation (bad keys simply yield 0 deletes).
3. PostgREST `DELETE /rest/v1/custom_properties?project_id=eq.<…>&key=in.(k1,k2,…)`.
4. `Success` = HTTP 2xx; `Status` = `"Deleted N properties"` from the response's `Content-Range` count.

## Validation Rules

### Key (applied after trim)

- Regex: `^[a-z][a-z0-9_]*$`
- Length: 1–200 chars
- Both enforced by DB `CHECK` constraints as a backstop

### "Did you mean" suggestion algorithm

1. Trim
2. Lowercase
3. Replace any char not matching `[a-z0-9_]` with `_`
4. Collapse runs of `_` into a single `_`
5. Strip leading `_`
6. If result is empty or starts with a digit, prepend `prop_`
7. Truncate to 200 chars

Examples:
- `"Soil pH"` → `soil_ph`
- `"Contour Height (m)"` → `contour_height_m`
- `"1stFloor"` → `prop_1stfloor`
- `"  --??  "` → `prop_`

### Value (applied per active inline Type)

- `text` → no validation
- `number` → `double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, …)` — `6.4` passes, `6,4` fails (matches existing convention)
- `boolean` → `bool.TryParse(v, …)` — accepts `true`/`false` in any casing

## Error Handling

| Condition | Level | Status text |
|---|---|---|
| Not logged in | Warning | `Not logged in. Place a Login component first.` |
| Missing Project ID | Warning | `Missing Project ID` |
| Empty Keys list (Upload) | Warning | `Nothing to upload.` |
| Empty Keys list (Delete) | Warning | `Nothing to delete.` |
| `Keys.Count ≠ Values.Count` | Warning + truncate to `min` | `Truncated to N pairs (lists mismatched)` |
| Invalid key | **Error** | `Invalid key 'X'. Must be snake_case. Did you mean: y?` |
| Value fails type parse | **Error** | `Value 'X' is not a valid <type> (key: y)` |
| Network / Supabase error | **Error** | underlying API message |
| DB CHECK rejection (shouldn't happen if client validates) | **Error** | raw Postgres message + `(client validation should have caught this — please report)` |

All errors abort the operation (no partial state). The DB constraints are the safety net; the GH validation is the friendly first line.

## Migration & Rollout

```
1. Apply DB migration: docs/migrations/2026-05-28-add-custom-properties-table.sql
   - CREATE TABLE custom_properties + index + trigger + RLS policies
   - DROP COLUMN properties from topography, geology, analyses, optimizations
   - NOTIFY pgrst, 'reload schema';

2. Ship new plugin (.gha):
   - Add Selvagen.Core models (CustomPropertyInfo, CustomPropertyUpsert)
   - Add SelvagenClient methods (List/Upsert/Delete)
   - Add three new components + SelvagenActionComponentBase refactor
   - Update generate_icons.py + regenerate affected icons
   - Delete SelvagenPropertiesComponent + SelvagenPropertiesAttributes
   - Deploy via existing Libraries\Selvagen path; restart Rhino

3. User-facing note (changelog):
   "Custom Properties has been redesigned. Existing .gh files using the old
    Custom Properties component will show a missing-component placeholder on
    open. Replace with Upload Custom Property / List Custom Properties /
    Exclude Custom Property."
```

The migration intentionally breaks the old plugin first (column gone) so there's a clean cutover. Brief window of "old plugin against new DB" will produce upload errors — acceptable given the small user base.

## Files Changed

| File | Change |
|------|--------|
| `docs/migrations/2026-05-28-add-custom-properties-table.sql` | NEW — schema + cleanup |
| `Selvagen.Core/Models/CustomProperty.cs` | NEW — `CustomPropertyInfo`, `CustomPropertyUpsert` |
| `Selvagen.Core/Api/SelvagenClient.cs` | Add `ListCustomPropertiesAsync`, `UpsertCustomPropertiesAsync`, `DeleteCustomPropertiesAsync` |
| `Selvagen.GH/Components/SelvagenActionComponentBase.cs` | NEW — shared base for in-canvas button components |
| `Selvagen.GH/Components/SelvagenUploadComponentBase.cs` | Refactor to subclass `SelvagenActionComponentBase` (label = "Upload", color = green) |
| `Selvagen.GH/Components/SelvagenUploadAttributes.cs` | Generalize to read label + color from host component |
| `Selvagen.GH/Components/Shared/SelvagenUploadCustomPropertyComponent.cs` | NEW |
| `Selvagen.GH/Components/Shared/SelvagenListCustomPropertiesComponent.cs` | NEW |
| `Selvagen.GH/Components/Shared/SelvagenDeleteCustomPropertyComponent.cs` | NEW |
| `Selvagen.GH/Components/Shared/SelvagenPropertiesComponent.cs` | DELETE |
| `Selvagen.GH/Components/Shared/SelvagenPropertiesAttributes.cs` | DELETE |
| `Selvagen.GH/Icons/generate_icons.py` | Add custom-property family entries; remove standalone `Properties` |
| `Selvagen.GH/Icons/UploadCustomProperty.png` | NEW (generated) |
| `Selvagen.GH/Icons/ListCustomProperties.png` | NEW (generated) |
| `Selvagen.GH/Icons/DeleteCustomProperty.png` | NEW (generated) |
| `Selvagen.GH/Icons/Properties.png` | DELETE (no longer referenced) |

## Testing

No automated tests in this repo today; this is a manual test plan executed in Rhino with the deployed plugin.

**Smoke** (must pass before merge):

1. Login → drop `Upload Custom Property` → wire Project ID + Keys `["soil_ph", "vegetation"]` + Values `["6.4", "dense"]`, Type = `text` → press Upload. Expected: Status = `"Upserted 2 properties"`, IDs populated, Preview shows both rows.
2. Drop `List Custom Properties` → wire same Project ID → dropdown shows both keys → pick `soil_ph` → Selected Value = `"6.4"`, All Keys = `["soil_ph", "vegetation"]`.
3. Drop `Exclude Custom Property` → wire Project ID + `soil_ph` → press Delete → refresh List → only `vegetation` remains.

**Edge cases:**

- Re-upload `soil_ph = 7.1` → List shows updated value (upsert worked; no duplicate row).
- Upload with bad key `"Soil pH"` → red Error with suggestion `soil_ph`; no DB write.
- Upload with Type = `number` and value `"hello"` → red Error; no DB write.
- Upload mismatched `["a","b","c"]` / `["1","2"]` → yellow Warning, only 2 properties uploaded.
- Delete a key that doesn't exist → Success = true, Status = `"Deleted 0 properties"`.
- DB-level: hit PostgREST directly with `"BAD KEY"` → 400 from CHECK constraint (proves backstop).

## Out of Scope

- Compound/nested values (arrays, objects) — single Custom Property holds one scalar
- Per-property history / audit log — `updated_at` only
- Bulk import from CSV / JSON file
- Property templates or schemas (e.g., "every topography project must have `contour_height_m`")
- Edge Function wrapping the PostgREST calls — direct PostgREST access matches existing precedents (`color_legends`, module records)
- Backfilling historical `properties` JSON from the four module tables into the new table — migration drops the column outright. If real platform-side data exists, this becomes a prerequisite step (not in this spec's scope).
