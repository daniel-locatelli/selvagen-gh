using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoSoilComponent : SelvagenModuleComponentBase
    {
        public GeoSoilComponent()
            : base("Geology Soil", "GeoSl",
                   "Upload geology soil data", "Geology") { }

        protected override string ModuleTable => "geology";
        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000004");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("SoilMeshID", "SoilM", "Soil mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("SoilHMin", "SHMin", "Soil minimum height", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("SoilHMax", "SHMax", "Soil maximum height", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var soilMeshId)) values["soil_mesh_id"] = soilMeshId;
            if (TryGetNumber(DA, 2, out var soilHMin)) values["soil_height_min"] = soilHMin;
            if (TryGetNumber(DA, 3, out var soilHMax)) values["soil_height_max"] = soilHMax;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoSoil");
    }
}
