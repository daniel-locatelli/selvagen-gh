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
                "Upload a sequence of meshes as an animation to the platform.")
        { }

        public override Guid ComponentGuid => new Guid("E4AF5B60-C7D8-9012-3DEF-456789012345");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Target project ID", GH_ParamAccess.item);
            pManager.AddMeshParameter("Meshes", "M", "List of meshes (one per frame, in order)", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the animation", GH_ParamAccess.item);
            pManager.AddNumberParameter("FPS", "FPS", "Frames per second", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Loop", "L", "Loop playback", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("SequenceID", "ID", "ID of the created animation sequence", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var meshes = new List<Mesh>();
            double fps = 1.0;
            bool loop = false, upload = false;

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, meshes);
            DA.GetData(2, ref name);
            DA.GetData(3, ref fps);
            DA.GetData(4, ref loop);
            DA.GetData(5, ref upload);

            var client = SessionManager.Current;

            if (!upload || client == null || meshes.Count == 0)
            {
                if (client == null && upload)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetWaiting(DA);
                return;
            }

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "ProjectID and Name are required.");
                SetWaiting(DA);
                return;
            }

            if (meshes.Count < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "At least 2 frames are needed for an animation.");
                SetWaiting(DA);
                return;
            }

            try
            {
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
                PluginLogger.Log($"SelvagenUploadAnimationComponent: Creating animation sequence...");

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
                SetUploadError(DA, ex);
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadAnimation");
    }
}
