using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoElevationComponent : SelvagenModuleComponentBase
    {
        public TopoElevationComponent()
            : base("Topography Elevation", "TpEl",
                   "Upload topography elevation data. [PT: Gradiente de Elevação]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("ElevMeshID", "EM", "Elevation mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("ElevCurvesID", "EC", "Elevation curve set asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("ElevMin", "Emn", "Minimum elevation", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("ElevMax", "Emx", "Maximum elevation", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
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
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoElevation");
    }
}
