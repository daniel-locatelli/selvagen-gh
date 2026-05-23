using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlRockComponent : SelvagenModuleComponentBase
    {
        public AnlRockComponent()
            : base("Analyses Rock", "AnlRk",
                   "Upload analyses rock data", "05 Analysis") { }

        protected override string ModuleTable => "analyses";
        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("RockMeshID", "RM", "Rock mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("RockLabelsHID", "RLH", "Rock height labels asset ID", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddTextParameter("RockLabelsVID", "RLV", "Rock volume labels asset ID", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("RockHMin", "RkHn", "Rock minimum height (m)", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("RockHMax", "RkHx", "Rock maximum height (m)", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("RockTotalVolCut", "RTV", "Rock total volume cut", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var rockMesh)) values["rock_mesh_id"] = rockMesh;
            if (TryGetText(DA, 2, out var rockLabelsH)) values["rock_text_3d_set_height_id"] = rockLabelsH;
            if (TryGetText(DA, 3, out var rockLabelsV)) values["rock_text_3d_set_vol_id"] = rockLabelsV;
            if (TryGetNumber(DA, 4, out var rockHMin)) values["rock_height_min"] = rockHMin;
            if (TryGetNumber(DA, 5, out var rockHMax)) values["rock_height_max"] = rockHMax;
            if (TryGetNumber(DA, 6, out var rockTotalVol)) values["rock_total_vol_cut"] = rockTotalVol;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlRock");
    }
}
