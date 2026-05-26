using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoBaseComponent : SelvagenModuleComponentBase
    {
        public TopoBaseComponent()
            : base("Topography Base", "TpBs",
                   "Upload topography base data (mesh, areas, TDR). [Terreno Base]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Mesh ID", "M", "Mesh asset ID [Malha]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Area 2D", "A2", "2D area [Área 2D]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Area 3D", "A3", "3D area [Área 3D]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("True Dimension Rate", "TDR", "True dimension rate [Taxa de Dimensão Real]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var baseMeshId))
                values["base_mesh_id"] = baseMeshId;
            if (TryGetNumber(DA, 2, out var baseArea2d))
                values["base_area_2d"] = baseArea2d;
            if (TryGetNumber(DA, 3, out var baseArea3d))
                values["base_area_3d"] = baseArea3d;
            if (TryGetNumber(DA, 4, out var baseTdr))
                values["base_true_dimension_rate"] = baseTdr;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoBase");
    }
}
