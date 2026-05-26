using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptEarthTotalComponent : SelvagenModuleComponentBase
    {
        public OptEarthTotalComponent()
            : base("Optimizations Earth Total", "OptETt",
                   "Upload optimizations earth total data. [Terraplanagem Otimizada (Total)]", "06 Optimizations") { }

        protected override string ModuleTable => "optimizations";
        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Total Volume Compact Fill", "TotCF", "Total volume compact fill [Volume Total de Aterro Compactado]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Total Volume Bulking Fill", "TotBF", "Total volume bulking fill [Volume Total de Aterro Empolado]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Total Volume Cut", "TotCt", "Total volume cut [Volume Total de Corte]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Total Volume Import", "TotIm", "Total volume import [Volume Total de Importação]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("Total Volume Export", "TotEx", "Total volume export [Volume Total de Exportação]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("Total Cost Import", "TotCIm", "Total cost import [Custo Total de Importação]", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("Total Cost Export", "TotCEx", "Total cost export [Custo Total de Exportação]", GH_ParamAccess.item);
            pManager[7].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetNumber(DA, 1, out var totalVolCompFill)) values["earth_total_vol_compact_fill"] = totalVolCompFill;
            if (TryGetNumber(DA, 2, out var totalVolBulkFill)) values["earth_total_vol_bulking_fill"] = totalVolBulkFill;
            if (TryGetNumber(DA, 3, out var totalVolCut)) values["earth_total_vol_cut"] = totalVolCut;
            if (TryGetNumber(DA, 4, out var totalVolImport)) values["earth_total_vol_import"] = totalVolImport;
            if (TryGetNumber(DA, 5, out var totalVolExport)) values["earth_total_vol_export"] = totalVolExport;
            if (TryGetNumber(DA, 6, out var totalCostImport)) values["earth_total_cost_import"] = totalCostImport;
            if (TryGetNumber(DA, 7, out var totalCostExport)) values["earth_total_cost_export"] = totalCostExport;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptEarthTotal");
    }
}
