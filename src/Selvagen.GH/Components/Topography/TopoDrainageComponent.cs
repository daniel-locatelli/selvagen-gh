using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoDrainageComponent : SelvagenModuleComponentBase
    {
        public TopoDrainageComponent()
            : base("Topography Drainage", "TpDr",
                   "Upload topography drainage data. [Drenagem]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000008");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Curves ID", "C", "Curve set asset ID [Curvas]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("Flow Paths", "FP", "Total flow paths [Caminhos de Fluxo]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Concentration Rate", "CR", "Concentration rate [Taxa de Concentração]", GH_ParamAccess.item);
            pManager[3].Optional = true;
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var drainCurvesId))
                values["drainage_curve_set_id"] = drainCurvesId;
            if (TryGetInt(DA, 2, out var drainFlowPaths))
                values["drainage_total_flow_paths"] = drainFlowPaths;
            if (TryGetNumber(DA, 3, out var drainConcRate))
                values["drainage_concentration_rate"] = drainConcRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoDrainage");
    }
}
