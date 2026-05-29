using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

namespace Selvagen.GH.Components
{
    public class SelvagenDeleteAssetComponent : GH_Component
    {
        private static readonly string[] ValidTables = { "meshes", "curve_sets", "label_sets", "animation_sequences" };

        private volatile bool _isDeleting;
        private bool _lastDelete;
        private bool? _pendingSuccess;
        private string _pendingMsg;
        private readonly object _lock = new object();

        public SelvagenDeleteAssetComponent()
            : base("Delete Asset", "SvDelete",
                "Delete a mesh, curve set, or label set by ID. [Excluir Asset]",
                "Selvagen", "08 Assets")
        { }

        public override Guid ComponentGuid => new Guid("C39D4E5F-A6B7-8901-2CDE-F34567890123");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset Table", "Table", "Table name: meshes, curve_sets, label_sets, or animation_sequences [Tabela do Asset]", GH_ParamAccess.item);
            pManager.AddTextParameter("Asset ID", "AstID", "ID of the asset to delete [ID do Asset]", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Delete", "Go", "Set to true to delete [Excluir]", GH_ParamAccess.item, false);
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

            // 1. Finished delete waiting?
            bool? success; string msg;
            lock (_lock) { success = _pendingSuccess; _pendingSuccess = null; msg = _pendingMsg; _pendingMsg = null; }
            if (success.HasValue)
            {
                if (!success.Value) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(0, success.Value);
                DA.SetData(1, msg);
                _lastDelete = doDelete; // consume this true-state so it won't re-fire
                return;
            }

            bool rising = doDelete && !_lastDelete;
            _lastDelete = doDelete;

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
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Asset Table and Asset ID are required.");
                DA.SetData(0, false);
                DA.SetData(1, "Missing inputs");
                return;
            }

            var tableNorm = tableName.Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidTables, tableNorm) < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Invalid table: {tableName}. Use: meshes, curve_sets, label_sets, or animation_sequences.");
                DA.SetData(0, false);
                DA.SetData(1, "Invalid table name");
                return;
            }

            if (_isDeleting) { DA.SetData(0, false); DA.SetData(1, "Deleting..."); return; }
            if (!rising)
            {
                DA.SetData(0, false);
                DA.SetData(1, "Toggle Delete off then on to delete again.");
                return;
            }

            _isDeleting = true;
            var t = tableNorm; var id = assetId;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    PluginLogger.Log($"SelvagenDeleteAssetComponent: Deleting {t}/{id}...");
                    await client.DeleteAssetAsync(t, id).ConfigureAwait(false);
                    lock (_lock) { _pendingSuccess = true; _pendingMsg = $"Deleted: {id}"; }
                }
                catch (Exception ex)
                {
                    var m = ex.Unwrap().Message;
                    lock (_lock) { _pendingSuccess = false; _pendingMsg = $"Error: {m}"; }
                }
                finally
                {
                    _isDeleting = false;
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        if (OnPingDocument() != null) ExpireSolution(true);
                    }));
                }
            });
            DA.SetData(0, false);
            DA.SetData(1, "Deleting...");
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Delete");
    }
}
