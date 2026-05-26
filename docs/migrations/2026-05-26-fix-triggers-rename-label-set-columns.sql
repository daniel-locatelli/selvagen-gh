-- Migration: fix trigger functions after label_sets rename
-- Applied: 2026-05-26
-- Project: GEN.BOARD (aqzfsrebvjkegvfexcut)
-- Context: The rename_text3d_to_label_sets migration renamed columns but
--          missed 3 trigger functions that still referenced old column names.

CREATE OR REPLACE FUNCTION trg_topography_check_assets() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM _assert_asset_project_matches('meshes', NEW.base_mesh_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('curve_sets', NEW.outline_curve_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('curve_sets', NEW.contours_curve_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('label_sets', NEW.contours_label_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('curve_sets', NEW.urbanization_curve_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.elevation_mesh_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('curve_sets', NEW.elevation_curve_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.slope_mesh_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.access8_mesh_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.access5_mesh_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('curve_sets', NEW.drainage_curve_set_id, NEW.project_id);
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION trg_analyses_check_assets() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM _assert_asset_project_matches('meshes', NEW.earth_mesh_terrain_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.earth_mesh_massing_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.rock_mesh_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('label_sets', NEW.rock_label_set_height_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('label_sets', NEW.rock_label_set_vol_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('curve_sets', NEW.access_curve_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('label_sets', NEW.access_label_set_id, NEW.project_id);
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION trg_optimizations_check_assets() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM _assert_asset_project_matches('curve_sets', NEW.access_curve_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('label_sets', NEW.access_label_set_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.earth_mesh_terrain_id, NEW.project_id);
    PERFORM _assert_asset_project_matches('meshes', NEW.earth_mesh_lots_id, NEW.project_id);
    RETURN NEW;
END;
$$;

NOTIFY pgrst, 'reload schema';
