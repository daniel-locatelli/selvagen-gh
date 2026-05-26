using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadCurvesComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private List<PolylineCurve> _cachedCurves;
        private List<Color> _cachedColors;
        private List<double> _cachedLinewidths;
        private string _cachedName;

        public SelvagenDownloadCurvesComponent()
            : base("Download Curves", "SvDnCrv",
                "Download a curve set from the platform as Rhino curves. [Download de Curvas]")
        { }

        public override Guid ComponentGuid => new Guid("cc3528a2-1136-4004-92fc-c2877d1d33a1");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Curve set asset ID (from List Assets) [ID do Asset de Curvas]", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "Crv", "Downloaded Rhino curves", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "Per-curve color", GH_ParamAccess.list);
            pManager.AddNumberParameter("Linewidths", "W", "Per-curve line width", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Asset name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(assetId))
            {
                DA.SetData(4, "Provide an Asset ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(4, "Not logged in.");
                return;
            }

            if (assetId == _cachedId && _cachedCurves != null)
            {
                DA.SetDataList(0, _cachedCurves);
                DA.SetDataList(1, _cachedColors);
                DA.SetDataList(2, _cachedLinewidths);
                DA.SetData(3, _cachedName);
                DA.SetData(4, $"Cached: {_cachedName} ({_cachedCurves.Count} curves)");
                return;
            }

            try
            {
                var asset = Task.Run(() => client.GetCurveSetAsync(assetId)).GetAwaiter().GetResult();

                if (asset.GeometryData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Curve set has no inline geometry data.");
                    DA.SetData(4, "No geometry data.");
                    return;
                }

                CurveConverter.FromCurveSet(asset.GeometryData,
                    out var curves, out var colors, out var linewidths);

                _cachedId = assetId;
                _cachedCurves = curves;
                _cachedColors = colors;
                _cachedLinewidths = linewidths;
                _cachedName = asset.Name;

                DA.SetDataList(0, curves);
                DA.SetDataList(1, colors);
                DA.SetDataList(2, linewidths);
                DA.SetData(3, asset.Name);
                DA.SetData(4, $"Downloaded: {asset.Name} ({curves.Count} curves)");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(4, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadCurves");
    }
}
