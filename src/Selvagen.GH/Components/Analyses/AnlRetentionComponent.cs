using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlRetentionComponent : SelvagenModuleComponentBase
    {
        public AnlRetentionComponent()
            : base("Analyses Retention", "AnlRt",
                   "Upload analysis retention wall data. [PT: Muro de Contenção]", "05 Analysis") { }

        protected override string ModuleTable => "analyses";
        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddNumberParameter("RetHMin", "RHn", "Retention wall minimum height (m)", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("RetHMax", "RHx", "Retention wall maximum height (m)", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("RetArea", "RA", "Retention wall area", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("RetCost", "RC", "Retention wall cost", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetNumber(DA, 1, out var retHMin)) values["retention_height_min"] = retHMin;
            if (TryGetNumber(DA, 2, out var retHMax)) values["retention_height_max"] = retHMax;
            if (TryGetNumber(DA, 3, out var retArea)) values["retention_area"] = retArea;
            if (TryGetNumber(DA, 4, out var retCost)) values["retention_cost"] = retCost;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlRetention");
    }
}
