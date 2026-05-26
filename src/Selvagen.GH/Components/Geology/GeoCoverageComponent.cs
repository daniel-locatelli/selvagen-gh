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
            pManager.AddTextParameter("Coverage Mesh ID", "CovM", "Coverage mesh asset ID [Malha de Cobertura]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddIntegerParameter("Coverage Point Count", "CovNP", "Number of coverage points [Número de Pontos de Sondagem]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Coverage Area", "CovA", "Coverage area [Área de Cobertura]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Coverage Rate", "CovR", "Coverage rate [Taxa de Cobertura]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var covMeshId)) values["coverage_mesh_id"] = covMeshId;
            if (TryGetInt(DA, 2, out var covNumPoints)) values["coverage_number_points"] = covNumPoints;
            if (TryGetNumber(DA, 3, out var covArea)) values["coverage_area"] = covArea;
            if (TryGetNumber(DA, 4, out var covRate)) values["coverage_rate"] = covRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoCoverage");
    }
}
