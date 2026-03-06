using System;
using System.Collections.Generic;
using System.Text.Json;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class SelvagenAnalysesComponent : SelvagenModuleComponentBase
    {
        public SelvagenAnalysesComponent()
            : base("Analyses", "SvAnalyses",
                "Upload analyses module data for a project. Creates the record if needed.")
        { }

        public override Guid ComponentGuid => new Guid("D3A4B5C6-E7F8-4012-ABCD-3456FACE0003");

        protected override string ModuleTable => "analyses";

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0: ProjectID (required)
            pManager.AddTextParameter("ProjectID", "PID", "Project ID", GH_ParamAccess.item);

            // ── Earthworks ──────────────────────────────────────────────────
            // 1: earth_mesh_terrain_id
            pManager.AddTextParameter("EarthTerrainMeshID", "ETM", "Terrain mesh asset ID", GH_ParamAccess.item);
            Params.Input[1].Optional = true;
            // 2: earth_mesh_massing_id
            pManager.AddTextParameter("EarthMassingMeshID", "EMM", "Massing mesh asset ID", GH_ParamAccess.item);
            Params.Input[2].Optional = true;
            // 3: earth_vol_fill
            pManager.AddNumberParameter("EarthVolFill", "EVF", "Earth volume fill (m3)", GH_ParamAccess.item);
            Params.Input[3].Optional = true;
            // 4: earth_vol_cut
            pManager.AddNumberParameter("EarthVolCut", "EVC", "Earth volume cut (m3)", GH_ParamAccess.item);
            Params.Input[4].Optional = true;
            // 5: earth_vol_import
            pManager.AddNumberParameter("EarthVolImport", "EVI", "Earth volume import (m3)", GH_ParamAccess.item);
            Params.Input[5].Optional = true;
            // 6: earth_vol_export
            pManager.AddNumberParameter("EarthVolExport", "EVE", "Earth volume export (m3)", GH_ParamAccess.item);
            Params.Input[6].Optional = true;
            // 7: earth_cost_import
            pManager.AddNumberParameter("EarthCostImport", "ECI", "Earth import cost", GH_ParamAccess.item);
            Params.Input[7].Optional = true;
            // 8: earth_cost_export
            pManager.AddNumberParameter("EarthCostExport", "ECE", "Earth export cost", GH_ParamAccess.item);
            Params.Input[8].Optional = true;

            // ── Retention ───────────────────────────────────────────────────
            // 9: retention_height_min
            pManager.AddNumberParameter("RetHMin", "RHn", "Retention wall minimum height (m)", GH_ParamAccess.item);
            Params.Input[9].Optional = true;
            // 10: retention_height_max
            pManager.AddNumberParameter("RetHMax", "RHx", "Retention wall maximum height (m)", GH_ParamAccess.item);
            Params.Input[10].Optional = true;
            // 11: retention_area
            pManager.AddNumberParameter("RetArea", "RA", "Retention wall area (m2)", GH_ParamAccess.item);
            Params.Input[11].Optional = true;
            // 12: retention_cost
            pManager.AddNumberParameter("RetCost", "RC", "Retention wall cost", GH_ParamAccess.item);
            Params.Input[12].Optional = true;

            // ── Rock ────────────────────────────────────────────────────────
            // 13: rock_mesh_id
            pManager.AddTextParameter("RockMeshID", "RM", "Rock mesh asset ID", GH_ParamAccess.item);
            Params.Input[13].Optional = true;
            // 14: rock_text_3d_set_height_id
            pManager.AddTextParameter("RockLabelsHID", "RLH", "Rock height labels asset ID", GH_ParamAccess.item);
            Params.Input[14].Optional = true;
            // 15: rock_text_3d_set_vol_id
            pManager.AddTextParameter("RockLabelsVID", "RLV", "Rock volume labels asset ID", GH_ParamAccess.item);
            Params.Input[15].Optional = true;
            // 16: rock_height_min
            pManager.AddNumberParameter("RockHMin", "RkHn", "Rock minimum height (m)", GH_ParamAccess.item);
            Params.Input[16].Optional = true;
            // 17: rock_height_max
            pManager.AddNumberParameter("RockHMax", "RkHx", "Rock maximum height (m)", GH_ParamAccess.item);
            Params.Input[17].Optional = true;
            // 18: rock_total_vol_cut
            pManager.AddNumberParameter("RockTotalVolCut", "RTV", "Rock total volume cut (m3)", GH_ParamAccess.item);
            Params.Input[18].Optional = true;

            // ── Access ──────────────────────────────────────────────────────
            // 19: access_curve_set_id
            pManager.AddTextParameter("AccCurvesID", "AC", "Access curves asset ID", GH_ParamAccess.item);
            Params.Input[19].Optional = true;
            // 20: access_text_3d_set_id
            pManager.AddTextParameter("AccLabelsID", "AL", "Access labels asset ID", GH_ParamAccess.item);
            Params.Input[20].Optional = true;
            // 21: access_ref
            pManager.AddNumberParameter("AccRef", "AR", "Access reference value", GH_ParamAccess.item);
            Params.Input[21].Optional = true;
            // 22: access_rate
            pManager.AddNumberParameter("AccRate", "ARt", "Access rate value", GH_ParamAccess.item);
            Params.Input[22].Optional = true;

            // ── Custom ──────────────────────────────────────────────────────
            // 23: properties (jsonb)
            pManager.AddTextParameter("Properties", "Props", "Custom properties as JSON string", GH_ParamAccess.item);
            Params.Input[23].Optional = true;

            // 24: Upload trigger (must be last)
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();

            // ── Earthworks ──────────────────────────────────────────────────
            if (TryGetText(DA, 1, out string earthTerrain))
                values["earth_mesh_terrain_id"] = earthTerrain;
            if (TryGetText(DA, 2, out string earthMassing))
                values["earth_mesh_massing_id"] = earthMassing;
            if (TryGetNumber(DA, 3, out double evf))
                values["earth_vol_fill"] = evf;
            if (TryGetNumber(DA, 4, out double evc))
                values["earth_vol_cut"] = evc;
            if (TryGetNumber(DA, 5, out double evi))
                values["earth_vol_import"] = evi;
            if (TryGetNumber(DA, 6, out double eve))
                values["earth_vol_export"] = eve;
            if (TryGetNumber(DA, 7, out double eci))
                values["earth_cost_import"] = eci;
            if (TryGetNumber(DA, 8, out double ece))
                values["earth_cost_export"] = ece;

            // ── Retention ───────────────────────────────────────────────────
            if (TryGetNumber(DA, 9, out double retHMin))
                values["retention_height_min"] = retHMin;
            if (TryGetNumber(DA, 10, out double retHMax))
                values["retention_height_max"] = retHMax;
            if (TryGetNumber(DA, 11, out double retArea))
                values["retention_area"] = retArea;
            if (TryGetNumber(DA, 12, out double retCost))
                values["retention_cost"] = retCost;

            // ── Rock ────────────────────────────────────────────────────────
            if (TryGetText(DA, 13, out string rockMesh))
                values["rock_mesh_id"] = rockMesh;
            if (TryGetText(DA, 14, out string rockLabelsH))
                values["rock_text_3d_set_height_id"] = rockLabelsH;
            if (TryGetText(DA, 15, out string rockLabelsV))
                values["rock_text_3d_set_vol_id"] = rockLabelsV;
            if (TryGetNumber(DA, 16, out double rockHMin))
                values["rock_height_min"] = rockHMin;
            if (TryGetNumber(DA, 17, out double rockHMax))
                values["rock_height_max"] = rockHMax;
            if (TryGetNumber(DA, 18, out double rockTotalVol))
                values["rock_total_vol_cut"] = rockTotalVol;

            // ── Access ──────────────────────────────────────────────────────
            if (TryGetText(DA, 19, out string accCurves))
                values["access_curve_set_id"] = accCurves;
            if (TryGetText(DA, 20, out string accLabels))
                values["access_text_3d_set_id"] = accLabels;
            if (TryGetNumber(DA, 21, out double accRef))
                values["access_ref"] = accRef;
            if (TryGetNumber(DA, 22, out double accRate))
                values["access_rate"] = accRate;

            // ── Custom ──────────────────────────────────────────────────────
            if (TryGetJson(DA, 23, out JsonElement props))
                values["properties"] = props;

            return values;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Analyses");
    }
}
