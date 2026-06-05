# Selvagen Grasshopper Plugin

A Grasshopper plugin for Rhino that pushes meshes, curves, and text labels from Rhino/Grasshopper to the [Selvagen](https://selvagen.com) web platform.

Geometry is converted from Rhino's Z-up coordinate system to Three.js Y-up format and uploaded to a Supabase backend via Edge Functions.

## Solution Structure

```
Selvagen.sln
├── src/
│   ├── Selvagen.Core        Core library: models, converters, API client (net48 + net8.0)
│   └── Selvagen.GH          Grasshopper components (net48 + net8.0 + net8.0-windows)
├── tests/
│   └── Selvagen.Core.Tests  Unit tests (net8.0, xUnit)
└── docs/
    └── GEOMETRY_FORMAT.md   JSON schema contract for geometry assets
```

`Selvagen.Core` multi-targets `net48` and `net8.0`; `Selvagen.GH` additionally targets `net8.0-windows` for the Windows-specific Forms UI. This supports Rhino 7 (.NET Framework 4.8) and Rhino 8 (.NET 8.0) natively. The test project targets `net8.0`.

## Grasshopper Components

| Component | Tab | Nickname | Description |
|-----------|-----|----------|-------------|
| **Selvagen Login** | Auth | SvLogin | Authenticate with email/password, outputs a client object |
| **Selvagen Clients** | Data | SvClients | List firm clients |
| **Selvagen Projects** | Data | SvProjects | List projects (with optional client filter) |
| **Selvagen List Assets** | Data | SvAssets | List meshes, curves, or labels for a project |
| **Selvagen Delete Asset** | Data | SvDelete | Delete a geometry asset by ID |
| **Selvagen Upload Mesh** | Upload | SvUpMesh | Convert and upload a Rhino mesh |
| **Selvagen Upload Curves** | Upload | SvUpCrv | Tessellate and upload curves |
| **Selvagen Upload Labels** | Upload | SvUpLbl | Upload 3D text labels, with optional per-label color, justification, and size |
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
- .NET Framework 4.8 (Rhino 7) or .NET 8.0+ (Rhino 8)
- A Selvagen account with a Supabase project URL and anon key

## Installation

Most users don't need to build from source — install a released build directly.

### Option A — Rhino Package Manager (recommended)

1. In Rhino 8, run the `PackageManager` command.
2. Search for **`selvagen`**.
3. Click **Install**, then restart Rhino.

New versions show up in the Package Manager automatically whenever a release is published.

### Option B — Download from GitHub Releases

1. Open the [Releases page](../../releases) and download the latest
   `Selvagen-vX.Y.Z-rh8-win.zip`.
2. Extract its contents into a `Selvagen` subfolder of your Grasshopper Libraries folder:
   - **Windows:** `%APPDATA%\Grasshopper\Libraries\Selvagen\`
3. Restart Rhino.

> The released package targets **Rhino 8 on Windows** (`net8.0-windows`). For Rhino 7 or
> macOS, build from source (below).

## Building

Build from source to develop the plugin or to target Rhino 7 (`net48`) / macOS:

```bash
dotnet build Selvagen.sln
```

Copy the output from the appropriate target directory into your Grasshopper libraries folder:

- **Rhino 7 (net48):** `src/Selvagen.GH/bin/Debug/net48/`
- **Rhino 8 (net8.0-windows):** `src/Selvagen.GH/bin/Debug/net8.0-windows/`

Library folder locations:

- **Windows:** `%APPDATA%\Grasshopper\Libraries\`
- **macOS:** `~/Library/Application Support/McNeel/Rhinoceros/Grasshopper/Libraries/`

## Running Tests

```bash
dotnet test
```

## Running integration tests

End-to-end tests drive a live Grasshopper instance through the Cordyceps MCP
server. See [`docs/INTEGRATION_TESTING.md`](docs/INTEGRATION_TESTING.md) for
prerequisites and setup. Quick run:

```powershell
$env:SELVAGEN_TEST_EMAIL = "you@example.com"
$env:SELVAGEN_TEST_PASSWORD = "your-test-password"
pwsh tests/integration/run.ps1
```

## Releases & Versioning

Versioning is automated with [release-please](https://github.com/googleapis/release-please)
driven by [Conventional Commits](https://www.conventionalcommits.org/). The version
number is computed from commit messages — you never bump it by hand.

| Commit prefix | Example | Version effect |
|---|---|---|
| `feat:` | `feat(gh): add Geology component` | **minor** bump (1.2.0 → 1.3.0) |
| `fix:` / `perf:` / `refactor:` | `fix(core): correct Y-up flip` | **patch** bump (1.2.0 → 1.2.1) |
| `feat!:` or `BREAKING CHANGE:` footer | `feat(gh)!: drop Rhino 7 support` | **major** bump (1.2.0 → 2.0.0) |
| `docs:` / `chore:` / `ci:` | `docs: update README` | no release on its own |

### How a release happens

1. You merge normal `feat:`/`fix:` commits to `main`.
2. The **Release** workflow keeps a standing **"Release PR"** open, showing the next
   version and an auto-generated `CHANGELOG.md`.
3. When you're ready to ship, **merge the Release PR**. release-please then:
   - bumps `<Version>` in both `.csproj` files and `manifest.yml` (kept in sync via the
     `x-release-please-version` markers),
   - tags the commit (`vX.Y.Z`) and publishes a **[GitHub Release](../../releases)**.
4. In the **same** workflow run, packaging builds the Rhino 8 / Windows target, zips it as
   `Selvagen-vX.Y.Z-rh8-win.zip`, attaches it to the Release, and (if configured) pushes a
   `.yak` package to Rhino's Package Manager.

The **CI** workflow builds all target frameworks and runs the test suite on every push and
pull request — a release is only ever cut from green `main`.

### One-time GitHub setup

These must be done once in the repository settings before the first release:

1. **Settings → Actions → General → Workflow permissions:** select *Read and write
   permissions* and tick *Allow GitHub Actions to create and approve pull requests*
   (release-please needs this to open the Release PR).
2. **Yak publishing (optional).** To auto-publish to Rhino's Package Manager:
   - Run `yak login --ci` locally (requires Rhino installed) to mint a non-expiring token.
   - Add it as a repo secret named **`YAK_TOKEN`** (Settings → Secrets and variables →
     Actions). Without this secret the Yak step is skipped; the GitHub Release zip is still
     produced.
   - The package name `selvagen` is reserved on first push by the account that owns the token.

## Target Framework Guidance

Rhino 8 moved from .NET Framework to .NET Core. The .NET runtime used depends on the Rhino version:

| Rhino Version | Windows Runtime | macOS Runtime |
|---------------|-----------------|---------------|
| Rhino 7 | .NET Framework 4.8 | Mono |
| Rhino 8 | .NET 7.0 (Framework 4.8 fallback) | .NET 7.0 |
| Rhino 8.20+ | .NET 8.0 (Framework 4.8 fallback) | .NET 8.0 |

This plugin multi-targets `net48`, `net8.0`, and `net8.0-windows`, supporting Rhino 7 and Rhino 8 natively (including macOS). The build produces separate assemblies in `net48/`, `net8.0/`, and `net8.0-windows/` output directories.

For multi-targeted Yak packages, binaries are placed in `net48/` and `net8.0/` subdirectories within the package. See the [Yak package anatomy guide](https://developer.rhino3d.com/guides/yak/the-anatomy-of-a-package/) and the [ShapeDiver plugin template](https://github.com/shapediver/GrasshopperPluginTemplate) for a community reference.

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
