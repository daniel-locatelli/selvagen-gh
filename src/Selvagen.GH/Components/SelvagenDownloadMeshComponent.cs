using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadMeshComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private Rhino.Geometry.Mesh _cachedMesh;
        private string _cachedName;

        private volatile bool _isFetching;
        private Selvagen.Core.Models.MeshAssetFull _pendingAsset;
        private string _pendingId;
        private string _fetchError;
        private readonly object _lock = new object();

        public SelvagenDownloadMeshComponent()
            : base("Download Mesh", "SvDnMesh",
                "Download a mesh from the platform as Rhino geometry. [Download de Malha]")
        { }

        public override Guid ComponentGuid => new Guid("42ab0466-4987-49dc-a4b4-f8d831f8398e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Mesh asset ID (from List Assets) [ID do Asset de Malha]", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Downloaded Rhino mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Asset name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(assetId)) { DA.SetData(2, "Provide an Asset ID."); return; }
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(2, "Not logged in.");
                return;
            }

            // 1. A finished fetch waiting? Build geometry on the solver thread, cache, emit.
            Selvagen.Core.Models.MeshAssetFull pending; string pendingId; string err;
            lock (_lock) { pending = _pendingAsset; pendingId = _pendingId; _pendingAsset = null; err = _fetchError; _fetchError = null; }
            if (err != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                DA.SetData(2, $"Error: {err}");
                return;
            }
            if (pending != null && pendingId == assetId)
            {
                if (pending.GeometryData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh has no inline geometry data.");
                    DA.SetData(2, "No geometry data.");
                    return;
                }
                var mesh = MeshConverter.FromBufferGeometry(pending.GeometryData);
                _cachedId = assetId; _cachedMesh = mesh; _cachedName = pending.Name;
                DA.SetData(0, mesh);
                DA.SetData(1, pending.Name);
                DA.SetData(2, $"Downloaded: {pending.Name}");
                return;
            }

            // 2. Cached for this id?
            if (assetId == _cachedId && _cachedMesh != null)
            {
                DA.SetData(0, _cachedMesh);
                DA.SetData(1, _cachedName);
                DA.SetData(2, $"Cached: {_cachedName}");
                return;
            }

            // 3. In-flight already?
            if (_isFetching) { DA.SetData(2, "Downloading..."); return; }

            // 4. Start the fetch (network only; geometry is built on the re-solve above).
            _isFetching = true;
            var capturedId = assetId;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var a = await client.GetMeshAsync(capturedId).ConfigureAwait(false);
                    lock (_lock) { _pendingAsset = a; _pendingId = capturedId; }
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
            DA.SetData(2, "Downloading...");
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadMesh");
    }
}
