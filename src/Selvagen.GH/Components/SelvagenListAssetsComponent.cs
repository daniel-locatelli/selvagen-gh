using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenListAssetsComponent : SelvagenSelectableComponentBase<AssetInfo>
    {
        public SelvagenListAssetsComponent()
            : base("List Assets", "SvAssets",
                "List meshes, curve sets, or text 3D sets for a project. Pick one from the inline dropdown.",
                "08 Assets")
        { }

        public override Guid ComponentGuid => new Guid("A17B2C3D-E4F5-6789-0ABC-DEF123456789");

        protected override void RegisterFilterInputs(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project ID to list assets for", GH_ParamAccess.item, "");
            pManager[0].Optional = true;
            pManager.AddTextParameter("AssetType", "T", "Asset type: meshes, curve_sets, or text_3d_sets", GH_ParamAccess.item, "meshes");
        }

        protected override object[] CaptureInputs(IGH_DataAccess da)
        {
            string projectId = "";
            string assetType = "meshes";
            da.GetData(0, ref projectId);
            da.GetData(1, ref assetType);
            return new object[] { projectId ?? "", assetType ?? "meshes" };
        }

        protected override async Task<AssetInfo[]> FetchAsync(SelvagenClient client, object[] inputs)
        {
            string projectId = (string)inputs[0];
            string assetType = (string)inputs[1];

            if (string.IsNullOrEmpty(projectId)) return new AssetInfo[0];

            switch (assetType.ToLowerInvariant())
            {
                case "meshes":
                case "mesh":
                    return await client.ListMeshesAsync(projectId).ConfigureAwait(false);
                case "curve_sets":
                case "curves":
                    return await client.ListCurveSetsAsync(projectId).ConfigureAwait(false);
                case "text_3d_sets":
                case "labels":
                case "text":
                    return await client.ListText3DSetsAsync(projectId).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown asset type: {assetType}. Use meshes, curve_sets, or text_3d_sets.");
            }
        }

        protected override string GetId(AssetInfo item) => item.Id;
        protected override string GetDisplayName(AssetInfo item) => item.Name;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("ListAssets");
    }
}
