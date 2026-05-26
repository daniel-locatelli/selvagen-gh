using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadAnimationComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private List<Mesh> _cachedMeshes;
        private List<string> _cachedLabels;
        private double _cachedFps;
        private bool _cachedLoop;
        private string _cachedName;

        public SelvagenDownloadAnimationComponent()
            : base("Download Animation", "SvDnAnim",
                "Download an animation sequence from the platform as a list of meshes. [Download de Animação]")
        { }

        public override Guid ComponentGuid => new Guid("0de3c718-5331-423e-9d5f-e28540c9c84f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Sequence ID", "SeqID", "Animation sequence ID (from List Assets)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Meshes", "M", "Per-frame meshes", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Lbl", "Per-frame labels", GH_ParamAccess.list);
            pManager.AddNumberParameter("FPS", "F", "Frames per second", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Loop", "L", "Loop playback", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Sequence name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string seqId = "";
            DA.GetData(0, ref seqId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(seqId))
            {
                DA.SetData(5, "Provide a Sequence ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(5, "Not logged in.");
                return;
            }

            if (seqId == _cachedId && _cachedMeshes != null)
            {
                DA.SetDataList(0, _cachedMeshes);
                DA.SetDataList(1, _cachedLabels);
                DA.SetData(2, _cachedFps);
                DA.SetData(3, _cachedLoop);
                DA.SetData(4, _cachedName);
                DA.SetData(5, $"Cached: {_cachedName} ({_cachedMeshes.Count} frames)");
                return;
            }

            try
            {
                var sequence = Task.Run(() => client.GetAnimationSequenceInfoAsync(seqId))
                    .GetAwaiter().GetResult();

                var baseMeshAsset = Task.Run(() => client.GetMeshAsync(sequence.BaseAssetId))
                    .GetAwaiter().GetResult();

                Mesh baseMesh = null;
                if (baseMeshAsset.GeometryData != null)
                    baseMesh = MeshConverter.FromBufferGeometry(baseMeshAsset.GeometryData);

                var frames = Task.Run(() => client.GetAnimationFramesAsync(seqId))
                    .GetAwaiter().GetResult();

                var meshes = new List<Mesh>();
                var labels = new List<string>();

                foreach (var frame in frames)
                {
                    if (frame.GeometryData == null)
                    {
                        meshes.Add(null);
                        labels.Add(frame.Label ?? "");
                        continue;
                    }

                    Mesh frameMesh;
                    if (frame.GeometryData.Format == "buffer_geometry" && frame.GeometryData.Geometry != null)
                    {
                        frameMesh = MeshConverter.FromBufferGeometry(frame.GeometryData.Geometry);
                    }
                    else if (frame.GeometryData.Positions != null && baseMesh != null)
                    {
                        frameMesh = baseMesh.DuplicateMesh();
                        var positions = frame.GeometryData.Positions;
                        int vertCount = Math.Min(frameMesh.Vertices.Count, positions.Length / 3);
                        for (int v = 0; v < vertCount; v++)
                        {
                            var pt = CoordinateHelper.FromYUp(positions, v * 3);
                            frameMesh.Vertices.SetVertex(v, pt);
                        }
                        frameMesh.Normals.ComputeNormals();
                    }
                    else
                    {
                        frameMesh = null;
                    }

                    meshes.Add(frameMesh);
                    labels.Add(frame.Label ?? "");
                }

                _cachedId = seqId;
                _cachedMeshes = meshes;
                _cachedLabels = labels;
                _cachedFps = sequence.Fps ?? 1.0;
                _cachedLoop = sequence.Loop ?? false;
                _cachedName = sequence.Name;

                DA.SetDataList(0, meshes);
                DA.SetDataList(1, labels);
                DA.SetData(2, _cachedFps);
                DA.SetData(3, _cachedLoop);
                DA.SetData(4, sequence.Name);
                DA.SetData(5, $"Downloaded: {sequence.Name} ({meshes.Count} frames)");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(5, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadAnimation");
    }
}
