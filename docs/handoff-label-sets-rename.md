# Platform Handoff: Label Sets Rename + Justification

**Date:** 2026-05-26
**Status:** Ready for platform implementation
**Context:** The Grasshopper plugin and Supabase database have been updated. The web platform must be updated to match.

## 1. Database Changes (ALREADY APPLIED)

The following renames have been applied to the GEN.BOARD Supabase database:

| Before | After |
|--------|-------|
| Table `text_3d_sets` | Table `label_sets` |
| `topography.contours_text_3d_set_id` | `topography.contours_label_set_id` |
| `analyses.access_text_3d_set_id` | `analyses.access_label_set_id` |
| `analyses.rock_text_3d_set_height_id` | `analyses.rock_label_set_height_id` |
| `analyses.rock_text_3d_set_vol_id` | `analyses.rock_label_set_vol_id` |
| `optimizations.access_text_3d_set_id` | `optimizations.access_label_set_id` |

The `text_data` JSONB column name is UNCHANGED (out of scope).

PostgREST schema cache has been reloaded. API path is now `/rest/v1/label_sets`.

## 2. Edge Function Rename

| Before | After |
|--------|-------|
| `plugin-upload-text3d` | `plugin-upload-labels` |

The Grasshopper plugin now calls `/functions/v1/plugin-upload-labels`. The old endpoint will 404.

## 3. Code References to Update

Search the web platform codebase for these patterns and rename:

| Search Pattern | Replace With |
|---------------|-------------|
| `text_3d_sets` | `label_sets` |
| `text3d` / `text_3d` | `label` / `labels` (context-dependent) |
| `Text3DSet` | `LabelSet` |
| `Text3DSetLoader` | `LabelSetLoader` |
| `contours_text_3d_set_id` | `contours_label_set_id` |
| `access_text_3d_set_id` | `access_label_set_id` |
| `rock_text_3d_set_height_id` | `rock_label_set_height_id` |
| `rock_text_3d_set_vol_id` | `rock_label_set_vol_id` |

## 4. Justification — New JSON Fields

The `text_data` JSONB column now includes `anchorX` and `anchorY` on label entries:

```json
{
  "labels": [
    {
      "id": "label-0",
      "text": "Hello",
      "position": [10, 5, 0],
      "anchorX": "center",
      "anchorY": "top"
    }
  ]
}
```

### Anchor Values

| anchorX | anchorY | Meaning |
|---------|---------|---------|
| `"left"` | `"bottom"` | Bottom-left aligned |
| `"center"` | `"bottom"` | Bottom-center aligned |
| `"right"` | `"bottom"` | Bottom-right aligned |
| `"left"` | `"middle"` | Middle-left aligned |
| `"center"` | `"middle"` | Middle-center (default) |
| `"right"` | `"middle"` | Middle-right aligned |
| `"left"` | `"top"` | Top-left aligned |
| `"center"` | `"top"` | Top-center aligned |
| `"right"` | `"top"` | Top-right aligned |

If `anchorX` or `anchorY` is null/missing, the platform should default to `"center"` / `"middle"` respectively.

These values map directly to troika-three-text `anchorX`/`anchorY` props if the platform uses that library.

## 5. UI Label Changes

Any user-facing strings that say "Text 3D Sets" or "Text 3D" should be changed to "Label Sets" or "Labels".
