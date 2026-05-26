using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptEarthTerrainComponent : SelvagenModuleComponentBase
    {
        public OptEarthTerrainComponent()
            : base("Optimizations Earth Terrain", "OptET",
                   "Upload optimizations earth terrain data. [Terraplanagem Otimizada (Terreno)]", "06 Optimizations") { }

        protected override string ModuleTable => "optimizations";
        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Mesh ID", "M", "Mesh asset ID [Malha]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Volume Compact Fill", "VCF", "Volume compact fill [Volume de Aterro Compactado]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Volume Bulking Fill", "VBF", "Volume bulking fill [Volume de Aterro Empolado]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Volume Cut", "VC", "Volume cut [Volume de Corte]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("Volume Import", "VI", "Volume import [Volume de Importação]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("Volume Export", "VE", "Volume export [Volume de Exportação]", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var terrMeshId)) values["earth_mesh_terrain_id"] = terrMeshId;
            if (TryGetNumber(DA, 2, out var terrVolCompFill)) values["earth_terrain_vol_compact_fill"] = terrVolCompFill;
            if (TryGetNumber(DA, 3, out var terrVolBulkFill)) values["earth_terrain_vol_bulking_fill"] = terrVolBulkFill;
            if (TryGetNumber(DA, 4, out var terrVolCut)) values["earth_terrain_vol_cut"] = terrVolCut;
            if (TryGetNumber(DA, 5, out var terrVolImport)) values["earth_terrain_vol_import"] = terrVolImport;
            if (TryGetNumber(DA, 6, out var terrVolExport)) values["earth_terrain_vol_export"] = terrVolExport;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptEarthTerrain");
    }
}
