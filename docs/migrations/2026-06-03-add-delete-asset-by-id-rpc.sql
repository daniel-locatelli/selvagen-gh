-- Finds the asset table that owns p_asset_id and deletes from it, under the
-- caller's RLS (SECURITY INVOKER). SELECT is granted to any active firm member
-- while DELETE requires editor, so a visible-but-undeletable row reports
-- 'forbidden' rather than collapsing into 'not_found'. Scans all five tables so
-- a UUID present in >1 table is refused before any delete.
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
  if exists (select 1 from public.meshes              where id = p_asset_id) then v_found := v_found || 'meshes';              end if;
  if exists (select 1 from public.curve_sets          where id = p_asset_id) then v_found := v_found || 'curve_sets';          end if;
  if exists (select 1 from public.label_sets          where id = p_asset_id) then v_found := v_found || 'label_sets';          end if;
  if exists (select 1 from public.animation_sequences where id = p_asset_id) then v_found := v_found || 'animation_sequences'; end if;
  if exists (select 1 from public.color_legends       where id = p_asset_id) then v_found := v_found || 'color_legends';       end if;

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
