using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

namespace Selvagen.GH.Components
{
    public class SelvagenDeleteCustomPropertyComponent : SelvagenActionComponentBase
    {
        public SelvagenDeleteCustomPropertyComponent()
            : base("Delete Custom Property", "SvDelProp",
                   "Delete one or more custom properties from a project by key. [Excluir Propriedade Personalizada]",
                   "07 Shared") { }

        public override Guid ComponentGuid => new Guid("A1000006-0001-4000-8000-000000000003");
        protected override Bitmap Icon => IconLoader.Load("DeleteCustomProperty");

        // Red gradient signals destructive action
        public override string ActionLabel        => "Delete";
        public override string ActionLabelRunning => "Deleting...";
        public override Color  ButtonGradientTop    => Color.FromArgb(180, 90, 90);
        public override Color  ButtonGradientBottom => Color.FromArgb(120, 40, 40);

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID",            GH_ParamAccess.item);
            pManager.AddTextParameter("Key",        "K",     "Keys to delete (list)",   GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "OK", "Whether the operation completed", GH_ParamAccess.item);
            pManager.AddTextParameter   ("Status",  "S",  "Operation status",                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "";
            var keys = new List<string>();
            DA.GetData(0, ref projectId);
            DA.GetDataList(1, keys);

            var client = SessionManager.Current;

            if (!ActionRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(0, false);
                DA.SetData(1, "Ready to delete.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(0, false);
                DA.SetData(1, "Not logged in.");
                return;
            }
            if (string.IsNullOrEmpty(projectId))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Project ID is required.");
                DA.SetData(0, false);
                DA.SetData(1, "Missing Project ID");
                return;
            }
            if (keys.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nothing to delete.");
                DA.SetData(0, false);
                DA.SetData(1, "Nothing to delete.");
                return;
            }

            // Trim each key; ignore empties to be forgiving
            var cleaned = new List<string>(keys.Count);
            foreach (var k in keys)
            {
                var t = (k ?? "").Trim();
                if (t.Length > 0) cleaned.Add(t);
            }
            if (cleaned.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "All provided keys were blank.");
                DA.SetData(0, false);
                DA.SetData(1, "Nothing to delete.");
                return;
            }

            try
            {
                IsRunning = true;
                ForceCanvasRefresh();

                int n = Task.Run(() => client.DeleteCustomPropertiesAsync(projectId, cleaned.ToArray()))
                            .GetAwaiter().GetResult();

                DA.SetData(0, true);
                DA.SetData(1, $"Deleted {n} properties");
            }
            catch (Exception ex)
            {
                DA.SetData(0, false);
                SetActionError(DA, 1, ex);
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}
