# Upload Button, Label Sets Rename, and Justification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace wired Upload boolean with on-canvas button, rename "Text 3D Sets" → "Label Sets" across the codebase, and add justification support to labels.

**Architecture:** The upload button follows the existing "Update" button pattern from `SelvagenSelectorAttributes` — custom `GH_ComponentAttributes` rendering a teal button. The rename is a batch refactor touching models, converters, client, and components. Justification maps GH's integer (0-8) to `anchorX`/`anchorY` strings in the converter, never storing the integer in JSON.

**Tech Stack:** C# / .NET 7+4.8, RhinoCommon, Grasshopper SDK, Supabase PostgREST, System.Text.Json

**Spec:** `docs/superpowers/specs/2026-05-26-upload-button-rename-justification-design.md`

---

## Phase 1: Integrated Upload Button

### Task 1: Create SelvagenUploadAttributes and update base class

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenUploadAttributes.cs`
- Modify: `src/Selvagen.GH/Components/SelvagenUploadComponentBase.cs`

- [ ] **Step 1: Create SelvagenUploadAttributes.cs**

Create `src/Selvagen.GH/Components/SelvagenUploadAttributes.cs`:

```csharp
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadAttributes : GH_ComponentAttributes
    {
        private const int ButtonHeight = 22;
        private const int TopPadding = 4;
        private const int InnerSidePadding = 6;

        private RectangleF _buttonRect;
        private bool _buttonPressed;
        private float? _naturalHeight;

        public SelvagenUploadAttributes(SelvagenUploadComponentBase owner) : base(owner) { }

        private SelvagenUploadComponentBase UploadOwner => (SelvagenUploadComponentBase)Owner;

        protected override void Layout()
        {
            base.Layout();

            if (!_naturalHeight.HasValue)
                _naturalHeight = Bounds.Height;

            var extra = TopPadding + ButtonHeight;
            var bounds = Bounds;
            bounds.Height = _naturalHeight.Value + extra;
            Bounds = bounds;

            _buttonRect = new RectangleF(
                Bounds.Left + InnerSidePadding,
                Bounds.Top + _naturalHeight.Value + TopPadding / 2f,
                Bounds.Width - 2 * InnerSidePadding,
                ButtonHeight);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;
            RenderButton(graphics);
        }

        private void RenderButton(Graphics g)
        {
            var r = _buttonRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            var path = RoundedRect(r, 3f);

            Color topColor, bottomColor;
            if (_buttonPressed)
            {
                topColor = Color.FromArgb(100, 160, 140);
                bottomColor = Color.FromArgb(40, 100, 80);
            }
            else
            {
                topColor = Color.FromArgb(60, 140, 120);
                bottomColor = Color.FromArgb(20, 80, 60);
            }

            using (var fill = new LinearGradientBrush(r, topColor, bottomColor, 90f))
                g.FillPath(fill, path);
            using (var pen = new Pen(Color.FromArgb(10, 50, 40), 1f))
                g.DrawPath(pen, path);

            var label = UploadOwner.IsUploading ? "Uploading..." : "Upload";
            using (var font = GH_FontServer.NewFont(GH_FontServer.Standard, 6f))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(label, font, Brushes.White, r, fmt);

            path.Dispose();
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left && _buttonRect.Contains(e.CanvasLocation))
            {
                if (UploadOwner.IsUploading) return GH_ObjectResponse.Handled;

                _buttonPressed = true;
                sender.Refresh();

                var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    _buttonPressed = false;
                    sender.Refresh();
                };
                timer.Start();

                UploadOwner.RequestUpload();
                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseDown(sender, e);
        }
    }
}
```

- [ ] **Step 2: Update SelvagenUploadComponentBase**

Replace the entire contents of `src/Selvagen.GH/Components/SelvagenUploadComponentBase.cs` with:

```csharp
using System;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public abstract class SelvagenUploadComponentBase : GH_Component
    {
        private bool _uploadRequested;

        protected SelvagenUploadComponentBase(string name, string nickname, string description)
            : base(name, nickname, description, "Selvagen", "08 Assets") { }

        public bool IsUploading { get; protected set; }

        public bool UploadRequested
        {
            get
            {
                if (!_uploadRequested) return false;
                _uploadRequested = false;
                return true;
            }
        }

        public void RequestUpload()
        {
            _uploadRequested = true;
            ExpireSolution(true);
        }

        public override void CreateAttributes()
        {
            m_attributes = new SelvagenUploadAttributes(this);
        }

        protected void SetReady(IGH_DataAccess DA, int statusIndex)
        {
            DA.SetData(statusIndex, "Ready to upload.");
        }

        protected void SetUploadError(IGH_DataAccess DA, int statusIndex, Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
            DA.SetData(statusIndex, $"Error: {msg}");
        }

        protected void ForceCanvasRefresh()
        {
            try { Grasshopper.Instances.ActiveCanvas?.Refresh(); }
            catch { }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
    }
}
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Note: Build will show errors in the 4 upload components because they still reference `SetWaiting(DA)` and read the old `upload` boolean. This is expected — the next 4 tasks fix each component.

- [ ] **Step 4: Commit (even with build errors in consumers)**

```powershell
git add src/Selvagen.GH/Components/SelvagenUploadAttributes.cs src/Selvagen.GH/Components/SelvagenUploadComponentBase.cs
git commit -m "feat(ui): add SelvagenUploadAttributes on-canvas upload button"
```

---

### Task 2: Update UploadMeshComponent to use upload button

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenUploadMeshComponent.cs`

- [ ] **Step 1: Rewrite the component**

Replace the entire contents of `src/Selvagen.GH/Components/SelvagenUploadMeshComponent.cs` with:

```csharp
using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadMeshComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadMeshComponent()
            : base("Upload Mesh", "SvUpMesh",
                "Upload a Rhino mesh to the platform. [Upload de Malha]")
        { }

        public override Guid ComponentGuid => new Guid("d3e4f5a6-b7c8-9012-3456-7890abcdef12");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Rhino mesh to upload", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Display name for the mesh", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Mesh ID", "MshID", "ID of the created mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            Mesh mesh = null;

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref mesh);
            DA.GetData(2, ref name);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || mesh == null || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Mesh, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var geometry = MeshConverter.ToBufferGeometry(mesh);
                var result = Task.Run(() => client.UploadMeshAsync(projectId, name, geometry)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name}");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, 1, ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadMesh");
    }
}
```

- [ ] **Step 2: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Build may still fail due to other upload components — that's OK, we fix them in the next tasks.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenUploadMeshComponent.cs
git commit -m "feat(ui): replace Upload Mesh boolean toggle with on-canvas button"
```

---

### Task 3: Update UploadCurvesComponent to use upload button

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenUploadCurvesComponent.cs`

- [ ] **Step 1: Rewrite the component**

Replace the entire contents of `src/Selvagen.GH/Components/SelvagenUploadCurvesComponent.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadCurvesComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadCurvesComponent()
            : base("Upload Curves", "SvUpCrv",
                "Upload curves from Rhino to the platform. [Upload de Curvas]")
        { }

        public override Guid ComponentGuid => new Guid("e4f5a6b7-c8d9-0123-4567-890abcdef123");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curves", "Crv", "Rhino curves to upload", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the curve set", GH_ParamAccess.item);
            pManager.AddColourParameter("Color", "C", "Per-curve colour (one per curve, or a single colour for all)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Per-curve line thickness in pixels (one per curve, or a single value for all)", GH_ParamAccess.list);

            Params.Input[3].Optional = true;
            Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Curve Set ID", "CrvID", "ID of the created curve set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var curves = new List<Curve>();
            var colors = new List<Color>();
            var thicknesses = new List<double>();

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, curves);
            DA.GetData(2, ref name);
            DA.GetDataList(3, colors);
            DA.GetDataList(4, thicknesses);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || curves.Count == 0 || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Curves, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var curveSet = CurveConverter.ToCurveSet(
                    curves,
                    colors: colors.Count > 0 ? colors : null,
                    linewidths: thicknesses.Count > 0 ? thicknesses : null);
                var result = Task.Run(() => client.UploadCurvesAsync(projectId, name, curveSet)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({curves.Count} curves)");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, 1, ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadCurves");
    }
}
```

- [ ] **Step 2: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenUploadCurvesComponent.cs
git commit -m "feat(ui): replace Upload Curves boolean toggle with on-canvas button"
```

---

### Task 4: Update UploadLabelsComponent to use upload button

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs`

Note: This task ONLY removes the Go input and adds the upload button. Justification and the rename are handled in later tasks. References to `TextConverter` and `Text3DSet` remain for now.

- [ ] **Step 1: Rewrite the component**

Replace the entire contents of `src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadLabelsComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadLabelsComponent()
            : base("Upload Labels", "SvUpLbl",
                "Upload text labels from Rhino to the platform. [Upload de Rótulos 3D]")
        { }

        public override Guid ComponentGuid => new Guid("f5a6b7c8-d9e0-1234-5678-90abcdef1234");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Planes", "Pl", "Label placement planes (origin = position, orientation drives text rotation)", GH_ParamAccess.list);
            pManager.AddTextParameter("Texts", "T", "Label text strings", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the label set", GH_ParamAccess.item);
            pManager.AddColourParameter("Color", "C", "Per-label text colour (one per label, or a single colour for all)", GH_ParamAccess.list);

            Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Label Set ID", "LblID", "ID of the created label set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var planes = new List<Plane>();
            var texts = new List<string>();
            var colors = new List<Color>();

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, planes);
            DA.GetDataList(2, texts);
            DA.GetData(3, ref name);
            DA.GetDataList(4, colors);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || planes.Count == 0 || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Planes, Texts, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var textSet = TextConverter.FromPlanesAndTexts(
                    planes,
                    texts,
                    colors: colors.Count > 0 ? colors : null);
                var result = Task.Run(() => client.UploadText3DAsync(projectId, name, textSet)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({planes.Count} labels)");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, 1, ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadLabels");
    }
}
```

- [ ] **Step 2: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs
git commit -m "feat(ui): replace Upload Labels boolean toggle with on-canvas button"
```

---

### Task 5: Update UploadAnimationComponent to use upload button

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenUploadAnimationComponent.cs`

- [ ] **Step 1: Rewrite the component**

Replace the entire contents of `src/Selvagen.GH/Components/SelvagenUploadAnimationComponent.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadAnimationComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadAnimationComponent()
            : base("Upload Animation", "SvUpAnim",
                "Upload a sequence of meshes as an animation to the platform. [Upload de Animação]")
        { }

        public override Guid ComponentGuid => new Guid("E4AF5B60-C7D8-9012-3DEF-456789012345");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID", GH_ParamAccess.item);
            pManager.AddMeshParameter("Meshes", "M", "List of meshes (one per frame, in order)", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the animation", GH_ParamAccess.item);
            pManager.AddNumberParameter("FPS", "FPS", "Frames per second", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Loop", "L", "Loop playback", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Sequence ID", "SeqID", "ID of the created animation sequence", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var meshes = new List<Mesh>();
            double fps = 1.0;
            bool loop = false;

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, meshes);
            DA.GetData(2, ref name);
            DA.GetData(3, ref fps);
            DA.GetData(4, ref loop);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || meshes.Count == 0 || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Meshes, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            if (meshes.Count < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "At least 2 frames are needed for an animation.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                PluginLogger.Log($"SelvagenUploadAnimationComponent: Converting {meshes.Count} frames...");

                var result = AnimationConverter.Convert(meshes);
                if (!result.TopologyConsistent)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        "Topology varies across frames. Some frames use full geometry (larger upload).");

                PluginLogger.Log($"SelvagenUploadAnimationComponent: Uploading base mesh...");

                var baseMeshResult = Task.Run(() =>
                    client.UploadMeshAsync(projectId, $"{name} [base]", result.BaseMesh, "animation_base"))
                    .GetAwaiter().GetResult();

                PluginLogger.Log($"SelvagenUploadAnimationComponent: Base mesh ID = {baseMeshResult.Id}");

                var sequence = Task.Run(() =>
                    client.CreateAnimationSequenceAsync(
                        projectId, name, baseMeshResult.Id,
                        result.Frames.Length, fps, loop))
                    .GetAwaiter().GetResult();

                PluginLogger.Log($"SelvagenUploadAnimationComponent: Sequence ID = {sequence.Id}");

                for (int i = 0; i < result.Frames.Length; i++)
                {
                    PluginLogger.Log($"SelvagenUploadAnimationComponent: Uploading frame {i + 1}/{result.Frames.Length}...");

                    Task.Run(() =>
                        client.UploadAnimationFrameAsync(sequence.Id, i, result.Frames[i]))
                        .GetAwaiter().GetResult();
                }

                PluginLogger.Log($"SelvagenUploadAnimationComponent: Upload complete.");

                DA.SetData(0, sequence.Id);
                DA.SetData(1, $"Uploaded: {name} ({result.Frames.Length} frames, {(result.TopologyConsistent ? "position-only" : "mixed")})");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, 1, ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadAnimation");
    }
}
```

- [ ] **Step 2: Verify full build (all upload components updated)**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded. All 4 upload components now use the on-canvas button.

- [ ] **Step 3: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenUploadAnimationComponent.cs
git commit -m "feat(ui): replace Upload Animation boolean toggle with on-canvas button"
```

---

## Phase 2: Rename Text 3D Sets → Label Sets

### Task 6: Rename model file and class

**Files:**
- Rename: `src/Selvagen.Core/Models/Text3DSet.cs` → `src/Selvagen.Core/Models/LabelSet.cs`

- [ ] **Step 1: Rename the file and class**

```powershell
git mv src/Selvagen.Core/Models/Text3DSet.cs src/Selvagen.Core/Models/LabelSet.cs
```

Then edit `src/Selvagen.Core/Models/LabelSet.cs` — change the class name `Text3DSet` → `LabelSet`:

Find: `public class Text3DSet`
Replace: `public class LabelSet`

No other changes to this file. `LabelData` class stays as-is (already correctly named).

- [ ] **Step 2: Commit (build will break — fixed in next tasks)**

```powershell
git add -A
git commit -m "refactor(models): rename Text3DSet to LabelSet"
```

---

### Task 7: Rename converter file and class, add justification mapping

**Files:**
- Rename: `src/Selvagen.Core/Converters/TextConverter.cs` → `src/Selvagen.Core/Converters/LabelConverter.cs`

- [ ] **Step 1: Rename the file**

```powershell
git mv src/Selvagen.Core/Converters/TextConverter.cs src/Selvagen.Core/Converters/LabelConverter.cs
```

- [ ] **Step 2: Rewrite the converter**

Replace the entire contents of `src/Selvagen.Core/Converters/LabelConverter.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino.Geometry;
using Selvagen.Core.Models;

namespace Selvagen.Core.Converters
{
    public static class LabelConverter
    {
        private static readonly string[] AnchorXValues = { "left", "center", "right" };
        private static readonly string[] AnchorYValues = { "bottom", "middle", "top" };

        public static (string anchorX, string anchorY) JustificationToAnchors(int justification)
        {
            justification = Math.Max(0, Math.Min(8, justification));
            int col = justification % 3;
            int row = justification / 3;
            return (AnchorXValues[col], AnchorYValues[row]);
        }

        public static int AnchorsToJustification(string anchorX, string anchorY)
        {
            int col = 1;
            if (anchorX == "left") col = 0;
            else if (anchorX == "right") col = 2;

            int row = 1;
            if (anchorY == "bottom") row = 0;
            else if (anchorY == "top") row = 2;

            return row * 3 + col;
        }

        public static LabelSet ToLabelSetFromDots(IEnumerable<TextDot> dots)
        {
            if (dots == null)
                throw new ArgumentNullException(nameof(dots));

            var labels = new List<LabelData>();
            int index = 0;

            foreach (var dot in dots)
            {
                if (dot == null) continue;

                labels.Add(new LabelData
                {
                    Id = $"label-{index}",
                    Text = dot.Text ?? "",
                    Position = CoordinateHelper.ToYUp(dot.Point),
                });

                index++;
            }

            return new LabelSet { Labels = labels.ToArray() };
        }

        public static LabelSet ToLabelSetFromPoints(IList<Point3d> points, IList<string> texts)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (points.Count != texts.Count)
                throw new ArgumentException("points and texts must have the same length");

            var labels = new LabelData[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                labels[i] = new LabelData
                {
                    Id = $"label-{i}",
                    Text = texts[i] ?? "",
                    Position = CoordinateHelper.ToYUp(points[i]),
                };
            }

            return new LabelSet { Labels = labels };
        }

        public static LabelSet ToLabelSet(
            IList<Plane> planes,
            IList<string> texts,
            IList<Color> colors = null,
            IList<int> justifications = null)
        {
            if (planes == null) throw new ArgumentNullException(nameof(planes));
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (planes.Count != texts.Count)
                throw new ArgumentException("planes and texts must have the same length");

            var labels = new LabelData[planes.Count];
            for (int i = 0; i < planes.Count; i++)
            {
                var label = new LabelData
                {
                    Id = $"label-{i}",
                    Text = texts[i] ?? "",
                    Position = CoordinateHelper.ToYUp(planes[i].Origin),
                    Rotation = CoordinateHelper.PlaneToYUpEuler(planes[i]),
                };

                if (colors != null && colors.Count > 0)
                {
                    var c = colors[Math.Min(i, colors.Count - 1)];
                    label.Color = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
                }

                if (justifications != null && justifications.Count > 0)
                {
                    int j = justifications[Math.Min(i, justifications.Count - 1)];
                    var (ax, ay) = JustificationToAnchors(j);
                    label.AnchorX = ax;
                    label.AnchorY = ay;
                }

                labels[i] = label;
            }

            return new LabelSet { Labels = labels };
        }

        public static void FromLabelSet(LabelSet ls,
            out List<Plane> planes,
            out List<string> texts,
            out List<Color> colors,
            out List<double> fontSizes,
            out List<int> justifications)
        {
            if (ls == null)
                throw new ArgumentNullException(nameof(ls));

            planes = new List<Plane>();
            texts = new List<string>();
            colors = new List<Color>();
            fontSizes = new List<double>();
            justifications = new List<int>();

            foreach (var label in ls.Labels)
            {
                if (label == null) continue;

                var origin = CoordinateHelper.FromYUp(
                    label.Position[0], label.Position[1], label.Position[2]);

                Plane plane;
                if (label.Rotation != null && label.Rotation.Length == 3)
                    plane = CoordinateHelper.FromYUpEuler(label.Rotation, origin);
                else
                    plane = new Plane(origin, Vector3d.XAxis, Vector3d.YAxis);

                planes.Add(plane);
                texts.Add(label.Text ?? "");

                if (!string.IsNullOrEmpty(label.Color) && label.Color.StartsWith("#"))
                    colors.Add(ColorTranslator.FromHtml(label.Color));
                else
                    colors.Add(Color.Black);

                fontSizes.Add(label.FontSize ?? 0.0);
                justifications.Add(AnchorsToJustification(label.AnchorX, label.AnchorY));
            }
        }
    }
}
```

- [ ] **Step 3: Commit**

```powershell
git add -A
git commit -m "refactor(converters): rename TextConverter to LabelConverter, add justification mapping"
```

---

### Task 8: Update ApiResponses, SelvagenClient, and all remaining references

**Files:**
- Modify: `src/Selvagen.Core/Models/ApiResponses.cs`
- Modify: `src/Selvagen.Core/Api/SelvagenClient.cs`
- Modify: `src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs`
- Modify: `src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs`
- Modify: `src/Selvagen.GH/Components/SelvagenListAssetsComponent.cs`
- Modify: `src/Selvagen.GH/Components/SelvagenDeleteAssetComponent.cs`

This task fixes ALL remaining references to the old names so the build passes again.

- [ ] **Step 1: Update ApiResponses.cs**

In `src/Selvagen.Core/Models/ApiResponses.cs`:

Find: `public class Text3DSetAssetFull`
Replace: `public class LabelSetAssetFull`

Find: `public Text3DSet TextData`
Replace: `public LabelSet TextData`

- [ ] **Step 2: Update SelvagenClient.cs**

In `src/Selvagen.Core/Api/SelvagenClient.cs`, apply these replacements:

Find: `public async Task<UploadResult> UploadText3DAsync(string projectId, string name, Text3DSet textSet)`
Replace: `public async Task<UploadResult> UploadLabelSetAsync(string projectId, string name, LabelSet labelSet)`

Inside that method, find: `text_data = textSet`
Replace: `text_data = labelSet`

Find: `public async Task<AssetInfo[]> ListText3DSetsAsync(string projectId)`
Replace: `public async Task<AssetInfo[]> ListLabelSetsAsync(string projectId)`

Inside that method, find: `/rest/v1/text_3d_sets?project_id=eq.{projectId}`
Replace: `/rest/v1/label_sets?project_id=eq.{projectId}`

Find: `"text_3d_sets"` in the label argument of `QueryAssetsAsync`
Replace: `"label_sets"`

Find: `public async Task<Text3DSetAssetFull> GetText3DSetAsync(string id)`
Replace: `public async Task<LabelSetAssetFull> GetLabelSetAsync(string id)`

Inside that method, find: `/rest/v1/text_3d_sets?id=eq.{id}`
Replace: `/rest/v1/label_sets?id=eq.{id}`

Find: `var results = JsonSerializer.Deserialize<Text3DSetAssetFull[]>(json);`
Replace: `var results = JsonSerializer.Deserialize<LabelSetAssetFull[]>(json);`

Find all: `Text 3D set` in error/log messages
Replace all: `Label set`

Find: `plugin-upload-text3d`
Replace: `plugin-upload-labels`

- [ ] **Step 3: Update SelvagenUploadLabelsComponent.cs**

In `src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs`:

Find: `var textSet = TextConverter.FromPlanesAndTexts(`
Replace: `var labelSet = LabelConverter.ToLabelSet(`

Find: `client.UploadText3DAsync(projectId, name, textSet)`
Replace: `client.UploadLabelSetAsync(projectId, name, labelSet)`

- [ ] **Step 4: Update SelvagenDownloadLabelsComponent.cs**

In `src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs`:

Find: `client.GetText3DSetAsync(assetId)`
Replace: `client.GetLabelSetAsync(assetId)`

Find: `asset.TextData == null`  (leave as-is — the property name `TextData` is correct, it maps to the JSON `text_data` column)

Find: `TextConverter.FromText3DSet(asset.TextData,`
Replace: `LabelConverter.FromLabelSet(asset.TextData,`

Find: `out var planes, out var texts, out var colors, out var fontSizes);`
Replace: `out var planes, out var texts, out var colors, out var fontSizes, out var justifications);`

Find: `"Label set has no inline text data."`  (if still says "text data", leave it — it IS text data in the JSONB column)

- [ ] **Step 5: Update SelvagenListAssetsComponent.cs**

In `src/Selvagen.GH/Components/SelvagenListAssetsComponent.cs`:

Find: `"text_3d_sets"` in `_filterOptions` array
Replace: `"label_sets"`

Find: `"Text 3D Sets"` in `_filterDisplayNames` array
Replace: `"Label Sets"`

In the `FetchAsync` switch statement, find: `case "text_3d_sets":`
Replace: `case "label_sets":`

Find: `return await client.ListText3DSetsAsync(projectId)`
Replace: `return await client.ListLabelSetsAsync(projectId)`

- [ ] **Step 6: Update SelvagenDeleteAssetComponent.cs**

In `src/Selvagen.GH/Components/SelvagenDeleteAssetComponent.cs`:

Find: `"text_3d_sets"` in `ValidTables` array
Replace: `"label_sets"`

- [ ] **Step 7: Verify full build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded. All references updated.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "refactor: rename all Text3DSet references to LabelSet across plugin"
```

---

## Phase 3: Justification Input/Output

### Task 9: Add Justification to Upload Labels and Download Labels

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs`
- Modify: `src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs`

- [ ] **Step 1: Update Upload Labels — add Justification input**

In `src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs`:

After the Color input line `pManager.AddColourParameter("Color", ...);`, add:

```csharp
pManager.AddIntegerParameter("Justification", "J", "Per-label justification (0=BotLeft, 1=BotCenter, 2=BotRight, 3=MidLeft, 4=MidCenter, 5=MidRight, 6=TopLeft, 7=TopCenter, 8=TopRight)", GH_ParamAccess.list);
```

Update `Params.Input[4].Optional = true;` to also make the new input optional:

```csharp
Params.Input[4].Optional = true;
Params.Input[5].Optional = true;
```

In `SolveInstance`, add after the colors list declaration:

```csharp
var justifications = new List<int>();
```

After `DA.GetDataList(4, colors);`, add:

```csharp
DA.GetDataList(5, justifications);
```

Update the converter call to pass justifications:

```csharp
var labelSet = LabelConverter.ToLabelSet(
    planes,
    texts,
    colors: colors.Count > 0 ? colors : null,
    justifications: justifications.Count > 0 ? justifications : null);
```

- [ ] **Step 2: Update Download Labels — add Justification output**

In `src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs`, in `RegisterOutputParams`, after Font Sizes and before Name, add:

```csharp
pManager.AddIntegerParameter("Justification", "J", "Per-label justification (0-8)", GH_ParamAccess.list);
```

This shifts Name from index 4→5, Status from index 5→6. Update ALL `DA.SetData`/`DA.SetDataList` index references:

- Planes: index 0 (unchanged)
- Texts: index 1 (unchanged)
- Colors: index 2 (unchanged)
- FontSizes: index 3 (unchanged)
- **Justification: index 4 (NEW)**
- Name: index 5 (was 4)
- Status: index 6 (was 5)

In the cached return path, add:

```csharp
DA.SetDataList(4, _cachedJustifications);
```

Add `private List<int> _cachedJustifications;` to the cache fields.

In the fresh-fetch path, after the `FromLabelSet` call (which now outputs `justifications`), add:

```csharp
_cachedJustifications = justifications;
```

And:

```csharp
DA.SetDataList(4, justifications);
```

Update all Status `DA.SetData` calls from index `5` to `6`, and Name from `4` to `5`.

- [ ] **Step 3: Verify build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenUploadLabelsComponent.cs src/Selvagen.GH/Components/SelvagenDownloadLabelsComponent.cs
git commit -m "feat(labels): add justification input/output to Upload/Download Labels"
```

---

## Phase 4: Database Migration and Handoff

### Task 10: Apply Supabase migration

**Files:**
- No local files — Supabase MCP or dashboard

- [ ] **Step 1: Run the migration on GEN.BOARD (project ID: aqzfsrebvjkegvfexcut)**

Use Supabase MCP `apply_migration` or `execute_sql`:

```sql
-- Rename table
ALTER TABLE text_3d_sets RENAME TO label_sets;

-- Rename FK columns in referencing tables
ALTER TABLE topography RENAME COLUMN contours_text_3d_set_id TO contours_label_set_id;
ALTER TABLE analyses RENAME COLUMN access_text_3d_set_id TO access_label_set_id;
ALTER TABLE analyses RENAME COLUMN rock_text_3d_set_height_id TO rock_label_set_height_id;
ALTER TABLE analyses RENAME COLUMN rock_text_3d_set_vol_id TO rock_label_set_vol_id;
ALTER TABLE optimizations RENAME COLUMN access_text_3d_set_id TO access_label_set_id;

-- Force PostgREST to pick up the new names immediately
NOTIFY pgrst, 'reload schema';
```

- [ ] **Step 2: Verify the migration**

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'label_sets';
```

Expected: 1 row returned.

```sql
SELECT column_name FROM information_schema.columns WHERE table_name = 'topography' AND column_name = 'contours_label_set_id';
```

Expected: 1 row returned.

- [ ] **Step 3: Commit a migration record locally**

Save the migration SQL to `docs/migrations/2026-05-26-rename-text3d-to-label-sets.sql` for reference, then commit.

---

### Task 11: Build, deploy, and verify

- [ ] **Step 1: Full release build**

```powershell
dotnet build src\Selvagen.GH\Selvagen.GH.csproj --configuration Release
```

- [ ] **Step 2: Deploy to Grasshopper Libraries**

Close Rhino first, then:

```powershell
$src = "src\Selvagen.GH\bin\Release\net8.0"
$dest = "$env:APPDATA\Grasshopper\Libraries\Selvagen"
Copy-Item "$src\*" $dest -Recurse -Force -Confirm:$false
```

- [ ] **Step 3: Verify in Grasshopper**

Open Rhino + Grasshopper and verify:

1. **Upload button**: All 4 upload components show a teal "Upload" button at the bottom. No "Go" input pin.
2. **Upload Mesh**: Wire project ID, mesh, name → click Upload → verify mesh uploads. Button shows "Uploading..." during upload, returns to "Upload" after.
3. **List Assets**: The dropdown shows "Label Sets" instead of "Text 3D Sets". Selecting it lists label sets correctly.
4. **Upload Labels**: Has new Justification input. Upload labels with justification values. Verify they appear with correct alignment on the platform.
5. **Download Labels**: Has new Justification output. Download labels and verify the justification integers match what was uploaded.
6. **Delete Asset**: Can delete from "label_sets" table.

---

### Task 12: Write platform handoff document

**Files:**
- Create: `docs/handoff-label-sets-rename.md`

- [ ] **Step 1: Write the handoff document**

Create `docs/handoff-label-sets-rename.md`:

```markdown
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
      "anchorY": "top",
      ...
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
```

- [ ] **Step 2: Commit**

```powershell
git add docs/handoff-label-sets-rename.md
git commit -m "docs: platform handoff for label sets rename and justification"
```
