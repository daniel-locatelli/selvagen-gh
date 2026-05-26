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

        public SelvagenDownloadMeshComponent()
            : base("Download Mesh", "SvDnMesh",
                "Download a mesh from the platform as Rhino geometry. [Download de Malha]")
        { }

        public override Guid ComponentGuid => new Guid("42ab0466-4987-49dc-a4b4-f8d831f8398e");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Mesh asset ID (from List Assets)", GH_ParamAccess.item);
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

            if (string.IsNullOrEmpty(assetId))
            {
                DA.SetData(2, "Provide an Asset ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(2, "Not logged in.");
                return;
            }

            if (assetId == _cachedId && _cachedMesh != null)
            {
                DA.SetData(0, _cachedMesh);
                DA.SetData(1, _cachedName);
                DA.SetData(2, $"Cached: {_cachedName}");
                return;
            }

            try
            {
                var asset = Task.Run(() => client.GetMeshAsync(assetId)).GetAwaiter().GetResult();

                if (asset.GeometryData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh has no inline geometry data.");
                    DA.SetData(2, "No geometry data.");
                    return;
                }

                var mesh = MeshConverter.FromBufferGeometry(asset.GeometryData);

                _cachedId = assetId;
                _cachedMesh = mesh;
                _cachedName = asset.Name;

                DA.SetData(0, mesh);
                DA.SetData(1, asset.Name);
                DA.SetData(2, $"Downloaded: {asset.Name}");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(2, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadMesh");
    }
}
