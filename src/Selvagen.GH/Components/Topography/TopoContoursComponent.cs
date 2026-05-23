using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoContoursComponent : SelvagenModuleComponentBase
    {
        public TopoContoursComponent()
            : base("Topography Contours", "TpCn",
                   "Upload topography contour data. [PT: Curvas de Nível]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("OutlineCurvesID", "OC", "Outline curve set asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("ContoursCurvesID", "CC", "Contours curve set asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddTextParameter("ContoursLabelsID", "CL", "Contours text 3D set asset ID", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("ContourInterval", "CI", "Contour interval", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var outlineCurvesId))
                values["outline_curve_set_id"] = outlineCurvesId;
            if (TryGetText(DA, 2, out var contoursCurvesId))
                values["contours_curve_set_id"] = contoursCurvesId;
            if (TryGetText(DA, 3, out var contoursLabelsId))
                values["contours_text_3d_set_id"] = contoursLabelsId;
            if (TryGetNumber(DA, 4, out var contourInterval))
                values["contour_interval"] = contourInterval;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoContours");
    }
}
