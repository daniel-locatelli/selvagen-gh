using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptRetentionComponent : SelvagenModuleComponentBase
    {
        public OptRetentionComponent()
            : base("Optimizations Retention", "OptRt",
                   "Upload optimizations retention wall data. [Contenção Otimizada]", "06 Optimizations") { }

        protected override string ModuleTable => "optimizations";
        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000005");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Retention Height Min", "RetMin", "Retention height min [Altura Mínima de Contenção]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Retention Height Max", "RetMax", "Retention height max [Altura Máxima de Contenção]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Retention Area", "RetA", "Retention area [Área de Contenção]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Retention Cost", "RetC", "Retention cost [Custo de Contenção]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
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
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptRetention");
    }
}
