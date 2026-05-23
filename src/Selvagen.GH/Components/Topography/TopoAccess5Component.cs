using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoAccess5Component : SelvagenModuleComponentBase
    {
        public TopoAccess5Component()
            : base("Topography Access 5", "TpA5",
                   "Upload topography access 5m data", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000007");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("Acc5MeshID", "A5M", "Access 5m mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Acc5Ref", "A5R", "Access 5m reference value", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Acc5Rate", "A5%", "Access 5m rate", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var acc5MeshId))
                values["access5_mesh_id"] = acc5MeshId;
            if (TryGetNumber(DA, 2, out var acc5Ref))
                values["access5_ref"] = acc5Ref;
            if (TryGetNumber(DA, 3, out var acc5Rate))
                values["access5_rate"] = acc5Rate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoAccess5");
    }
}
