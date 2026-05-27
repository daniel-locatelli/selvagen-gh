using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoElevationComponent : SelvagenModuleComponentBase
    {
        public TopoElevationComponent()
            : base("Topography Elevation", "TpEl",
                   "Upload topography elevation data. [Gradiente de Elevação]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Mesh ID", "M", "Mesh asset ID [Malha]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Curves ID", "C", "Curve set asset ID [Curvas]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Minimum", "Min", "Minimum [Mínima]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Maximum", "Max", "Maximum [Máxima]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddTextParameter("Legend ID", "LgdID",
                "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var elevMeshId))
                values["elevation_mesh_id"] = elevMeshId;
            if (TryGetText(DA, 2, out var elevCurvesId))
                values["elevation_curve_set_id"] = elevCurvesId;
            if (TryGetNumber(DA, 3, out var elevMin))
                values["elevation_min"] = elevMin;
            if (TryGetNumber(DA, 4, out var elevMax))
                values["elevation_max"] = elevMax;
            if (TryGetText(DA, Params.Input.Count - 2, out var legendId))
                values["elevation_legend_id"] = legendId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoElevation");
    }
}
