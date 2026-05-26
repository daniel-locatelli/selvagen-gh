using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlAccessComponent : SelvagenModuleComponentBase
    {
        public AnlAccessComponent()
            : base("Analyses Access", "AnlAc",
                   "Upload analysis access data. [Acessibilidade]", "05 Analysis") { }

        protected override string ModuleTable => "analyses";
        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Access Curves ID", "AC", "Access curves asset ID [Curvas de Acessibilidade]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Access Labels ID", "AL", "Access labels asset ID [Rótulos de Acessibilidade]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Access Reference", "AR", "Access reference value [Referência de Acessibilidade]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Access Rate", "ARt", "Access rate [Taxa de Acessibilidade]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var accCurves)) values["access_curve_set_id"] = accCurves;
            if (TryGetText(DA, 2, out var accLabels)) values["access_label_set_id"] = accLabels;
            if (TryGetNumber(DA, 3, out var accRef)) values["access_ref"] = accRef;
            if (TryGetNumber(DA, 4, out var accRate)) values["access_rate"] = accRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlAccess");
    }
}
