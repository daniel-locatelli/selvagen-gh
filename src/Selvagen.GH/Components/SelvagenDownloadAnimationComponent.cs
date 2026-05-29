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

        private volatile bool _isFetching;
        private (AnimationSequenceFull Info, MeshAssetFull BaseAsset, AnimationFrameFull[] Frames)? _pendingAnim;
        private string _pendingId;
        private string _fetchError;
        private readonly object _lock = new object();

        public SelvagenDownloadAnimationComponent()
            : base("Download Animation", "SvDnAnim",
                "Download an animation sequence from the platform as a list of meshes. [Download de Animação]")
        { }

        public override Guid ComponentGuid => new Guid("0de3c718-5331-423e-9d5f-e28540c9c84f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Sequence ID", "SeqID", "Animation sequence ID (from List Assets) [ID da Sequência de Animação]", GH_ParamAccess.item);
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

            if (string.IsNullOrEmpty(seqId)) { DA.SetData(5, "Provide a Sequence ID."); return; }
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(5, "Not logged in.");
                return;
            }

            // 1. A finished fetch waiting? Build geometry on the solver thread, cache, emit.
            (AnimationSequenceFull Info, MeshAssetFull BaseAsset, AnimationFrameFull[] Frames)? pending;
            string pendingId; string err;
            lock (_lock) { pending = _pendingAnim; pendingId = _pendingId; _pendingAnim = null; err = _fetchError; _fetchError = null; }
            if (err != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                DA.SetData(5, $"Error: {err}");
                return;
            }
            if (pending != null && pendingId == seqId)
            {
                var sequence = pending.Value.Info;
                var baseMeshAsset = pending.Value.BaseAsset;
                var frames = pending.Value.Frames;

                Mesh baseMesh = null;
                if (baseMeshAsset.GeometryData != null)
                    baseMesh = MeshConverter.FromBufferGeometry(baseMeshAsset.GeometryData);

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
                return;
            }

            // 2. Cached for this id?
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

            // 3. In-flight already?
            if (_isFetching) { DA.SetData(5, "Downloading..."); return; }

            // 4. Start the fetch (network only; geometry is built on the re-solve above).
            _isFetching = true;
            var capturedId = seqId;
            Task.Run(async () =>
            {
                try
                {
                    var info = await client.GetAnimationSequenceInfoAsync(capturedId).ConfigureAwait(false);
                    var baseAsset = await client.GetMeshAsync(info.BaseAssetId).ConfigureAwait(false);
                    var frames = await client.GetAnimationFramesAsync(capturedId).ConfigureAwait(false);
                    lock (_lock) { _pendingAnim = (info, baseAsset, frames); _pendingId = capturedId; }
                }
                catch (Exception ex)
                {
                    lock (_lock) { _fetchError = ex.Unwrap().Message; }
                }
                finally
                {
                    _isFetching = false;
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        if (OnPingDocument() != null) ExpireSolution(true);
                    }));
                }
            });
            DA.SetData(5, "Downloading...");
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadAnimation");
    }
}
