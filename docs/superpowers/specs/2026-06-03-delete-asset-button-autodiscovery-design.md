# Delete Asset — embedded button + table auto-discovery

**Date:** 2026-06-03
**Component:** `SelvagenDeleteAssetComponent` (Selvagen.GH → "08 Assets")
**Status:** Approved design

## Problem

The "Delete Asset" component has two friction points:

1. **No in-canvas action.** Deletion is triggered by wiring a boolean `Delete`
   input to `true`, rather than by a button on the component like Upload has.
2. **Two inputs required.** The user must supply *both* the `Asset Table` name
   and the `Asset ID`. The table is redundant information the user shouldn't
   have to know or wire — the component should determine it from the ID alone.

## Goal

The user wires a single `Asset ID` and clicks an embedded **Delete** button.
The component discovers which table the asset lives in and deletes it from there.

## Key facts that shape the design

- Asset IDs are Postgres **UUIDs**, globally unique across all asset tables.
  The table name is only needed to *route* the delete, not to identify the row.
- All five asset tables (`meshes`, `curve_sets`, `label_sets`,
  `animation_sequences`, `color_legends`) enforce DELETE through an **RLS
  policy** of the form `user_can_at_least(project.firm_id, 'editor')`.
- The button + one-shot-flag infrastructure already exists in
  `SelvagenActionComponentBase` + `SelvagenActionAttributes` (built for Upload).

## Decisions (from brainstorming)

- **Discovery mechanism:** server-side RPC that finds-and-deletes across tables
  in a single call (chosen over client-side table probing or carrying the type
  through List Assets).
- **Color legends:** included — Delete can target `color_legends`, matching what
  List Assets shows.
- **Compatibility:** clean break. The `Asset Table` and boolean `Delete` inputs
  are removed; the component keeps its existing `ComponentGuid` so saved
  definitions still resolve to it (they drop the wires to removed inputs rather
  than showing a missing-component placeholder).
- **Old client method:** `DeleteAssetAsync(table, id)` is removed — its only
  caller is the component being rewritten.

## Design

### 1. Server — RPC `public.delete_asset_by_id(p_asset_id uuid) → text`

`SECURITY INVOKER` (the default), so the deletes inside run as the calling user
and the existing RLS `editor` policies enforce authorization unchanged — no
privilege escalation, no service-role key. The body tries each table in order
and early-returns the table name on the first row deleted; returns `null` if
nothing was deleted.

Because RLS filters rows the caller can't touch, a delete the user isn't
permitted to perform affects 0 rows rather than erroring — so "not found" and
"not authorized" collapse into the same `null` result, by design.

```sql
create or replace function public.delete_asset_by_id(p_asset_id uuid)
returns text
language plpgsql
security invoker
set search_path = public
as $$
begin
  delete from public.meshes               where id = p_asset_id; if found then return 'meshes';               end if;
  delete from public.curve_sets           where id = p_asset_id; if found then return 'curve_sets';           end if;
  delete from public.label_sets           where id = p_asset_id; if found then return 'label_sets';           end if;
  delete from public.animation_sequences  where id = p_asset_id; if found then return 'animation_sequences';  end if;
  delete from public.color_legends        where id = p_asset_id; if found then return 'color_legends';        end if;
  return null;
end;
$$;

grant execute on function public.delete_asset_by_id(uuid) to authenticated;
```

Tracked as a file under `docs/migrations/` (matching the existing convention)
and applied to project `aqzfsrebvjkegvfexcut` (GEN.BOARD) via `apply_migration`.

### 2. Client — `SelvagenClient.DeleteAssetByIdAsync`

```
Task<string> DeleteAssetByIdAsync(string assetId)
```

POSTs `{ "p_asset_id": assetId }` to `/rest/v1/rpc/delete_asset_by_id` via the
existing `SendAuthorizedAsync` helper; deserializes the scalar response to the
table name string, or `null` when nothing was deleted. Removes the old
`DeleteAssetAsync(string tableName, string assetId)` method.

### 3. Component — `SelvagenDeleteAssetComponent` rewrite

- Inherits from `SelvagenActionComponentBase` (embedded button + one-shot flag).
- **Input:** `Asset ID` only.
- **Outputs:** unchanged — `Success` (bool), `Status` (text).
- Overrides button members: `ActionLabel = "Delete"`,
  `ActionLabelRunning = "Deleting..."`, with a **red gradient** to signal a
  destructive action (distinct from Upload's gray).
- Keeps the existing `ComponentGuid` (`C39D4E5F-A6B7-8901-2CDE-F34567890123`).
- `SolveInstance` mirrors the upload pattern:
  - `!ActionRequested` → status "Ready."; warn if not logged in.
  - empty Asset ID → warning, status "Missing Asset ID".
  - else set `IsRunning`, `ForceCanvasRefresh`, call `DeleteAssetByIdAsync`:
    - non-null table → `Success = true`, status `"Deleted <id> from <table>"`.
    - null → `Success = false`, status
      `"Asset not found or you lack permission to delete it."`
    - exception → `SetActionError`.

## Verification

1. `dotnet build` exits 0.
2. Deploy full output to `%APPDATA%\Grasshopper\Libraries\Selvagen\`.
3. Restart Rhino.
4. On canvas: log in, List Assets, wire one Asset ID into Delete, click the
   button → confirm the row disappears from a re-listed set and status reads
   "Deleted …".
5. Wire a bogus/already-deleted ID, click → status reads the not-found message,
   `Success = false`.
