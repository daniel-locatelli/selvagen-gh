using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoBaseComponent : SelvagenModuleComponentBase
    {
        public TopoBaseComponent()
            : base("Topography Base", "TpBs",
                   "Upload topography base data (mesh, areas, TDR)", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("BaseMeshID", "BM", "Base mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("BaseArea2D", "BA2", "Base 2D area", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("BaseArea3D", "BA3", "Base 3D area", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("BaseTDR", "BTDR", "Base true dimension rate", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var baseMeshId))
                values["base_mesh_id"] = baseMeshId;
            if (TryGetNumber(DA, 2, out var baseArea2d))
                values["base_area_2d"] = baseArea2d;
            if (TryGetNumber(DA, 3, out var baseArea3d))
                values["base_area_3d"] = baseArea3d;
            if (TryGetNumber(DA, 4, out var baseTdr))
                values["base_true_dimension_rate"] = baseTdr;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoBase");
    }
}
