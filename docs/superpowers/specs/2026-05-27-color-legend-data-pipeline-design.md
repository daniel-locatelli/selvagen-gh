# Color Legend Data Pipeline — Design Spec

**Date:** 2026-05-27
**Status:** Approved
**Scope:** End-to-end pipeline for Color Legend data from Grasshopper → Supabase → Platform

## Problem

The platform has 10 canvas component types. Three of them — Color Legend, Chart, and Table — currently have no way to receive data from Grasshopper. The existing data flow uses two patterns: (1) module PATCH with scalar values + asset IDs to PostgREST, and (2) geometry asset upload via Edge Functions. Neither supports the array-shaped data these components need (colors[], labels[], data points[], rows[]).

This spec addresses Color Legend. Chart and Table are out of scope but the architecture accommodates them later.

## Decisions

- **Approach A** — dedicated `color_legends` asset table (over embedding in module JSONB or a generic `visualization_data` table)
- **Both gradient and discrete** legend variants supported
- **Platform binding** — asset picker dropdown with auto-detection of module bindings (like 3D Layer Assets), plus expression binding under the hood. The user never types expressions.
- **Scope** — Color Legend end-to-end; Chart and Table are out of scope

## Architecture Overview

```
[GH Gradient] ──colors──→ [Mesh Coloring] ──mesh──→ [Upload Mesh] ──mesh_id──→ [Topo Slope]
               └──colors──→ [Upload Color Legend] ──legend_id──────────────────↗
                                    │
                                    ↓ (PostgREST upsert)
                             color_legends table
                                    │
                                    ↓ (module PATCH links legend_id)
                             topography.slope_legend_id
                                    │
                                    ↓ (platform auto-detects binding)
                             Legend Component renders
```

---

## 1. Data Model (Supabase)

### 1.1 New Table: `color_legends`

```sql
CREATE TABLE color_legends (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  project_id  UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  name        TEXT NOT NULL,
  variant     TEXT NOT NULL CHECK (variant IN ('gradient', 'discrete')),
  colors      TEXT[] NOT NULL,
  labels      TEXT[],
  domain_min  REAL,
  domain_max  REAL,
  unit        TEXT,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

  UNIQUE (project_id, name)
);
```

**Column semantics:**

| Column | Gradient | Discrete |
|--------|----------|----------|
| `colors` | Hex color stops (e.g. `['#2d7d46','#f5e642','#d63031']`) | One hex per category |
| `labels` | NULL (derived from domain) | Per-color category labels (e.g. `['Clay','Sand','Rock']`) |
| `domain_min` | Start value (e.g. `0`) | NULL |
| `domain_max` | End value (e.g. `45`) | NULL |
| `unit` | Optional (e.g. `%`, `°`, `m`) | Optional |

### 1.2 RLS Policy

Same pattern as `meshes`, `curve_sets`, `label_sets`:

```sql
ALTER TABLE color_legends ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Project members can manage color legends"
  ON color_legends FOR ALL
  USING (project_id IN (SELECT accessible_project_ids()));
```

### 1.3 Updated Trigger

Add `color_legends` to the existing `updated_at` trigger pattern.

### 1.4 Module FK Columns

Each module component that uploads a colored mesh gets a corresponding `_legend_id` FK column:

**topography** (5 columns):
- `elevation_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `contours_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `slope_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `access5_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `access8_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`

**geology** (5 columns):
- `soil_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `rock_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `coverage_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `depth_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `rippability_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`

**analyses** (4 columns):
- `rock_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `access_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `earthworks_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `retention_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`

**optimizations** (5 columns):
- `access_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `earth_terrain_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `earth_lots_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `earth_total_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`
- `retention_legend_id UUID REFERENCES color_legends(id) ON DELETE SET NULL`

All columns nullable (backward-compatible). `ON DELETE SET NULL` — if a legend is deleted, the FK is set to NULL and the platform falls back to manual config.

---

## 2. Grasshopper Plugin

### 2.1 New Component: `SelvagenUploadColorLegendComponent`

**Category:** Selvagen > Upload
**GUID:** New unique GUID
**Inherits:** `SelvagenUploadComponentBase` (or new lightweight base if upload base is too geometry-specific)

**Inputs:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Project | Guid | Yes | Project ID from project selector |
| Name | String | Yes | Legend display name (e.g. "Slope") |
| Variant | Integer | Yes | 0 = gradient, 1 = discrete (value list) |
| Colors | List\<Color\> | Yes | Rhino Color objects, converted to `#RRGGBB` hex |
| Labels | List\<String\> | No | Per-color labels (discrete only) |
| Domain Min | Number | No | Start of value range (gradient only) |
| Domain Max | Number | No | End of value range (gradient only) |
| Unit | String | No | Display unit (e.g. "%", "°", "m") |

**Outputs:**

| Name | Type | Description |
|------|------|-------------|
| Legend ID | Guid | UUID of the created/updated legend |
| Status | String | "Created", "Updated", or error message |

**Upsert logic:** Single atomic POST to PostgREST with `Prefer: resolution=merge-duplicates` header, leveraging the `UNIQUE (project_id, name)` constraint. No client-side check-then-write — one request handles both create and update.

**Color conversion:** `System.Drawing.Color` → `#RRGGBB` hex string via `$"#{c.R:X2}{c.G:X2}{c.B:X2}"`. Alpha channel is explicitly stripped — only R, G, B components are used. If a Rhino color has transparency, it is silently discarded.

### 2.2 SelvagenClient API Additions

New methods on `SelvagenClient`:

```csharp
Task<ColorLegendInfo[]> ListColorLegendsAsync(Guid projectId)
Task<ColorLegendInfo> GetColorLegendAsync(Guid legendId)
Task<ColorLegendInfo> UpsertColorLegendAsync(Guid projectId, string name, ColorLegendPayload payload)
// ^ Uses POST with Prefer: resolution=merge-duplicates header (atomic upsert on UNIQUE constraint)
Task DeleteColorLegendAsync(Guid legendId)
```

**`ColorLegendPayload`** model:
```csharp
class ColorLegendPayload
{
    public string Variant { get; set; }     // "gradient" or "discrete"
    public string[] Colors { get; set; }     // hex codes
    public string[] Labels { get; set; }     // nullable
    public float? DomainMin { get; set; }
    public float? DomainMax { get; set; }
    public string Unit { get; set; }         // nullable
}
```

**`ColorLegendInfo`** response model:
```csharp
class ColorLegendInfo
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; }
    public string Variant { get; set; }
    public string[] Colors { get; set; }
    public string[] Labels { get; set; }
    public float? DomainMin { get; set; }
    public float? DomainMax { get; set; }
    public string Unit { get; set; }
}
```

### 2.3 Modified Module Components (18 components)

Each module component that uploads a mesh gains an **optional** `Legend ID` (Guid) input parameter. When provided, the legend ID is included in the PATCH payload alongside the existing mesh ID.

**Example — `TopoSlopeComponent`:**
- Before PATCH: `{ "slope_mesh_id": "..." }`
- After PATCH: `{ "slope_mesh_id": "...", "slope_legend_id": "..." }`

If the Legend ID input is not wired, the field is omitted from the PATCH (backward-compatible).

**Components affected:**

| Module | Components |
|--------|-----------|
| Topography | TopoElevation, TopoContours, TopoSlope, TopoAccess5, TopoAccess8 |
| Geology | GeoSoil, GeoRock, GeoCoverage, GeoDepth, GeoRippability |
| Analyses | AnlRock, AnlAccess, AnlEarthworks, AnlRetention |
| Optimizations | OptAccess, OptEarthTerrain, OptEarthLots, OptEarthTotal, OptRetention |

---

## 3. Platform (Web App)

### 3.1 Query Hooks

**`useColorLegend(legendId: string | null)`** — fetches a single legend by ID. Enabled only when `legendId` is truthy.

**`useColorLegends(projectId: string)`** — fetches all legends for a project. Used by the Inspector dropdown.

Both follow existing TanStack Query patterns in the codebase.

**DataContext update:** The existing module queries (`useSlideDataContext`) must be updated to SELECT the new `_legend_id` columns so they're available in the DataContext for binding detection.

### 3.2 Legend Component Data Flow

```
resolved.legend_id ?? config.legend_id
         │
         ↓
  useColorLegend(legendId)
         │
         ↓
  Merge: legend data > manual config
    variant  = legend?.variant  ?? config.variant
    colors   = legend?.colors   ?? config.colors
    labels   = legend?.labels   ?? config.labels
    title    = resolved.title   ?? legend?.name ?? config.title
    startLabel = resolved.startLabel ?? (legend ? `${legend.domain_min}${legend.unit}` : config.startLabel)
    endLabel   = resolved.endLabel   ?? (legend ? `${legend.domain_max}${legend.unit}` : config.endLabel)
```

Fetched legend data takes precedence over manual config. Expression-bound title/labels override everything.

### 3.3 Updated `bindingFields`

```typescript
// Before:
bindingFields: ['title', 'startLabel', 'endLabel']

// After:
bindingFields: ['title', 'startLabel', 'endLabel', 'legend_id']
```

### 3.4 Inspector: Auto-Binding Detection

When the user picks a legend from the dropdown:

1. Scan the `DataContext` (topography, geology, analyses, optimizations) for ALL `_legend_id` columns whose value matches the selected legend's ID.
2. **Exactly one match** → auto-bind as expression `${namespace.field}`, show chain link icon, display "Bound to {module} → {analysis}".
3. **Multiple matches** → show disambiguation UI listing all matching module fields (e.g., "topography → slope", "analyses → rock"). User picks which binding to use.
4. **No match** → store as static `config.legend_id`, no chain link, display "Static reference — not bound to a module".

```typescript
function findModuleBindings(legendId: string, ctx: DataContext) {
  const matches: Array<{ namespace: string; field: string }> = [];
  for (const [namespace, moduleData] of Object.entries(ctx)) {
    if (!moduleData) continue;
    for (const [field, value] of Object.entries(moduleData)) {
      if (field.endsWith('_legend_id') && value === legendId)
        matches.push({ namespace, field });
    }
  }
  return matches;
}
```

### 3.5 Inspector UI

The Legend Inspector gains a "Data Source" section above existing manual config fields:

- **Dropdown** listing all `color_legends` for the current project (via `useColorLegends`)
- **Chain link icon** (🔗) if the selected legend is module-bound
- **Binding hint** showing "Bound to topography → slope" or "Static reference"
- **Preview** showing the color gradient/discrete swatches below the dropdown
- **Manual config fields** remain below as overrides (existing behavior preserved)

---

## 4. Known Limitations

- **Orphaned legends on rename:** If a legend is renamed in Grasshopper, the upsert creates a new record (new name), leaving the old one unreferenced. These orphans are ~200 bytes each and accumulate slowly. A cleanup mechanism will be addressed when the legend management UI is built. Not a blocker — no performance or correctness impact.

## 5. Out of Scope

- Chart data pipeline (no real-world GH use case yet)
- Table data pipeline (no real-world GH use case yet)
- Chart/Table GH components
- Legend deletion/management UI in the platform
- Legend editing in the platform (data is GH-authored)
- Download Color Legend GH component

---

## 6. Migration Checklist

Single Supabase migration file containing:
1. `CREATE TABLE color_legends` with all columns and constraints
2. `ENABLE ROW LEVEL SECURITY` + RLS policy
3. `updated_at` trigger
4. `ALTER TABLE topography ADD COLUMN` × 5
5. `ALTER TABLE geology ADD COLUMN` × 5
6. `ALTER TABLE analyses ADD COLUMN` × 4
7. `ALTER TABLE optimizations ADD COLUMN` × 5
