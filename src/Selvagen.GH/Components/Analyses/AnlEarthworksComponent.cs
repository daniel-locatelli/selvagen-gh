using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlEarthworksComponent : SelvagenModuleComponentBase
    {
        public AnlEarthworksComponent()
            : base("Analyses Earthworks", "AnlEw",
                   "Upload analysis earthworks data. [Movimentação de Terra]", "05 Analysis") { }

        protected override string ModuleTable => "analyses";
        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Terrain Mesh ID", "ETM", "Terrain mesh asset ID [Malha do Terreno]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Massing Mesh ID", "EMM", "Massing mesh asset ID [Malha de Volumetria]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Volume Fill", "VF", "Volume fill [Volume de Aterro]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Volume Cut", "VC", "Volume cut [Volume de Corte]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("Volume Import", "VI", "Volume import [Volume de Importação]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("Volume Export", "VE", "Volume export [Volume de Exportação]", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("Cost Import", "CI", "Import cost [Custo de Importação]", GH_ParamAccess.item);
            pManager[7].Optional = true;
            pManager.AddNumberParameter("Cost Export", "CE", "Export cost [Custo de Exportação]", GH_ParamAccess.item);
            pManager[8].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var earthTerrain)) values["earth_mesh_terrain_id"] = earthTerrain;
            if (TryGetText(DA, 2, out var earthMassing)) values["earth_mesh_massing_id"] = earthMassing;
            if (TryGetNumber(DA, 3, out var evf)) values["earth_vol_fill"] = evf;
            if (TryGetNumber(DA, 4, out var evc)) values["earth_vol_cut"] = evc;
            if (TryGetNumber(DA, 5, out var evi)) values["earth_vol_import"] = evi;
            if (TryGetNumber(DA, 6, out var eve)) values["earth_vol_export"] = eve;
            if (TryGetNumber(DA, 7, out var eci)) values["earth_cost_import"] = eci;
            if (TryGetNumber(DA, 8, out var ece)) values["earth_cost_export"] = ece;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlEarthworks");
    }
}
