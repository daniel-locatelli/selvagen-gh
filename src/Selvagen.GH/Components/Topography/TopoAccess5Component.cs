using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class TopoAccess5Component : SelvagenModuleComponentBase
    {
        public TopoAccess5Component()
            : base("Topography Access 5", "TpA5",
                   "Upload topography access 5m data. [Acessibilidade 5m]", "03 Topography") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("A1000001-0001-4000-8000-000000000007");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Access 5m Mesh ID", "A5M", "Access 5m mesh asset ID [Malha de Acessibilidade 5m]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Access 5m Reference", "A5R", "Access 5m reference value [Referência de Acessibilidade 5m]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Access 5m Rate", "A5%", "Access 5m rate [Taxa de Acessibilidade 5m]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var acc5MeshId))
                values["access5_mesh_id"] = acc5MeshId;
            if (TryGetNumber(DA, 2, out var acc5Ref))
                values["access5_ref"] = acc5Ref;
            if (TryGetNumber(DA, 3, out var acc5Rate))
                values["access5_rate"] = acc5Rate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("TopoAccess5");
    }
}
