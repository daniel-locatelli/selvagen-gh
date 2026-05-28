using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoContoursComponent : SelvagenModuleComponentBase
    {
        public TopoContoursComponent()
            : base("Topography Contours", "TpCn",
                   "Upload topography contour data. [Curvas de Nível]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Outline Curves ID", "OC", "Outline curve set asset ID [Curvas de Contorno]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Curves ID", "C", "Curves asset ID [Curvas de Nível]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddTextParameter("Labels ID", "L", "Labels asset ID [Rótulos]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Interval", "I", "Interval [Intervalo]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddTextParameter("Legend ID", "LgdID",
                "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
            pManager[5].Optional = true;
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var outlineCurvesId))
                values["outline_curve_set_id"] = outlineCurvesId;
            if (TryGetText(DA, 2, out var contoursCurvesId))
                values["contours_curve_set_id"] = contoursCurvesId;
            if (TryGetText(DA, 3, out var contoursLabelsId))
                values["contours_label_set_id"] = contoursLabelsId;
            if (TryGetNumber(DA, 4, out var contourInterval))
                values["contour_interval"] = contourInterval;
            if (TryGetText(DA, Params.Input.Count - 1, out var legendId))
                values["contours_legend_id"] = legendId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoContours");
    }
}
