using System;
using System.Collections.Generic;
using System.Text.Json;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class SelvagenOptimizationsComponent : SelvagenModuleComponentBase
    {
        public SelvagenOptimizationsComponent()
            : base("Optimizations", "SvOptim",
                "Upload optimizations module data for a project. Creates the record if needed.")
        { }

        public override Guid ComponentGuid => new Guid("D4A5B6C7-E8F9-4123-ABCD-4567FACE0004");

        protected override string ModuleTable => "optimizations";

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - ProjectID
            pManager.AddTextParameter("ProjectID", "PID", "Project ID", GH_ParamAccess.item);

            // ── Access ──────────────────────────────────────────────────────────
            // 1
            pManager.AddTextParameter("AccCurvesID", "AccCrv", "Access curve set asset ID (access_curve_set_id)", GH_ParamAccess.item);
            pManager[1].Optional = true;
            // 2
            pManager.AddTextParameter("AccLabelsID", "AccLbl", "Access text 3D set asset ID (access_text_3d_set_id)", GH_ParamAccess.item);
            pManager[2].Optional = true;
            // 3
            pManager.AddNumberParameter("AccRef", "AccRef", "Access reference value (access_ref)", GH_ParamAccess.item);
            pManager[3].Optional = true;
            // 4
            pManager.AddNumberParameter("AccRate", "AccRt", "Access rate value (access_rate)", GH_ParamAccess.item);
            pManager[4].Optional = true;

            // ── Earth Terrain ───────────────────────────────────────────────────
            // 5
            pManager.AddTextParameter("TerrMeshID", "TerrM", "Terrain mesh asset ID (earth_mesh_terrain_id)", GH_ParamAccess.item);
            pManager[5].Optional = true;
            // 6
            pManager.AddNumberParameter("TerrVolCompFill", "TerrCF", "Terrain volume compact fill (earth_terrain_vol_compact_fill)", GH_ParamAccess.item);
            pManager[6].Optional = true;
            // 7
            pManager.AddNumberParameter("TerrVolBulkFill", "TerrBF", "Terrain volume bulking fill (earth_terrain_vol_bulking_fill)", GH_ParamAccess.item);
            pManager[7].Optional = true;
            // 8
            pManager.AddNumberParameter("TerrVolCut", "TerrCt", "Terrain volume cut (earth_terrain_vol_cut)", GH_ParamAccess.item);
            pManager[8].Optional = true;
            // 9
            pManager.AddNumberParameter("TerrVolImport", "TerrIm", "Terrain volume import (earth_terrain_vol_import)", GH_ParamAccess.item);
            pManager[9].Optional = true;
            // 10
            pManager.AddNumberParameter("TerrVolExport", "TerrEx", "Terrain volume export (earth_terrain_vol_export)", GH_ParamAccess.item);
            pManager[10].Optional = true;

            // ── Earth Lots ──────────────────────────────────────────────────────
            // 11
            pManager.AddTextParameter("LotsMeshID", "LotsM", "Lots mesh asset ID (earth_mesh_lots_id)", GH_ParamAccess.item);
            pManager[11].Optional = true;
            // 12
            pManager.AddNumberParameter("LotsVolCompFill", "LotsCF", "Lots volume compact fill (earth_lots_vol_compact_fill)", GH_ParamAccess.item);
            pManager[12].Optional = true;
            // 13
            pManager.AddNumberParameter("LotsVolBulkFill", "LotsBF", "Lots volume bulking fill (earth_lots_vol_bulking_fill)", GH_ParamAccess.item);
            pManager[13].Optional = true;
            // 14
            pManager.AddNumberParameter("LotsVolCut", "LotsCt", "Lots volume cut (earth_lots_vol_cut)", GH_ParamAccess.item);
            pManager[14].Optional = true;
            // 15
            pManager.AddNumberParameter("LotsVolImport", "LotsIm", "Lots volume import (earth_lots_vol_import)", GH_ParamAccess.item);
            pManager[15].Optional = true;
            // 16
            pManager.AddNumberParameter("LotsVolExport", "LotsEx", "Lots volume export (earth_lots_vol_export)", GH_ParamAccess.item);
            pManager[16].Optional = true;

            // ── Earth Total ─────────────────────────────────────────────────────
            // 17
            pManager.AddNumberParameter("TotalVolCompFill", "TotCF", "Total volume compact fill (earth_total_vol_compact_fill)", GH_ParamAccess.item);
            pManager[17].Optional = true;
            // 18
            pManager.AddNumberParameter("TotalVolBulkFill", "TotBF", "Total volume bulking fill (earth_total_vol_bulking_fill)", GH_ParamAccess.item);
            pManager[18].Optional = true;
            // 19
            pManager.AddNumberParameter("TotalVolCut", "TotCt", "Total volume cut (earth_total_vol_cut)", GH_ParamAccess.item);
            pManager[19].Optional = true;
            // 20
            pManager.AddNumberParameter("TotalVolImport", "TotIm", "Total volume import (earth_total_vol_import)", GH_ParamAccess.item);
            pManager[20].Optional = true;
            // 21
            pManager.AddNumberParameter("TotalVolExport", "TotEx", "Total volume export (earth_total_vol_export)", GH_ParamAccess.item);
            pManager[21].Optional = true;
            // 22
            pManager.AddNumberParameter("TotalCostImport", "TotCIm", "Total cost import (earth_total_cost_import)", GH_ParamAccess.item);
            pManager[22].Optional = true;
            // 23
            pManager.AddNumberParameter("TotalCostExport", "TotCEx", "Total cost export (earth_total_cost_export)", GH_ParamAccess.item);
            pManager[23].Optional = true;

            // ── Retention ───────────────────────────────────────────────────────
            // 24
            pManager.AddNumberParameter("RetHMin", "RetMin", "Retention height min (retention_height_min)", GH_ParamAccess.item);
            pManager[24].Optional = true;
            // 25
            pManager.AddNumberParameter("RetHMax", "RetMax", "Retention height max (retention_height_max)", GH_ParamAccess.item);
            pManager[25].Optional = true;
            // 26
            pManager.AddNumberParameter("RetArea", "RetA", "Retention area (retention_area)", GH_ParamAccess.item);
            pManager[26].Optional = true;
            // 27
            pManager.AddNumberParameter("RetCost", "RetC", "Retention cost (retention_cost)", GH_ParamAccess.item);
            pManager[27].Optional = true;

            // ── Custom ──────────────────────────────────────────────────────────
            // 28
            pManager.AddTextParameter("Properties", "Props", "Custom properties as JSON string (properties)", GH_ParamAccess.item);
            pManager[28].Optional = true;

            // 29 - Upload trigger (MUST be last)
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();

            // Access
            if (TryGetText(DA, 1, out string accCurvesId))
                values["access_curve_set_id"] = accCurvesId;
            if (TryGetText(DA, 2, out string accLabelsId))
                values["access_text_3d_set_id"] = accLabelsId;
            if (TryGetNumber(DA, 3, out double accRef))
                values["access_ref"] = accRef;
            if (TryGetNumber(DA, 4, out double accRate))
                values["access_rate"] = accRate;

            // Earth Terrain
            if (TryGetText(DA, 5, out string terrMeshId))
                values["earth_mesh_terrain_id"] = terrMeshId;
            if (TryGetNumber(DA, 6, out double terrVolCompFill))
                values["earth_terrain_vol_compact_fill"] = terrVolCompFill;
            if (TryGetNumber(DA, 7, out double terrVolBulkFill))
                values["earth_terrain_vol_bulking_fill"] = terrVolBulkFill;
            if (TryGetNumber(DA, 8, out double terrVolCut))
                values["earth_terrain_vol_cut"] = terrVolCut;
            if (TryGetNumber(DA, 9, out double terrVolImport))
                values["earth_terrain_vol_import"] = terrVolImport;
            if (TryGetNumber(DA, 10, out double terrVolExport))
                values["earth_terrain_vol_export"] = terrVolExport;

            // Earth Lots
            if (TryGetText(DA, 11, out string lotsMeshId))
                values["earth_mesh_lots_id"] = lotsMeshId;
            if (TryGetNumber(DA, 12, out double lotsVolCompFill))
                values["earth_lots_vol_compact_fill"] = lotsVolCompFill;
            if (TryGetNumber(DA, 13, out double lotsVolBulkFill))
                values["earth_lots_vol_bulking_fill"] = lotsVolBulkFill;
            if (TryGetNumber(DA, 14, out double lotsVolCut))
                values["earth_lots_vol_cut"] = lotsVolCut;
            if (TryGetNumber(DA, 15, out double lotsVolImport))
                values["earth_lots_vol_import"] = lotsVolImport;
            if (TryGetNumber(DA, 16, out double lotsVolExport))
                values["earth_lots_vol_export"] = lotsVolExport;

            // Earth Total
            if (TryGetNumber(DA, 17, out double totalVolCompFill))
                values["earth_total_vol_compact_fill"] = totalVolCompFill;
            if (TryGetNumber(DA, 18, out double totalVolBulkFill))
                values["earth_total_vol_bulking_fill"] = totalVolBulkFill;
            if (TryGetNumber(DA, 19, out double totalVolCut))
                values["earth_total_vol_cut"] = totalVolCut;
            if (TryGetNumber(DA, 20, out double totalVolImport))
                values["earth_total_vol_import"] = totalVolImport;
            if (TryGetNumber(DA, 21, out double totalVolExport))
                values["earth_total_vol_export"] = totalVolExport;
            if (TryGetNumber(DA, 22, out double totalCostImport))
                values["earth_total_cost_import"] = totalCostImport;
            if (TryGetNumber(DA, 23, out double totalCostExport))
                values["earth_total_cost_export"] = totalCostExport;

            // Retention
            if (TryGetNumber(DA, 24, out double retHMin))
                values["retention_height_min"] = retHMin;
            if (TryGetNumber(DA, 25, out double retHMax))
                values["retention_height_max"] = retHMax;
            if (TryGetNumber(DA, 26, out double retArea))
                values["retention_area"] = retArea;
            if (TryGetNumber(DA, 27, out double retCost))
                values["retention_cost"] = retCost;

            // Custom properties (JSON)
            if (TryGetJson(DA, 28, out JsonElement properties))
                values["properties"] = properties;

            return values;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Optimizations");
    }
}
