using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoCoverageComponent : SelvagenModuleComponentBase
    {
        public GeoCoverageComponent()
            : base("Geology Coverage", "GeoCv",
                   "Upload geology coverage data. [Cobertura de Sondagem]", "04 Geology") { }

        protected override string ModuleTable => "geology";
        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Mesh ID", "M", "Mesh asset ID [Malha]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("Point Count", "NP", "Number of points [Número de Pontos]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Area", "A", "Area [Área]", GH_ParamAccess.item);
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
            if (TryGetText(DA, 1, out var covMeshId)) values["coverage_mesh_id"] = covMeshId;
            if (TryGetInt(DA, 2, out var covNumPoints)) values["coverage_number_points"] = covNumPoints;
            if (TryGetNumber(DA, 3, out var covArea)) values["coverage_area"] = covArea;
            if (TryGetNumber(DA, 4, out var covRate)) values["coverage_rate"] = covRate;
            if (TryGetText(DA, Params.Input.Count - 2, out var legendId)) values["coverage_legend_id"] = legendId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoCoverage");
    }
}
