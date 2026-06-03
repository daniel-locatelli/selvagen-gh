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
  policy** of the form `user_can_at_least(project.firm_id, 'editor')`, while
  SELECT is granted more broadly to *any active firm member*. This asymmetry
  (SELECT ⊃ DELETE) is what lets a `SECURITY INVOKER` function tell "you can see
  it but can't delete it" (forbidden) apart from "you can't see it" (not found).
- `id` is the PRIMARY KEY on every asset table, so an `id = …` probe is an index
  lookup, not a sequential scan — five probes cost microseconds and do not
  degrade as the tables grow.
- The button + one-shot-flag infrastructure already exists in
  `SelvagenActionComponentBase` + `SelvagenActionAttributes` (built for Upload).

## Decisions (from brainstorming)

- **Discovery mechanism:** server-side RPC that finds-and-deletes across tables
  in a single call (chosen over client-side table probing or carrying the type
  through List Assets).
- **Color legends:** included — Delete can target `color_legends`, matching what
  List Assets shows.
- **Compatibility:** clean break. The `Asset Table` and boolean `Delete` inputs
  are removed. The project is **greenfield** — no saved definitions use the old
  component — so no upgrade-detection warning is needed. The existing
  `ComponentGuid` is kept anyway (harmless, and avoids gratuitous churn).
- **Old client method:** `DeleteAssetAsync(table, id)` is removed — its only
  caller is the component being rewritten.
- **Integrity guard:** the RPC scans all five tables (not early-return) so a UUID
  that somehow exists in more than one table is caught and refused *before* any
  delete, rather than silently orphaning a twin row.
- **Error granularity:** the RPC returns three distinct outcomes — `deleted`,
  `forbidden` (visible but caller is not an editor), `not_found` — so the
  component can show precise, actionable status messages.

## Design

### 1. Server — RPC `public.delete_asset_by_id(p_asset_id uuid) → jsonb`

`SECURITY INVOKER` (the default), so every statement inside runs as the calling
user and the existing RLS policies enforce authorization unchanged — no
privilege escalation, no service-role key.

Flow: an **existence scan** probes all five tables (PK index lookups under the
caller's SELECT visibility). Then:

- **0 tables matched** → `{ "status": "not_found" }` (not present, or in a firm
  the caller can't see — deliberately not distinguished, to avoid leaking the
  existence of other firms' assets).
- **>1 table matched** → `raise exception` (data-integrity guard; refuses to
  delete a UUID that exists in multiple tables, before destroying anything).
- **exactly 1 table** → delete from it. Row count read via `GET DIAGNOSTICS`
  (note: `EXECUTE` does **not** set `FOUND`). Deleted ≥1 row →
  `{ "status": "deleted", "table": "<name>" }`; deleted 0 rows →
  `{ "status": "forbidden" }` (caller could see the row but is not an editor).

```sql
create or replace function public.delete_asset_by_id(p_asset_id uuid)
returns jsonb
language plpgsql
security invoker
set search_path = public
as $$
declare
  v_found text[] := array[]::text[];
  v_table text;
  v_count integer;
begin
  -- Existence scan under the caller's RLS. SELECT is granted to any active firm
  -- member; DELETE needs editor — so a visible-but-undeletable row is detectable.
  -- Scanning all five also catches a UUID present in >1 table before we delete.
  -- array_append, NOT `||`: appending an untyped literal via || makes Postgres
  -- resolve it as anyarray||anyarray and parse e.g. 'meshes' as an array literal
  -- (22P02 malformed array literal). array_append binds the literal to anyelement.
  if exists (select 1 from public.meshes              where id = p_asset_id) then v_found := array_append(v_found, 'meshes');              end if;
  if exists (select 1 from public.curve_sets          where id = p_asset_id) then v_found := array_append(v_found, 'curve_sets');          end if;
  if exists (select 1 from public.label_sets          where id = p_asset_id) then v_found := array_append(v_found, 'label_sets');          end if;
  if exists (select 1 from public.animation_sequences where id = p_asset_id) then v_found := array_append(v_found, 'animation_sequences'); end if;
  if exists (select 1 from public.color_legends       where id = p_asset_id) then v_found := array_append(v_found, 'color_legends');       end if;

  if array_length(v_found, 1) is null then
    return jsonb_build_object('status', 'not_found');
  end if;
  if array_length(v_found, 1) > 1 then
    raise exception 'Data integrity violation: asset id % found in multiple tables (%).', p_asset_id, v_found;
  end if;

  v_table := v_found[1];
  execute format('delete from public.%I where id = $1', v_table) using p_asset_id;
  get diagnostics v_count = row_count;   -- EXECUTE does not set FOUND

  if v_count > 0 then
    return jsonb_build_object('status', 'deleted', 'table', v_table);
  else
    return jsonb_build_object('status', 'forbidden');
  end if;
end;
$$;

grant execute on function public.delete_asset_by_id(uuid) to authenticated;
```

Tracked as a file under `docs/migrations/` (matching the existing convention)
and applied to project `aqzfsrebvjkegvfexcut` (GEN.BOARD) via `apply_migration`.

**Known edge (accepted):** a concurrent delete landing between the existence
scan and our delete would yield `forbidden` instead of `not_found` — a wrong
*message*, never wrong data.

### 2. Client — `SelvagenClient.DeleteAssetByIdAsync`

```
Task<DeleteAssetResult> DeleteAssetByIdAsync(string assetId)
```

POSTs `{ "p_asset_id": assetId }` to `/rest/v1/rpc/delete_asset_by_id` via the
existing `SendAuthorizedAsync` helper and deserializes the `jsonb` response into
a small DTO:

```csharp
public class DeleteAssetResult
{
    [JsonPropertyName("status")] public string Status { get; set; } = ""; // deleted | forbidden | not_found
    [JsonPropertyName("table")]  public string Table  { get; set; } = "";
}
```

A non-2xx response (e.g. the integrity-violation `raise`) throws
`SelvagenApiException`, consistent with the other client methods. Removes the
old `DeleteAssetAsync(string tableName, string assetId)` method.

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
  - else set `IsRunning`, `ForceCanvasRefresh`, call `DeleteAssetByIdAsync`, then
    map `result.Status`:
    - `deleted` → `Success = true`, status `"Deleted <id> from <table>"`.
    - `forbidden` → `Success = false`, warning + status
      `"You don't have permission to delete this asset (editor role required)."`
    - `not_found` → `Success = false`, status
      `"Asset not found (check the ID, or it may already be deleted)."`
    - exception → `SetActionError` (surfaces the integrity-violation message).

## Verification

1. `dotnet build` exits 0.
2. Deploy full output to `%APPDATA%\Grasshopper\Libraries\Selvagen\`.
3. Restart Rhino.
4. On canvas: log in, List Assets, wire one Asset ID into Delete, click the
   button → confirm the row disappears from a re-listed set and status reads
   "Deleted …".
5. Wire a bogus/already-deleted ID, click → status reads the not-found message,
   `Success = false`.
6. (If a non-editor test session is available) delete a visible asset → status
   reads the permission message. Otherwise verify the `forbidden` branch with a
   direct `select public.delete_asset_by_id(...)` under a viewer role in SQL.
