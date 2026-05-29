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

        private volatile bool _isFetching;
        private CurveSetAssetFull _pendingAsset;
        private string _pendingId;
        private string _fetchError;
        private readonly object _lock = new object();

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

            if (string.IsNullOrEmpty(assetId)) { DA.SetData(4, "Provide an Asset ID."); return; }
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(4, "Not logged in.");
                return;
            }

            // 1. A finished fetch waiting? Build geometry on the solver thread, cache, emit.
            CurveSetAssetFull pending; string pendingId; string err;
            lock (_lock) { pending = _pendingAsset; pendingId = _pendingId; _pendingAsset = null; err = _fetchError; _fetchError = null; }
            if (err != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                DA.SetData(4, $"Error: {err}");
                return;
            }
            if (pending != null && pendingId == assetId)
            {
                if (pending.GeometryData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Curve set has no inline geometry data.");
                    DA.SetData(4, "No geometry data.");
                    return;
                }

                CurveConverter.FromCurveSet(pending.GeometryData,
                    out var curves, out var colors, out var linewidths);

                _cachedId = assetId;
                _cachedCurves = curves;
                _cachedColors = colors;
                _cachedLinewidths = linewidths;
                _cachedName = pending.Name;

                DA.SetDataList(0, curves);
                DA.SetDataList(1, colors);
                DA.SetDataList(2, linewidths);
                DA.SetData(3, pending.Name);
                DA.SetData(4, $"Downloaded: {pending.Name} ({curves.Count} curves)");
                return;
            }

            // 2. Cached for this id?
            if (assetId == _cachedId && _cachedCurves != null)
            {
                DA.SetDataList(0, _cachedCurves);
                DA.SetDataList(1, _cachedColors);
                DA.SetDataList(2, _cachedLinewidths);
                DA.SetData(3, _cachedName);
                DA.SetData(4, $"Cached: {_cachedName} ({_cachedCurves.Count} curves)");
                return;
            }

            // 3. In-flight already?
            if (_isFetching) { DA.SetData(4, "Downloading..."); return; }

            // 4. Start the fetch (network only; geometry is built on the re-solve above).
            _isFetching = true;
            var capturedId = assetId;
            Task.Run(async () =>
            {
                try
                {
                    var a = await client.GetCurveSetAsync(capturedId).ConfigureAwait(false);
                    lock (_lock) { _pendingAsset = a; _pendingId = capturedId; }
                }
                catch (Exception ex)
                {
                    lock (_lock) { _fetchError = ex.Unwrap().Message; }
                }
                finally
                {
                    _isFetching = false;
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        if (OnPingDocument() != null) ExpireSolution(true);
                    }));
                }
            });
            DA.SetData(4, "Downloading...");
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadCurves");
    }
}
