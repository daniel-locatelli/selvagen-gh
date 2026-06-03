using System;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDeleteAssetComponent : SelvagenActionComponentBase
    {
        public SelvagenDeleteAssetComponent()
            : base("Delete Asset", "SvDelete",
                "Delete an asset (mesh, curve set, label set, animation, or color legend) by ID. The component finds which table it belongs to. [Excluir Asset]",
                "08 Assets")
        { }

        public override Guid ComponentGuid => new Guid("C39D4E5F-A6B7-8901-2CDE-F34567890123");

        // ── ISelvagenActionButton — destructive red button ─────────────────
        public override string ActionLabel        => "Delete";
        public override string ActionLabelRunning => "Deleting...";
        public override Color  ButtonGradientTop    => Color.FromArgb(200, 60, 60);
        public override Color  ButtonGradientBottom => Color.FromArgb(130, 30, 30);

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "ID of the asset to delete [ID do Asset]", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "OK", "Whether deletion succeeded", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Operation status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (!ActionRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(0, false);
                SetReady(DA, 1);
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(0, false);
                DA.SetData(1, "Not logged in");
                return;
            }

            if (string.IsNullOrEmpty(assetId))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Asset ID is required.");
                DA.SetData(0, false);
                DA.SetData(1, "Missing Asset ID");
                return;
            }

            try
            {
                IsRunning = true;
                ForceCanvasRefresh();

                PluginLogger.Log($"SelvagenDeleteAssetComponent: Deleting {assetId}...");
                var result = Task.Run(() => client.DeleteAssetByIdAsync(assetId)).GetAwaiter().GetResult();

                switch (result?.Status)
                {
                    case "deleted":
                        PluginLogger.Log($"SelvagenDeleteAssetComponent: Deleted {assetId} from {result.Table}.");
                        DA.SetData(0, true);
                        DA.SetData(1, $"Deleted {assetId} from {result.Table}");
                        break;
                    case "forbidden":
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "You don't have permission to delete this asset (editor role required).");
                        DA.SetData(0, false);
                        DA.SetData(1, "You don't have permission to delete this asset (editor role required).");
                        break;
                    case "not_found":
                    default:
                        DA.SetData(0, false);
                        DA.SetData(1, "Asset not found (check the ID, or it may already be deleted).");
                        break;
                }
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                PluginLogger.Log($"SelvagenDeleteAssetComponent Error: {msg}");
                DA.SetData(0, false);
                SetActionError(DA, 1, ex);
            }
            finally
            {
                IsRunning = false;
            }
        }

        protected override Bitmap Icon => IconLoader.Load("Delete");
    }
}
