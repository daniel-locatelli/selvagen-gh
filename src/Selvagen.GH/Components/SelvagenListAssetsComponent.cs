using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenListAssetsComponent : GH_Component
    {
        private List<AssetInfo> _cachedAssets;
        private string _lastProjectId;
        private string _lastAssetType;
        private bool _refreshWasTrue;

        public SelvagenListAssetsComponent()
            : base("List Assets", "SvAssets",
                "List meshes, curve sets, or text 3D sets for a project.",
                "Selvagen", "Data")
        { }

        public override Guid ComponentGuid => new Guid("A17B2C3D-E4F5-6789-0ABC-DEF123456789");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project ID to list assets for", GH_ParamAccess.item);
            pManager.AddTextParameter("AssetType", "T", "Asset type: meshes, curve_sets, or text_3d_sets", GH_ParamAccess.item, "meshes");
            pManager.AddBooleanParameter("Refresh", "R", "Force a re-fetch", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("IDs", "ID", "Asset IDs", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Asset names", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Asset types (meshes only)", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "";
            string assetType = "meshes";
            bool refresh = false;

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref assetType);
            DA.GetData(2, ref refresh);

            var client = SessionManager.Current;
            if (client == null || string.IsNullOrEmpty(projectId))
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetEmpty(DA);
                return;
            }

            bool needsFetch = _cachedAssets == null
                || projectId != _lastProjectId
                || assetType != _lastAssetType
                || (refresh && !_refreshWasTrue);
            _refreshWasTrue = refresh;
            _lastProjectId = projectId;
            _lastAssetType = assetType;

            if (needsFetch)
            {
                try
                {
                    PluginLogger.Log($"SelvagenListAssetsComponent: Fetching {assetType} for project {projectId}...");
                    var assets = Task.Run(() => FetchAssets(client, projectId, assetType)).GetAwaiter().GetResult();
                    _cachedAssets = assets.ToList();
                    PluginLogger.Log($"SelvagenListAssetsComponent: Found {_cachedAssets.Count} assets.");
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.InnerException?.Message ?? ex.Message;
                    PluginLogger.Log($"SelvagenListAssetsComponent Error: {errorMsg}");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMsg);
                    SetEmpty(DA);
                    return;
                }
            }

            DA.SetDataList(0, _cachedAssets.Select(a => a.Id).ToList());
            DA.SetDataList(1, _cachedAssets.Select(a => a.Name).ToList());
            DA.SetDataList(2, _cachedAssets.Select(a => a.Type).ToList());
        }

        private async Task<AssetInfo[]> FetchAssets(SelvagenClient client, string projectId, string assetType)
        {
            switch (assetType.ToLowerInvariant())
            {
                case "meshes":
                case "mesh":
                    return await client.ListMeshesAsync(projectId);
                case "curve_sets":
                case "curves":
                    return await client.ListCurveSetsAsync(projectId);
                case "text_3d_sets":
                case "labels":
                case "text":
                    return await client.ListText3DSetsAsync(projectId);
                default:
                    throw new ArgumentException($"Unknown asset type: {assetType}. Use meshes, curve_sets, or text_3d_sets.");
            }
        }

        private void SetEmpty(IGH_DataAccess DA)
        {
            DA.SetDataList(0, new List<string>());
            DA.SetDataList(1, new List<string>());
            DA.SetDataList(2, new List<string>());
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("ListAssets");
    }
}
