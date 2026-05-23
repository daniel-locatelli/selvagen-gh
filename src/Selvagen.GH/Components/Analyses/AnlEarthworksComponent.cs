using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlEarthworksComponent : SelvagenModuleComponentBase
    {
        public AnlEarthworksComponent()
            : base("Analyses Earthworks", "AnlEw",
                   "Upload analyses earthworks data", "Analyses") { }

        protected override string ModuleTable => "analyses";
        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("EarthTerrainMeshID", "ETM", "Terrain mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("EarthMassingMeshID", "EMM", "Massing mesh asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("EarthVolFill", "EVF", "Earth volume fill", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("EarthVolCut", "EVC", "Earth volume cut", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("EarthVolImport", "EVI", "Earth volume import", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("EarthVolExport", "EVE", "Earth volume export", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("EarthCostImport", "ECI", "Earth import cost", GH_ParamAccess.item);
            pManager[7].Optional = true;
            pManager.AddNumberParameter("EarthCostExport", "ECE", "Earth export cost", GH_ParamAccess.item);
            pManager[8].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
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
