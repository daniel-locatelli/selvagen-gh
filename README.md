# Selvagen Grasshopper Plugin

A Grasshopper plugin for Rhino that pushes meshes, curves, and text labels from Rhino/Grasshopper to the [Selvagen](https://selvagen.com) web platform.

Geometry is converted from Rhino's Z-up coordinate system to Three.js Y-up format and uploaded to a Supabase backend via Edge Functions.

## Solution Structure

```
Selvagen.sln
├── src/
│   ├── Selvagen.Core        Core library: models, converters, API client (net48 + net8.0)
│   └── Selvagen.GH          Grasshopper components (net48)
├── tests/
│   └── Selvagen.Core.Tests  Unit tests (net8.0, xUnit)
└── docs/
    └── GEOMETRY_FORMAT.md   JSON schema contract for geometry assets
```

## Grasshopper Components

| Component | Tab | Description |
|-----------|-----|-------------|
| **Selvigen Login** | Auth | Authenticate with email/password, outputs a client object |
| **Selvigen Projects** | Data | List available projects (IDs and names) |
| **Selvigen Upload Mesh** | Upload | Convert and upload a Rhino mesh |
| **Selvigen Upload Curves** | Upload | Tessellate and upload curves |
| **Selvigen Upload Labels** | Upload | Upload 3D text labels |

### Typical Workflow

```
[Login] → Client → [Projects] → Project ID ─┐
                                              ├→ [Upload Mesh]
                         Rhino Geometry ──────┤→ [Upload Curves]
                                              └→ [Upload Labels]
```

1. Drop a **Selvigen Login** component, wire in your Supabase URL, anon key, email, and password.
2. Use **Selvigen Projects** to list your projects and pick a project ID.
3. Connect geometry and the project ID to any **Upload** component, then toggle the `Go` input to upload.

## Coordinate System

Rhino (Z-up) is converted to Three.js (Y-up) using:

```
(X, Y, Z)_rhino  →  (X, Z, -Y)_three
```

All geometry stored in Supabase is in Y-up coordinates. The plugin handles this conversion automatically.

## Supported Geometry Types

- **Meshes** — Converted to Three.js `BufferGeometry` JSON. Quad faces are triangulated. Normals are computed automatically.
- **Curves** — NURBS curves are tessellated to polylines. Points are stored as flat arrays `[x, y, z, ...]`.
- **Text Labels** — Created from `TextDot` objects or point/text pairs. Supports font size, color, and anchor options.

See [`docs/GEOMETRY_FORMAT.md`](docs/GEOMETRY_FORMAT.md) for the full JSON schema specification.

## Prerequisites

- [Rhino 7 or 8](https://www.rhino3d.com/) with Grasshopper
- .NET Framework 4.8 (Rhino 7) or .NET 8 (Rhino 8)
- A Selvagen account with a Supabase project URL and anon key

## Building

```bash
dotnet build Selvagen.sln
```

Copy the output from `src/Selvagen.GH/bin/Debug/net48/` into your Grasshopper libraries folder:

- **Windows:** `%APPDATA%\Grasshopper\Libraries\`
- **macOS:** `~/Library/Application Support/McNeel/Rhinoceros/Grasshopper/Libraries/`

## Running Tests

```bash
dotnet test
```

## API Endpoints

All endpoints require a JWT Bearer token obtained via the login component.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/functions/v1/plugin-upload-mesh` | Upload a mesh |
| POST | `/functions/v1/plugin-upload-curves` | Upload a curve set |
| POST | `/functions/v1/plugin-upload-text3d` | Upload text labels |
| GET | `/functions/v1/plugin-projects` | List user projects |

## License

All rights reserved.
