using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

namespace Selvagen.GH.Components
{
    public class SelvagenDeleteAssetComponent : GH_Component
    {
        private static readonly string[] ValidTables = { "meshes", "curve_sets", "text_3d_sets" };

        public SelvagenDeleteAssetComponent()
            : base("Delete Asset", "SvDelete",
                "Delete a mesh, curve set, or text 3D set by ID.",
                "Selvagen", "Data")
        { }

        public override Guid ComponentGuid => new Guid("C39D4E5F-A6B7-8901-2CDE-F34567890123");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("AssetTable", "T", "Table name: meshes, curve_sets, or text_3d_sets", GH_ParamAccess.item);
            pManager.AddTextParameter("AssetID", "ID", "ID of the asset to delete", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Delete", "Go", "Set to true to delete", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "OK", "Whether deletion succeeded", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Operation status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string tableName = "";
            string assetId = "";
            bool doDelete = false;

            DA.GetData(0, ref tableName);
            DA.GetData(1, ref assetId);
            DA.GetData(2, ref doDelete);

            var client = SessionManager.Current;

            if (!doDelete || client == null)
            {
                if (client == null && doDelete)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(0, false);
                DA.SetData(1, "Waiting...");
                return;
            }

            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(assetId))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "AssetTable and AssetID are required.");
                DA.SetData(0, false);
                DA.SetData(1, "Missing inputs");
                return;
            }

            var tableNorm = tableName.Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidTables, tableNorm) < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Invalid table: {tableName}. Use: meshes, curve_sets, or text_3d_sets.");
                DA.SetData(0, false);
                DA.SetData(1, "Invalid table name");
                return;
            }

            try
            {
                PluginLogger.Log($"SelvagenDeleteAssetComponent: Deleting {tableNorm}/{assetId}...");
                Task.Run(() => client.DeleteAssetAsync(tableNorm, assetId)).GetAwaiter().GetResult();

                PluginLogger.Log($"SelvagenDeleteAssetComponent: Deleted successfully.");
                DA.SetData(0, true);
                DA.SetData(1, $"Deleted: {assetId}");
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException?.Message ?? ex.Message;
                PluginLogger.Log($"SelvagenDeleteAssetComponent Error: {errorMsg}");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMsg);
                DA.SetData(0, false);
                DA.SetData(1, $"Error: {errorMsg}");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Delete");
    }
}
