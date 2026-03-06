using System;
using System.Collections.Generic;
using System.Text.Json;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Grasshopper component that manages the geology module table.
    /// Creates the record if needed, then PATCHes all provided field values.
    /// </summary>
    public class SelvagenGeologyComponent : SelvagenModuleComponentBase
    {
        public SelvagenGeologyComponent()
            : base("Geology", "SvGeo",
                   "Upload geology module data for a project. Creates the record if needed.") { }

        protected override string ModuleTable => "geology";

        public override Guid ComponentGuid => new Guid("D2A3B4C5-E6F7-4901-ABCD-2345FACE0002");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - ProjectID (required)
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);

            // ── Coverage ────────────────────────────────────────────────
            // 1
            pManager.AddTextParameter("CovMeshID", "CovM", "Coverage mesh asset ID (UUID)", GH_ParamAccess.item);
            pManager[1].Optional = true;

            // 2
            pManager.AddIntegerParameter("CovNumPoints", "CovNP", "Number of coverage points", GH_ParamAccess.item);
            pManager[2].Optional = true;

            // 3
            pManager.AddNumberParameter("CovArea", "CovA", "Coverage area", GH_ParamAccess.item);
            pManager[3].Optional = true;

            // 4
            pManager.AddNumberParameter("CovRate", "CovR", "Coverage rate", GH_ParamAccess.item);
            pManager[4].Optional = true;

            // ── Rock ────────────────────────────────────────────────────
            // 5
            pManager.AddTextParameter("RockMeshID", "RkM", "Rock mesh asset ID (UUID)", GH_ParamAccess.item);
            pManager[5].Optional = true;

            // 6
            pManager.AddTextParameter("RockCurvesID", "RkC", "Rock curve set asset ID (UUID)", GH_ParamAccess.item);
            pManager[6].Optional = true;

            // 7
            pManager.AddNumberParameter("RockContourInt", "RkCI", "Rock contour interval", GH_ParamAccess.item);
            pManager[7].Optional = true;

            // ── Rippability ─────────────────────────────────────────────
            // 8
            pManager.AddTextParameter("RipMeshID", "RipM", "Rippability mesh asset ID (UUID)", GH_ParamAccess.item);
            pManager[8].Optional = true;

            // ── Soil ────────────────────────────────────────────────────
            // 9
            pManager.AddTextParameter("SoilMeshID", "SoilM", "Soil mesh asset ID (UUID)", GH_ParamAccess.item);
            pManager[9].Optional = true;

            // 10
            pManager.AddNumberParameter("SoilHMin", "SHMin", "Soil minimum height", GH_ParamAccess.item);
            pManager[10].Optional = true;

            // 11
            pManager.AddNumberParameter("SoilHMax", "SHMax", "Soil maximum height", GH_ParamAccess.item);
            pManager[11].Optional = true;

            // ── Depth ───────────────────────────────────────────────────
            // 12
            pManager.AddTextParameter("DepthMeshID", "DepM", "Depth mesh asset ID (UUID)", GH_ParamAccess.item);
            pManager[12].Optional = true;

            // 13
            pManager.AddNumberParameter("DepthRef", "DepR", "Depth reference value", GH_ParamAccess.item);
            pManager[13].Optional = true;

            // 14
            pManager.AddNumberParameter("DepthUsRate", "DepUR", "Depth usability rate", GH_ParamAccess.item);
            pManager[14].Optional = true;

            // ── Custom ──────────────────────────────────────────────────
            // 15
            pManager.AddTextParameter("Properties", "Props", "Custom properties as JSON string", GH_ParamAccess.item);
            pManager[15].Optional = true;

            // 16 - Upload trigger (MUST be last)
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();

            // Coverage
            if (TryGetText(DA, 1, out var covMeshId))
                values["coverage_mesh_id"] = covMeshId;

            if (TryGetInt(DA, 2, out var covNumPoints))
                values["coverage_number_points"] = covNumPoints;

            if (TryGetNumber(DA, 3, out var covArea))
                values["coverage_area"] = covArea;

            if (TryGetNumber(DA, 4, out var covRate))
                values["coverage_rate"] = covRate;

            // Rock
            if (TryGetText(DA, 5, out var rockMeshId))
                values["rock_mesh_id"] = rockMeshId;

            if (TryGetText(DA, 6, out var rockCurvesId))
                values["rock_curve_set_id"] = rockCurvesId;

            if (TryGetNumber(DA, 7, out var rockContourInt))
                values["rock_contour_interval"] = rockContourInt;

            // Rippability
            if (TryGetText(DA, 8, out var ripMeshId))
                values["rippability_mesh_id"] = ripMeshId;

            // Soil
            if (TryGetText(DA, 9, out var soilMeshId))
                values["soil_mesh_id"] = soilMeshId;

            if (TryGetNumber(DA, 10, out var soilHMin))
                values["soil_height_min"] = soilHMin;

            if (TryGetNumber(DA, 11, out var soilHMax))
                values["soil_height_max"] = soilHMax;

            // Depth
            if (TryGetText(DA, 12, out var depthMeshId))
                values["depth_mesh_id"] = depthMeshId;

            if (TryGetNumber(DA, 13, out var depthRef))
                values["depth_ref"] = depthRef;

            if (TryGetNumber(DA, 14, out var depthUsRate))
                values["depth_usability_rate"] = depthUsRate;

            // Custom properties (JSON)
            if (TryGetJson(DA, 15, out var properties))
                values["properties"] = properties;

            return values;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Geology");
    }
}
