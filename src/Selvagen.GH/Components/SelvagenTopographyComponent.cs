using System;
using System.Collections.Generic;
using System.Text.Json;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class SelvagenTopographyComponent : SelvagenModuleComponentBase
    {
        public SelvagenTopographyComponent()
            : base("Topography", "SvTopo",
                   "Upload topography module data for a project. Creates the record if needed.") { }

        protected override string ModuleTable => "topography";

        public override Guid ComponentGuid => new Guid("D1A2B3C4-E5F6-4890-ABCD-1234FACE0001");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // 0 - ProjectID (required)
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);

            // ── Base ────────────────────────────────────────────────────────
            // 1
            pManager.AddTextParameter("BaseMeshID", "BM", "Base mesh asset ID (base_mesh_id)", GH_ParamAccess.item);
            // 2
            pManager.AddNumberParameter("BaseArea2D", "BA2", "Base 2D area (base_area_2d)", GH_ParamAccess.item);
            // 3
            pManager.AddNumberParameter("BaseArea3D", "BA3", "Base 3D area (base_area_3d)", GH_ParamAccess.item);
            // 4
            pManager.AddNumberParameter("BaseTDR", "BTDR", "Base true dimension rate (base_true_dimension_rate)", GH_ParamAccess.item);

            // ── Contours ────────────────────────────────────────────────────
            // 5
            pManager.AddTextParameter("OutlineCurvesID", "OC", "Outline curve set asset ID (outline_curve_set_id)", GH_ParamAccess.item);
            // 6
            pManager.AddTextParameter("ContoursCurvesID", "CC", "Contours curve set asset ID (contours_curve_set_id)", GH_ParamAccess.item);
            // 7
            pManager.AddTextParameter("ContoursLabelsID", "CL", "Contours text 3D set asset ID (contours_text_3d_set_id)", GH_ParamAccess.item);
            // 8
            pManager.AddNumberParameter("ContourInterval", "CI", "Contour interval (contour_interval)", GH_ParamAccess.item);

            // ── Urbanization ────────────────────────────────────────────────
            // 9
            pManager.AddTextParameter("UrbanCurvesID", "UC", "Urbanization curve set asset ID (urbanization_curve_set_id)", GH_ParamAccess.item);

            // ── Elevation ───────────────────────────────────────────────────
            // 10
            pManager.AddTextParameter("ElevMeshID", "EM", "Elevation mesh asset ID (elevation_mesh_id)", GH_ParamAccess.item);
            // 11
            pManager.AddTextParameter("ElevCurvesID", "EC", "Elevation curve set asset ID (elevation_curve_set_id)", GH_ParamAccess.item);
            // 12
            pManager.AddNumberParameter("ElevMin", "Emn", "Minimum elevation (elevation_min)", GH_ParamAccess.item);
            // 13
            pManager.AddNumberParameter("ElevMax", "Emx", "Maximum elevation (elevation_max)", GH_ParamAccess.item);

            // ── Slope ───────────────────────────────────────────────────────
            // 14
            pManager.AddTextParameter("SlopeMeshID", "SM", "Slope mesh asset ID (slope_mesh_id)", GH_ParamAccess.item);
            // 15
            pManager.AddNumberParameter("SlopeRef", "SR", "Slope reference (slope_ref)", GH_ParamAccess.item);
            // 16
            pManager.AddNumberParameter("SlopeRestRate", "SRR", "Slope restricted area rate (slope_restricted_area_rate)", GH_ParamAccess.item);
            // 17
            pManager.AddNumberParameter("SlopeMin", "Smn", "Minimum slope (slope_min)", GH_ParamAccess.item);
            // 18
            pManager.AddNumberParameter("SlopeMax", "Smx", "Maximum slope (slope_max)", GH_ParamAccess.item);

            // ── Access 8 ────────────────────────────────────────────────────
            // 19
            pManager.AddTextParameter("Acc8MeshID", "A8M", "Access 8 mesh asset ID (access8_mesh_id)", GH_ParamAccess.item);
            // 20
            pManager.AddNumberParameter("Acc8Ref", "A8R", "Access 8 reference (access8_ref)", GH_ParamAccess.item);
            // 21
            pManager.AddNumberParameter("Acc8Rate", "A8%", "Access 8 rate (access8_rate)", GH_ParamAccess.item);

            // ── Access 5 ────────────────────────────────────────────────────
            // 22
            pManager.AddTextParameter("Acc5MeshID", "A5M", "Access 5 mesh asset ID (access5_mesh_id)", GH_ParamAccess.item);
            // 23
            pManager.AddNumberParameter("Acc5Ref", "A5R", "Access 5 reference (access5_ref)", GH_ParamAccess.item);
            // 24
            pManager.AddNumberParameter("Acc5Rate", "A5%", "Access 5 rate (access5_rate)", GH_ParamAccess.item);

            // ── Drainage ────────────────────────────────────────────────────
            // 25
            pManager.AddTextParameter("DrainCurvesID", "DC", "Drainage curve set asset ID (drainage_curve_set_id)", GH_ParamAccess.item);
            // 26
            pManager.AddIntegerParameter("DrainFlowPaths", "DFP", "Total drainage flow paths (drainage_total_flow_paths)", GH_ParamAccess.item);
            // 27
            pManager.AddNumberParameter("DrainConcRate", "DCR", "Drainage concentration rate (drainage_concentration_rate)", GH_ParamAccess.item);

            // ── Custom ──────────────────────────────────────────────────────
            // 28
            pManager.AddTextParameter("Properties", "Props", "Custom properties as JSON string (properties)", GH_ParamAccess.item);

            // ── Upload trigger (must be last) ───────────────────────────────
            // 29
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to execute upload", GH_ParamAccess.item, false);

            // Mark all data inputs as optional (indices 1 through 28)
            for (int i = 1; i <= 28; i++)
            {
                pManager[i].Optional = true;
            }
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();

            // ── Base ────────────────────────────────────────────────────────
            if (TryGetText(DA, 1, out var baseMeshId))
                values["base_mesh_id"] = baseMeshId;
            if (TryGetNumber(DA, 2, out var baseArea2d))
                values["base_area_2d"] = baseArea2d;
            if (TryGetNumber(DA, 3, out var baseArea3d))
                values["base_area_3d"] = baseArea3d;
            if (TryGetNumber(DA, 4, out var baseTdr))
                values["base_true_dimension_rate"] = baseTdr;

            // ── Contours ────────────────────────────────────────────────────
            if (TryGetText(DA, 5, out var outlineCurvesId))
                values["outline_curve_set_id"] = outlineCurvesId;
            if (TryGetText(DA, 6, out var contoursCurvesId))
                values["contours_curve_set_id"] = contoursCurvesId;
            if (TryGetText(DA, 7, out var contoursLabelsId))
                values["contours_text_3d_set_id"] = contoursLabelsId;
            if (TryGetNumber(DA, 8, out var contourInterval))
                values["contour_interval"] = contourInterval;

            // ── Urbanization ────────────────────────────────────────────────
            if (TryGetText(DA, 9, out var urbanCurvesId))
                values["urbanization_curve_set_id"] = urbanCurvesId;

            // ── Elevation ───────────────────────────────────────────────────
            if (TryGetText(DA, 10, out var elevMeshId))
                values["elevation_mesh_id"] = elevMeshId;
            if (TryGetText(DA, 11, out var elevCurvesId))
                values["elevation_curve_set_id"] = elevCurvesId;
            if (TryGetNumber(DA, 12, out var elevMin))
                values["elevation_min"] = elevMin;
            if (TryGetNumber(DA, 13, out var elevMax))
                values["elevation_max"] = elevMax;

            // ── Slope ───────────────────────────────────────────────────────
            if (TryGetText(DA, 14, out var slopeMeshId))
                values["slope_mesh_id"] = slopeMeshId;
            if (TryGetNumber(DA, 15, out var slopeRef))
                values["slope_ref"] = slopeRef;
            if (TryGetNumber(DA, 16, out var slopeRestRate))
                values["slope_restricted_area_rate"] = slopeRestRate;
            if (TryGetNumber(DA, 17, out var slopeMin))
                values["slope_min"] = slopeMin;
            if (TryGetNumber(DA, 18, out var slopeMax))
                values["slope_max"] = slopeMax;

            // ── Access 8 ────────────────────────────────────────────────────
            if (TryGetText(DA, 19, out var acc8MeshId))
                values["access8_mesh_id"] = acc8MeshId;
            if (TryGetNumber(DA, 20, out var acc8Ref))
                values["access8_ref"] = acc8Ref;
            if (TryGetNumber(DA, 21, out var acc8Rate))
                values["access8_rate"] = acc8Rate;

            // ── Access 5 ────────────────────────────────────────────────────
            if (TryGetText(DA, 22, out var acc5MeshId))
                values["access5_mesh_id"] = acc5MeshId;
            if (TryGetNumber(DA, 23, out var acc5Ref))
                values["access5_ref"] = acc5Ref;
            if (TryGetNumber(DA, 24, out var acc5Rate))
                values["access5_rate"] = acc5Rate;

            // ── Drainage ────────────────────────────────────────────────────
            if (TryGetText(DA, 25, out var drainCurvesId))
                values["drainage_curve_set_id"] = drainCurvesId;
            if (TryGetInt(DA, 26, out var drainFlowPaths))
                values["drainage_total_flow_paths"] = drainFlowPaths;
            if (TryGetNumber(DA, 27, out var drainConcRate))
                values["drainage_concentration_rate"] = drainConcRate;

            // ── Custom ──────────────────────────────────────────────────────
            if (TryGetJson(DA, 28, out var properties))
                values["properties"] = properties;

            return values;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Topography");
    }
}
