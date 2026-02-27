# Geometry Format Specification

Canonical JSON formats for geometry assets stored in Supabase and rendered by the Selvagen web app. This document serves as the contract between the web app loaders and external clients (e.g., Grasshopper/Rhino plugin).

## Coordinate System

- **Web app (Three.js)**: Y-up, right-handed
- **Rhino/Grasshopper**: Z-up, right-handed
- **Conversion**: `(X_rhino, Y_rhino, Z_rhino)` → `(X_three, Z_rhino, -Y_rhino)`

All geometry stored in Supabase MUST be in the Three.js Y-up coordinate system. The C# plugin is responsible for converting from Rhino Z-up before upload.

---

## 1. BufferGeometry (Meshes)

**DB table**: `meshes`
**DB column**: `geometry_data` (JSONB)
**Web app loader**: `MeshLoader.tsx` → `THREE.BufferGeometryLoader().parse()`

### Schema

```json
{
  "metadata": {
    "version": 4.6,
    "type": "BufferGeometry",
    "generator": "selvagen-grasshopper"
  },
  "type": "BufferGeometry",
  "data": {
    "attributes": {
      "position": {
        "itemSize": 3,
        "type": "Float32Array",
        "array": [x, y, z, x, y, z, ...],
        "normalized": false
      },
      "normal": {
        "itemSize": 3,
        "type": "Float32Array",
        "array": [nx, ny, nz, nx, ny, nz, ...],
        "normalized": false
      }
    },
    "index": {
      "type": "Uint16Array",
      "array": [i0, i1, i2, i0, i1, i2, ...]
    }
  }
}
```

### Constraints

| Field | Requirement |
|-------|-------------|
| `data.attributes.position` | Required. `itemSize` must be `3`. `array` length must be a multiple of 3. |
| `data.attributes.normal` | Optional but recommended. Same length as position array. |
| `data.index` | Optional. If present, `array` length must be a multiple of 3 (triangle faces). |
| Faces | Must be triangulated. Rhino quad faces must be split into two triangles. |
| Coordinates | Y-up (Three.js convention). |

### DB Row Fields

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `name` | string | Yes | Display name |
| `project_id` | uuid | Yes | Parent project |
| `geometry_data` | JSONB | Yes* | BufferGeometry JSON |
| `geometry_url` | string | No | Storage URL for >5MB payloads (Phase 3) |
| `type` | string | No | Optional mesh classification |
| `metadata` | JSONB | No | Arbitrary key-value metadata |

---

## 2. CurveSet (Curves)

**DB table**: `curve_sets`
**DB column**: `geometry_data` (JSONB)
**Web app loader**: `CurveSetLoader.tsx`

### Schema

```json
{
  "curves": [
    {
      "id": "unique-curve-id",
      "points": [x, y, z, x, y, z, ...],
      "closed": false,
      "color": "#ffffff",
      "linewidth": 1.5,
      "metadata": {}
    }
  ]
}
```

### Constraints

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `curves` | array | Yes | Non-empty array of curve objects |
| `curves[].id` | string | Yes | Unique identifier within the set |
| `curves[].points` | number[] | Yes | Flat array `[x,y,z, x,y,z, ...]`. Length must be a multiple of 3. |
| `curves[].closed` | boolean | No | If `true`, the loader appends the first point to close the curve. Default `false`. |
| `curves[].color` | string | No | CSS color string (e.g., `"#ff0000"`). Default `"#ffffff"`. |
| `curves[].linewidth` | number | No | Line width in pixels. Default `1.5`. |
| `curves[].metadata` | object | No | Arbitrary key-value metadata. |

### Notes

- NURBS curves should be tessellated to polylines before upload (use `Curve.ToPolyline()` in Rhino).
- Points must be in Y-up coordinate system.

### DB Row Fields

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `name` | string | Yes | Display name |
| `project_id` | uuid | Yes | Parent project |
| `geometry_data` | JSONB | Yes* | CurveSet JSON |
| `geometry_url` | string | No | Storage URL for >5MB payloads (Phase 3) |

---

## 3. Text3DSet (Text Labels)

**DB table**: `text_3d_sets`
**DB column**: `text_data` (JSONB) ← note: different column name
**Web app loader**: `Text3DSetLoader.tsx`

### Schema

```json
{
  "labels": [
    {
      "id": "unique-label-id",
      "text": "Label content",
      "position": [x, y, z],
      "rotation": [rx, ry, rz],
      "fontSize": 2.5,
      "color": "#333333",
      "anchorX": "center",
      "anchorY": "middle",
      "metadata": {}
    }
  ]
}
```

### Constraints

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `labels` | array | Yes | Non-empty array of label objects |
| `labels[].id` | string | Yes | Unique identifier within the set |
| `labels[].text` | string | Yes | The displayed text content |
| `labels[].position` | [n, n, n] | Yes | 3D position as `[x, y, z]` tuple |
| `labels[].rotation` | [n, n, n] | No | Euler rotation in radians |
| `labels[].fontSize` | number | No | Font size. Default `2.5`. |
| `labels[].color` | string | No | CSS color string. Default `"#333333"`. |
| `labels[].anchorX` | string | No | `"left"`, `"center"`, or `"right"`. Default `"center"`. |
| `labels[].anchorY` | string | No | `"top"`, `"top-baseline"`, `"middle"`, `"bottom-baseline"`, or `"bottom"`. Default `"middle"`. |
| `labels[].metadata` | object | No | Arbitrary key-value metadata. |

### DB Row Fields

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `name` | string | Yes | Display name |
| `project_id` | uuid | Yes | Parent project |
| `text_data` | JSONB | Yes* | Text3DSet JSON |
| `geometry_url` | string | No | Storage URL for >5MB payloads (Phase 3) |

---

## API Endpoints (Edge Functions)

Base URL: `https://<project-ref>.supabase.co/functions/v1`

All endpoints require `Authorization: Bearer <jwt>` header.

| Method | Endpoint | Body | Returns |
|--------|----------|------|---------|
| POST | `/plugin-upload-mesh` | `{ name, project_id, geometry_data, type?, metadata? }` | `{ id, name, created_at }` |
| POST | `/plugin-upload-curves` | `{ name, project_id, geometry_data }` | `{ id, name, created_at }` |
| POST | `/plugin-upload-text3d` | `{ name, project_id, text_data }` | `{ id, name, created_at }` |
| GET | `/plugin-projects` | — | `[{ id, name, created_at }]` |

### Authentication

1. `POST https://<project-ref>.supabase.co/auth/v1/token?grant_type=password`
   - Body: `{ "email": "...", "password": "..." }`
   - Headers: `apikey: <supabase-anon-key>`
   - Returns: `{ access_token, refresh_token, ... }`
2. Use `access_token` as Bearer token for all subsequent requests.

### Error Responses

| Status | Meaning |
|--------|---------|
| 400 | Bad request (missing fields) |
| 401 | Unauthorized (missing/invalid JWT) |
| 403 | Forbidden (RLS policy denied access) |
| 405 | Method not allowed |
| 422 | Unprocessable entity (geometry validation failed) |
| 500 | Internal server error |
