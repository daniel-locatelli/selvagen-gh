using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptAccessComponent : SelvagenModuleComponentBase
    {
        public OptAccessComponent()
            : base("Optimizations Access", "OptAc",
                   "Upload optimizations access data", "06 Optimizations") { }

        protected override string ModuleTable => "optimizations";
        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("AccCurvesID", "AC", "Access curves asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("AccLabelsID", "AL", "Access labels asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("AccRef", "AR", "Access reference value", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("AccRate", "ARt", "Access rate", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var accCurves)) values["access_curve_set_id"] = accCurves;
            if (TryGetText(DA, 2, out var accLabels)) values["access_text_3d_set_id"] = accLabels;
            if (TryGetNumber(DA, 3, out var accRef)) values["access_ref"] = accRef;
            if (TryGetNumber(DA, 4, out var accRate)) values["access_rate"] = accRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptAccess");
    }
}
