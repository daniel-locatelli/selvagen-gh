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
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddMeshParameter("Meshes", "M", "List of meshes (one per frame, in order) [Malhas por Quadro]", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the animation [Nome de Exibição]", GH_ParamAccess.item);
            pManager.AddNumberParameter("FPS", "FPS", "Frames per second [Quadros por Segundo]", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Loop", "L", "Loop playback [Reprodução em Loop]", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Sequence ID", "SeqID", "ID of the created animation sequence", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Emit a finished async result, if one is waiting.
            if (TryFinishAsync<(string SeqId, string Name, int FrameCount, bool TopologyConsistent)>(DA, 1, (da, r) =>
                {
                    da.SetData(0, r.SeqId);
                    da.SetData(1, $"Uploaded: {r.Name} ({r.FrameCount} frames, {(r.TopologyConsistent ? "position-only" : "mixed")})");
                }))
                return;

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
                if (IsRunningAsync) { DA.SetData(1, "Uploading..."); return; }
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

            // Convert Rhino geometry on the solver thread; only the HTTP calls go async.
            PluginLogger.Log($"SelvagenUploadAnimationComponent: Converting {meshes.Count} frames...");

            var conv = AnimationConverter.Convert(meshes);
            if (!conv.TopologyConsistent)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    "Topology varies across frames. Some frames use full geometry (larger upload).");

            StartAsync(async () =>
            {
                PluginLogger.Log($"SelvagenUploadAnimationComponent: Uploading base mesh...");
                var baseMeshResult = await client.UploadMeshAsync(projectId, $"{name} [base]", conv.BaseMesh, "animation_base").ConfigureAwait(false);
                PluginLogger.Log($"SelvagenUploadAnimationComponent: Base mesh ID = {baseMeshResult.Id}");

                var sequence = await client.CreateAnimationSequenceAsync(projectId, name, baseMeshResult.Id, conv.Frames.Length, fps, loop).ConfigureAwait(false);
                PluginLogger.Log($"SelvagenUploadAnimationComponent: Sequence ID = {sequence.Id}");

                for (int i = 0; i < conv.Frames.Length; i++)
                {
                    PluginLogger.Log($"SelvagenUploadAnimationComponent: Uploading frame {i + 1}/{conv.Frames.Length}...");
                    await client.UploadAnimationFrameAsync(sequence.Id, i, conv.Frames[i]).ConfigureAwait(false);
                }

                PluginLogger.Log($"SelvagenUploadAnimationComponent: Upload complete.");
                return (sequence.Id, name, conv.Frames.Length, conv.TopologyConsistent);
            });
            DA.SetData(1, "Uploading...");
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadAnimation");
    }
}
