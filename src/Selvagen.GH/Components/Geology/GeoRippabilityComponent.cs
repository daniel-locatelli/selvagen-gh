using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoRippabilityComponent : SelvagenModuleComponentBase
    {
        public GeoRippabilityComponent()
            : base("Geology Rippability", "GeoRp",
                   "Upload geology rippability data. [Escavabilidade]", "04 Geology") { }

        protected override string ModuleTable => "geology";
        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Mesh ID", "M", "Mesh asset ID [Malha]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Legend ID", "LgdID",
                "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var ripMeshId)) values["rippability_mesh_id"] = ripMeshId;
            if (TryGetText(DA, Params.Input.Count - 2, out var legendId)) values["rippability_legend_id"] = legendId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoRippability");
    }
}
