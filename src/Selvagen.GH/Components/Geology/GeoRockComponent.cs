using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoRockComponent : SelvagenModuleComponentBase
    {
        public GeoRockComponent()
            : base("Geology Rock", "GeoRk",
                   "Upload geology rock data. [Afloramento de Rochas]", "04 Geology") { }

        protected override string ModuleTable => "geology";
        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Mesh ID", "M", "Mesh asset ID [Malha]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Curves ID", "C", "Curve set asset ID [Curvas]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Contour Interval", "CI", "Contour interval [Intervalo de Contorno]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var rockMeshId)) values["rock_mesh_id"] = rockMeshId;
            if (TryGetText(DA, 2, out var rockCurvesId)) values["rock_curve_set_id"] = rockCurvesId;
            if (TryGetNumber(DA, 3, out var rockContourInt)) values["rock_contour_interval"] = rockContourInt;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoRock");
    }
}
