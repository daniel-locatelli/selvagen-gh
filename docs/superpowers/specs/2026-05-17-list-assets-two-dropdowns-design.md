# List Assets two-dropdown UI — design

**Status:** approved
**Date:** 2026-05-17
**Scope:** `SelvagenListAssetsComponent` and the shared selector machinery in `Selvagen.GH/Components/`
**Builds on:** [2026-05-14 dropdown selectors design](2026-05-14-grasshopper-dropdown-selectors-design.md)

---

## Problem

The `List Assets` component currently has a single `AssetType` text input parameter that the user wires (or leaves at the default `"meshes"`) to pick which table to query. The platform now exposes a fourth asset type (`animation_sequences`) and the wired-text-input UX is awkward — users have to type the table name correctly or wire a Value List.

The right model is two dropdowns on the component face: one for asset type, one for the asset itself. Picking a different type cascades into a fresh fetch and clears the asset selection.

## Goals

- Replace the `AssetType` input parameter with an inline dropdown on the component.
- Add `animation_sequences` as a fourth asset type alongside the three existing ones.
- Two dropdowns on `List Assets`: top = AssetType (fixed 4-option enum), bottom = Asset (dynamic, depends on type).
- Picking a new AssetType clears the previous Asset selection and triggers a re-fetch.
- Both selections persist across save/reopen.
- Other selector components (`Clients`, `Projects`) keep their existing single-dropdown UX unchanged.

## Non-goals

- Adding `slides`, `project_stages`, or any module tables to the AssetType list (out of scope per user decision).
- Keeping `AssetType` available as an optional wired input alongside the dropdown (rejected as ambiguous UX).
- Generalising the secondary dropdown for use beyond `List Assets` right now. The machinery supports N dropdowns, but only `List Assets` exercises >1 in this PR.

---

## Asset types

| Display name | Table name (id) | Existing? |
|---|---|---|
| Meshes | `meshes` | yes (default) |
| Curve Sets | `curve_sets` | yes |
| Text 3D Sets | `text_3d_sets` | yes |
| Animation Sequences | `animation_sequences` | no — new |

A small static array in `SelvagenListAssetsComponent` declares this list (4 tuples). No external configuration; if more types appear later, append to that array.

---

## Interface refactor — `ISelectorComponent` becomes list-shaped

The single-dropdown interface is generalised to a list. Each entry in the list is a self-contained dropdown.

```csharp
public interface ISelectorComponent
{
    IReadOnlyList<IDropdownSlot> Dropdowns { get; }
}

public interface IDropdownSlot
{
    string CurrentDisplayText { get; }
    bool HasItems { get; }
    IEnumerable<(string Id, string Name)> GetMenuItems();
    string SelectedId { get; }
    void SetSelectedId(string id);
}
```

`SelvagenSelectableComponentBase<TItem>` becomes the producer of one default `IDropdownSlot` (a private adapter exposing the existing `_cachedItems`/`_selectedId` state and the existing display-text rules). The base's `Dropdowns` getter returns a one-element list containing that adapter. Subclasses (`Clients`, `Projects`) inherit this unchanged — they automatically expose exactly one dropdown.

`SelvagenListAssetsComponent` overrides `Dropdowns` to return a two-element list: `[typeSlot, assetSlot]`. `typeSlot` is a new small adapter backed by `_selectedAssetType` + the static type list; `assetSlot` is the same default adapter the base uses.

### Why this shape

- The attributes class (`SelvagenSelectorAttributes`) iterates the list and stacks rectangles; it never needs to know if there's 1 or 2. One render path, one click path, one menu-mirror path.
- Subclasses with one dropdown require zero code changes — they inherit the default `Dropdowns` getter from the base.
- Subclasses with N dropdowns implement `Dropdowns` once and define their N adapters. No widget-class subclassing.

---

## Attributes widget — N stacked dropdowns

`SelvagenSelectorAttributes.Layout()` changes from "expand bounds by 26 px and place one dropdown" to "expand bounds by `N × (DropdownHeight + DropdownPadding)` and place N dropdowns vertically below the natural body":

```
Bounds.Height = naturalHeight + Dropdowns.Count * (DropdownHeight + DropdownPadding)

For i in 0..Dropdowns.Count:
    dropdownRect[i] = RectangleF(
        Bounds.Left + InnerSidePadding,
        Bounds.Top + naturalHeight + (i * (DropdownHeight + DropdownPadding)) + DropdownPadding/2,
        Bounds.Width - 2*InnerSidePadding,
        DropdownHeight)
```

`Render()` iterates the `_dropdownRects` array, drawing one capsule + `▼` + display text per slot. `RespondToMouseDown` finds which (if any) rect contains the click and calls that slot's popup.

The popup-builder helper is parameterised by the slot rather than reading `Selector.CurrentDisplayText` directly:

```csharp
private void ShowDropdownMenu(GH_Canvas canvas, IDropdownSlot slot, RectangleF rect) { ... }
```

The right-click menu mirror in the base class is extended similarly — one submenu per slot, named after the slot. For `List Assets` the menu reads `Asset Type ▸ Meshes / Curve Sets / Text 3D Sets / Animation Sequences` and `Asset ▸ Sphere_1 / Sphere_2 / ...`. For Clients/Projects the menu shows one submenu named `Select` (current behaviour preserved).

### Submenu naming

To preserve backward compatibility with the existing right-click menu UX, the base class needs to know what to call each slot's submenu. We extend `IDropdownSlot` with a `SubmenuName` property:

```csharp
public interface IDropdownSlot
{
    string SubmenuName { get; }       // "Select" by default, or "Asset Type", "Asset", etc.
    string CurrentDisplayText { get; }
    bool HasItems { get; }
    IEnumerable<(string Id, string Name)> GetMenuItems();
    string SelectedId { get; }
    void SetSelectedId(string id);
}
```

The default base-class adapter returns `"Select"` so Clients/Projects keep their current submenu label. `SelvagenListAssetsComponent`'s type adapter returns `"Asset Type"`; the asset adapter returns `"Asset"`.

---

## `SelvagenListAssetsComponent` changes

### Input parameters

Before:
- `ProjectID` (text, item, optional)
- `AssetType` (text, item, default "meshes")
- (Refresh added by base)

After:
- `ProjectID` (text, item, optional) — unchanged
- (Refresh added by base) — unchanged

`AssetType` removed. The selection lives in component state, exposed through the type dropdown.

### State

```csharp
private string _selectedAssetType = "meshes";  // default
```

(Existing `_selectedId`, `_cachedItems`, etc. from base unchanged.)

### Static type list

```csharp
private static readonly (string Id, string Name)[] AssetTypes = new[]
{
    ("meshes", "Meshes"),
    ("curve_sets", "Curve Sets"),
    ("text_3d_sets", "Text 3D Sets"),
    ("animation_sequences", "Animation Sequences"),
};
```

### Hooks

`CaptureInputs(da)` returns `[projectId, _selectedAssetType]`. Cache key is the same array (default `GetCacheKey` passthrough). Changing `_selectedAssetType` triggers cache invalidation via the existing `CacheDecision.NeedsFetch` path.

`FetchAsync(client, inputs)` switches on `(string)inputs[1]` and dispatches to `client.ListMeshesAsync` / `ListCurveSetsAsync` / `ListText3DSetsAsync` / `ListAnimationSequencesAsync(projectId)`.

### Type-change handler

A method analogous to `SetSelectedId` lives on `SelvagenListAssetsComponent`:

```csharp
public void SetSelectedAssetType(string typeId)
{
    if (typeId == _selectedAssetType) return;
    _selectedAssetType = typeId;
    _selectedId = null;             // invalidate the previously-picked asset
    ExpireSolution(true);           // re-solves → cache key differs → re-fetch
}
```

The type adapter's `SetSelectedId(typeId)` forwards to this method. Clearing `_selectedId` is the spec's "second dropdown is invalidated after the first changes" rule.

### Persistence

`Write` adds `_selectedAssetType`; `Read` restores it (default to `"meshes"` if missing so files saved before this change open cleanly):

```csharp
public override bool Write(GH_IWriter writer)
{
    if (!base.Write(writer)) return false;
    writer.SetString("SelectedAssetType", _selectedAssetType ?? "meshes");
    return true;
}

public override bool Read(GH_IReader reader)
{
    if (!base.Read(reader)) return false;
    string t = "meshes";
    reader.TryGetString("SelectedAssetType", ref t);
    _selectedAssetType = string.IsNullOrEmpty(t) ? "meshes" : t;
    return true;
}
```

### `Dropdowns` override

The base class exposes a protected factory method so subclasses can construct the default asset slot with a custom submenu name:

```csharp
// On SelvagenSelectableComponentBase<TItem>:
protected IDropdownSlot CreatePrimarySlot(string submenuName) =>
    new DefaultAssetSlot(this, submenuName);

public virtual IReadOnlyList<IDropdownSlot> Dropdowns =>
    new[] { CreatePrimarySlot("Select") };
```

`SelvagenListAssetsComponent` overrides:

```csharp
public override IReadOnlyList<IDropdownSlot> Dropdowns => new IDropdownSlot[]
{
    new AssetTypeDropdownSlot(this),
    CreatePrimarySlot("Asset"),
};
```

`AssetTypeDropdownSlot` is a private nested class implementing `IDropdownSlot` against the static `AssetTypes` array + `_selectedAssetType`. It returns `"Asset Type"` for `SubmenuName`, and the matched display name (or `"Meshes"` as default) for `CurrentDisplayText`. `HasItems` is always `true` (the list is static, non-empty). `SetSelectedId` calls `SetSelectedAssetType`.

`DefaultAssetSlot` is the base-class adapter wrapping the dynamic-asset state. Its `SubmenuName` is whatever was passed at construction. Clients/Projects inherit the `Dropdowns` getter from the base and end up with `"Select"`. List Assets constructs its own with `"Asset"`.

---

## `SelvagenClient.ListAnimationSequencesAsync` (new)

Follows the existing `ListMeshesAsync` pattern:

```csharp
public async Task<AssetInfo[]> ListAnimationSequencesAsync(string projectId)
{
    if (string.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
    var path = $"/rest/v1/animation_sequences?project_id=eq.{projectId}&select=id,name,created_at&order=created_at.desc";
    return await QueryAssetsAsync(path, "animation_sequences").ConfigureAwait(false);
}
```

`animation_sequences` rows have `id`, `name`, `created_at` but no `type` column — `AssetInfo.Type` will deserialise as empty string. That's fine; `Type` is unused by the selector UI (the AssetType dropdown is the type indicator).

---

## Breaking changes

1. **`SelvagenListAssetsComponent.AssetType` input parameter removed.** Existing `.gh` files with a wire feeding `AssetType` will lose that wire on load. The plugin is still young; acceptable per prior breaking-change policy. The persisted `_selectedAssetType` defaults to `"meshes"` so silent loads behave identically to the previous default.

2. **`ISelectorComponent` interface shape changes.** This is an internal interface (only consumed by `SelvagenSelectorAttributes`); no external plugin code consumes it. Not a public API break.

---

## Open questions resolved during brainstorm

| Question | Decision |
|---|---|
| Keep AssetType as input alongside dropdown? | No, remove entirely |
| Generalise attributes for N dropdowns or special-case List Assets? | Generalise via list-shaped `ISelectorComponent` |
| What happens to picked Asset when AssetType changes? | Always clear |
| Persist `_selectedAssetType` across save/reopen? | Yes |
| Asset types to include? | `meshes`, `curve_sets`, `text_3d_sets`, `animation_sequences` (no slides or stages) |

---

## Testing

### Unit tests

No new pure-logic added. `CacheDecision.NeedsFetch` and `Reconcile.SelectId` are unchanged.

Optional: a tiny test asserting that `SetSelectedAssetType("curve_sets")` clears `_selectedId`. This requires either making the component testable without GH runtime (hard) or skipping. **Decision:** skip — the behaviour is trivial enough to verify by code inspection + integration test.

### Integration tests

The existing `test_clients_cascade.py` scenario already covers `Clients → Projects → Assets` cascade with the default asset type. After this change it still passes because the default is still `meshes` and the asset list still fetches.

A new scenario `test_asset_type_switch.py` verifies the new behaviour:
1. Login + Clients + Projects + Assets, wire Acme client → Projects → Assets default ProjectID
2. Recompute, verify Assets returns meshes (current behaviour)
3. Right-click Assets → `Asset Type ▸ Curve Sets`
4. Recompute, verify Assets re-fetched and returned curve_sets (new IDs/Names)
5. Verify previously-picked asset (if any) is cleared

(Per the existing constraint, Cordyceps cannot click custom canvas widgets but can invoke right-click menu items, so this scenario is automatable via the menu mirror.)

### Manual UI smoke

Same as the first dropdown spec — visual confirmation that two dropdowns stack properly at the bottom, click each to open its popup, picking from the type dropdown invalidates the asset dropdown's display ("— Select —" again).

---

## Follow-ups (out of scope here)

- Surfacing the Supabase RLS-disabled rollback tables (`_rollback_user_profiles_20260515`, `_rollback_firm_members_20260515`) to the operator — security advisory, separate ticket.
- Animation-sequence-specific UI in any downstream component (e.g., showing frame count alongside the name). Cosmetic.
