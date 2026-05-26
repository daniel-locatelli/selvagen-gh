-- Migration: rename text_3d_sets → label_sets
-- Applied: 2026-05-26
-- Project: GEN.BOARD (aqzfsrebvjkegvfexcut)

ALTER TABLE text_3d_sets RENAME TO label_sets;

ALTER TABLE topography RENAME COLUMN contours_text_3d_set_id TO contours_label_set_id;
ALTER TABLE analyses RENAME COLUMN access_text_3d_set_id TO access_label_set_id;
ALTER TABLE analyses RENAME COLUMN rock_text_3d_set_height_id TO rock_label_set_height_id;
ALTER TABLE analyses RENAME COLUMN rock_text_3d_set_vol_id TO rock_label_set_vol_id;
ALTER TABLE optimizations RENAME COLUMN access_text_3d_set_id TO access_label_set_id;

NOTIFY pgrst, 'reload schema';
