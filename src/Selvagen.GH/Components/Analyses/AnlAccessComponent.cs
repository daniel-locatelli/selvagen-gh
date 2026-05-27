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
            pManager.AddTextParameter("Curves ID", "C", "Curves asset ID [Curvas]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Labels ID", "L", "Labels asset ID [Rótulos]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Reference", "R", "Reference value [Referência]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Rate", "%", "Rate [Taxa]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddTextParameter("Legend ID", "LgdID",
                "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var accCurves)) values["access_curve_set_id"] = accCurves;
            if (TryGetText(DA, 2, out var accLabels)) values["access_label_set_id"] = accLabels;
            if (TryGetNumber(DA, 3, out var accRef)) values["access_ref"] = accRef;
            if (TryGetNumber(DA, 4, out var accRate)) values["access_rate"] = accRate;
            if (TryGetText(DA, Params.Input.Count - 2, out var legendId)) values["access_legend_id"] = legendId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlAccess");
    }
}
