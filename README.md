# Selvagen Grasshopper Plugin

A Grasshopper plugin for Rhino that pushes meshes, curves, and text labels from Rhino/Grasshopper to the [Selvagen](https://selvagen.com) web platform.

Geometry is converted from Rhino's Z-up coordinate system to Three.js Y-up format and uploaded to a Supabase backend via Edge Functions.

## Solution Structure

```
Selvagen.sln
├── src/
│   ├── Selvagen.Core        Core library: models, converters, API client (net48 + net7.0)
│   └── Selvagen.GH          Grasshopper components (net48 + net7.0)
├── tests/
│   └── Selvagen.Core.Tests  Unit tests (net8.0, xUnit)
└── docs/
    └── GEOMETRY_FORMAT.md   JSON schema contract for geometry assets
```

`Selvagen.Core` and `Selvagen.GH` both multi-target `net48` and `net7.0`, supporting Rhino 7 (.NET Framework 4.8) and Rhino 8 (.NET 7.0) natively. The test project targets `net8.0` and references Core's `net7.0` build via forward compatibility.

## Grasshopper Components

| Component | Tab | Nickname | Description |
|-----------|-----|----------|-------------|
| **Selvagen Login** | Auth | SvLogin | Authenticate with email/password, outputs a client object |
| **Selvagen Clients** | Data | SvClients | List firm clients |
| **Selvagen Projects** | Data | SvProjects | List projects (with optional client filter) |
| **Selvagen List Assets** | Data | SvAssets | List meshes, curves, or labels for a project |
| **Selvagen Project Modules** | Data | SvModules | Check which modules have records |
| **Selvagen Delete Asset** | Data | SvDelete | Delete a geometry asset by ID |
| **Selvagen Upload Mesh** | Upload | SvUpMesh | Convert and upload a Rhino mesh |
| **Selvagen Upload Curves** | Upload | SvUpCrv | Tessellate and upload curves |
| **Selvagen Upload Labels** | Upload | SvUpLbl | Upload 3D text labels |
| **Selvagen Upload Animation** | Upload | SvUpAnim | Upload a mesh sequence as animation |
| **Selvagen Topography** | Modules | SvTopo | Populate topography data (28 fields) |
| **Selvagen Geology** | Modules | SvGeo | Populate geology data (14 fields) |
| **Selvagen Analyses** | Modules | SvAnalyses | Populate analyses data (22 fields) |
| **Selvagen Optimizations** | Modules | SvOptim | Populate optimizations data (27 fields) |

For full input/output documentation, examples, and troubleshooting, see [`docs/PLUGIN_GUIDE.md`](docs/PLUGIN_GUIDE.md).

### Typical Workflow

```
[Login] → Client → [Projects] → Project ID ─┬→ [Upload Mesh]   → MeshID ──┐
                                              ├→ [Upload Curves] → CrvID   ├→ [Topography]
                         Rhino Geometry ──────┤→ [Upload Labels] → LblID ──┘
                                              └→ [Upload Animation]
```

1. Drop a **Selvagen Login** component and enter your email and password.
2. Use **Selvagen Projects** to list your projects and pick a project ID.
3. Connect geometry and the project ID to any **Upload** component, then toggle `Go` to upload.
4. Wire the resulting asset IDs into a **Module** component (Topography, Geology, etc.) along with computed scalar values, then toggle `Go` to push module data.

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
- .NET Framework 4.8 (Rhino 7) or .NET 7.0+ (Rhino 8)
- A Selvagen account with a Supabase project URL and anon key

## Building

```bash
dotnet build Selvagen.sln
```

Copy the output from the appropriate target directory into your Grasshopper libraries folder:

- **Rhino 7 (net48):** `src/Selvagen.GH/bin/Debug/net48/`
- **Rhino 8 (net7.0):** `src/Selvagen.GH/bin/Debug/net7.0/`

Library folder locations:

- **Windows:** `%APPDATA%\Grasshopper\Libraries\`
- **macOS:** `~/Library/Application Support/McNeel/Rhinoceros/Grasshopper/Libraries/`

## Running Tests

```bash
dotnet test
```

## Target Framework Guidance

Rhino 8 moved from .NET Framework to .NET Core. The .NET runtime used depends on the Rhino version:

| Rhino Version | Windows Runtime | macOS Runtime |
|---------------|-----------------|---------------|
| Rhino 7 | .NET Framework 4.8 | Mono |
| Rhino 8 | .NET 7.0 (Framework 4.8 fallback) | .NET 7.0 |
| Rhino 8.20+ | .NET 8.0 (Framework 4.8 fallback) | .NET 8.0 |

This plugin multi-targets both `net48` and `net7.0`, supporting Rhino 7 and Rhino 8 natively (including macOS). The build produces separate assemblies in `net48/` and `net7.0/` output directories.

For multi-targeted Yak packages, binaries are placed in `net48/` and `net7.0/` subdirectories within the package. See the [Yak package anatomy guide](https://developer.rhino3d.com/guides/yak/the-anatomy-of-a-package/) and the [ShapeDiver plugin template](https://github.com/shapediver/GrasshopperPluginTemplate) for a community reference.

## API Endpoints

All endpoints require a JWT Bearer token obtained via the login component.

### Edge Functions

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/functions/v1/plugin-upload-mesh` | Upload a mesh |
| POST | `/functions/v1/plugin-upload-curves` | Upload a curve set |
| POST | `/functions/v1/plugin-upload-text3d` | Upload text labels |
| GET | `/functions/v1/plugin-projects` | List user projects |

### PostgREST (Direct Table Access)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/rest/v1/firm_clients` | List firm clients |
| GET | `/rest/v1/projects` | List projects (with filters) |
| GET | `/rest/v1/meshes` | List mesh assets |
| GET | `/rest/v1/curve_sets` | List curve set assets |
| GET | `/rest/v1/text_3d_sets` | List text 3D set assets |
| GET | `/rest/v1/{module_table}` | Query module records (topography, geology, etc.) |
| POST | `/rest/v1/{module_table}` | Create module record |
| PATCH | `/rest/v1/{module_table}` | Update module record fields |
| DELETE | `/rest/v1/{asset_table}` | Delete geometry asset |
| POST | `/rest/v1/animation_sequences` | Create animation sequence |
| POST | `/rest/v1/animation_frames` | Upload animation frame |

## Developer Resources

### Official Rhino/Grasshopper Documentation

- [Rhino Developer Home](https://developer.rhino3d.com/) — Starting point for all Rhino plugin development
- [Grasshopper Developer Guides](https://developer.rhino3d.com/guides/grasshopper/) — Component anatomy, data trees, multi-threading, custom GUIs, and more
- [Your First Component (Windows)](https://developer.rhino3d.com/guides/grasshopper/your-first-component-windows/) — Tutorial for creating a Grasshopper component from scratch
- [Installing Tools (Windows)](https://developer.rhino3d.com/guides/grasshopper/installing-tools-windows/) — Setting up Visual Studio templates for Grasshopper development

### .NET Migration (Rhino 7 → 8)

- [Moving to .NET Core](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/) — Official migration guide for Rhino 8's .NET runtime change
- [What's New in RhinoCommon](https://developer.rhino3d.com/guides/rhinocommon/whats-new/) — API changes in Rhino 8

### API References

- [RhinoCommon API](https://developer.rhino3d.com/api/rhinocommon/) — Full `Rhino.Geometry`, `Rhino.DocObjects`, etc.
- [Grasshopper SDK](https://developer.rhino3d.com/api/grasshopper/) — `Grasshopper.Kernel`, `GH_Component`, parameters, data types
- [All Rhino APIs](https://developer.rhino3d.com/api/) — Index of all available APIs (RhinoCommon, Grasshopper, C++, Eto, rhino3dm, Compute)

### NuGet Packages

- [RhinoCommon](https://www.nuget.org/packages/RhinoCommon/) — Rhino .NET SDK
- [Grasshopper](https://www.nuget.org/packages/Grasshopper/) — Grasshopper SDK (pulls in RhinoCommon)
- [Rhino.Templates](https://www.nuget.org/packages/Rhino.Templates) — `dotnet new` templates for Rhino/Grasshopper plugins
- [Rhino.Testing](https://www.nuget.org/packages/Rhino.Testing) — NUnit testing framework for Rhino 8+ plugins
- [Using NuGet Guide](https://developer.rhino3d.com/guides/rhinocommon/using-nuget/) — How to reference Rhino packages correctly

### Community & Samples

- [Rhino Developer Samples](https://github.com/mcneel/rhino-developer-samples) — Official sample plugins (branch `8` for Rhino 8)
- [ShapeDiver Plugin Template](https://github.com/shapediver/GrasshopperPluginTemplate) — Multi-targeted Grasshopper plugin template for Rhino 7 + 8
- [Rhino Developer Forum](https://discourse.mcneel.com/c/rhino-developer/) — Community support for plugin development
- [Yak Package Anatomy](https://developer.rhino3d.com/guides/yak/the-anatomy-of-a-package/) — How to structure multi-targeted packages for distribution

## License

All rights reserved.
