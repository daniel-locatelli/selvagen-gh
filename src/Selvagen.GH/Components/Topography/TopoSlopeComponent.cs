using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoSlopeComponent : SelvagenModuleComponentBase
    {
        public TopoSlopeComponent()
            : base("Topography Slope", "TpSl",
                   "Upload topography slope data. [Declividade]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000005");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Slope Mesh ID", "SM", "Slope mesh asset ID [Malha de Declividade]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Slope Reference", "SR", "Slope reference value [Referência de Declividade]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Slope Restricted Rate", "SRR", "Slope restricted area rate [Taxa de Área Restrita]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Slope Minimum", "Smn", "Minimum slope [Declividade Mínima]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("Slope Maximum", "Smx", "Maximum slope [Declividade Máxima]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var slopeMeshId))
                values["slope_mesh_id"] = slopeMeshId;
            if (TryGetNumber(DA, 2, out var slopeRef))
                values["slope_ref"] = slopeRef;
            if (TryGetNumber(DA, 3, out var slopeRestRate))
                values["slope_restricted_area_rate"] = slopeRestRate;
            if (TryGetNumber(DA, 4, out var slopeMin))
                values["slope_min"] = slopeMin;
            if (TryGetNumber(DA, 5, out var slopeMax))
                values["slope_max"] = slopeMax;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoSlope");
    }
}
