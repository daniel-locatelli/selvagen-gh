using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenDownloadColorLegendComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private string _cachedName;
        private string _cachedVariant;
        private List<Color> _cachedColors;
        private List<string> _cachedLabels;
        private double? _cachedDomainMin;
        private double? _cachedDomainMax;
        private string _cachedUnit;

        private volatile bool _isFetching;
        private ColorLegendInfo _pendingLegend;
        private string _pendingId;
        private string _fetchError;
        private readonly object _lock = new object();

        public SelvagenDownloadColorLegendComponent()
            : base("Download Color Legend", "SvDnLegend",
                "Download a color legend from the platform. [Download de Legenda de Cores]")
        { }

        public override Guid ComponentGuid => new Guid("e032adf7-55f5-4500-b981-41f9e4e5f59a");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Legend ID", "LgdID", "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Legend name", GH_ParamAccess.item);
            pManager.AddTextParameter("Variant", "V", "'gradient' or 'discrete'", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "C", "Color stops as Rhino colors", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Lb", "Per-color labels (discrete only)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Domain Min", "DMin", "Start of value range (gradient only)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Domain Max", "DMax", "End of value range (gradient only)", GH_ParamAccess.item);
            pManager.AddTextParameter("Unit", "U", "Display unit, e.g. %, °, m", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string legendId = "";
            DA.GetData(0, ref legendId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(legendId)) { DA.SetData(7, "Provide a Legend ID."); return; }
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(7, "Not logged in.");
                return;
            }

            // 1. A finished fetch waiting? Cache + emit on the solver thread. (No Rhino geometry to build.)
            ColorLegendInfo pending; string pendingId; string err;
            lock (_lock) { pending = _pendingLegend; pendingId = _pendingId; _pendingLegend = null; err = _fetchError; _fetchError = null; }
            if (err != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                DA.SetData(7, $"Error: {err}");
                return;
            }
            if (pending != null && pendingId == legendId)
            {
                _cachedId = legendId;
                _cachedName = pending.Name;
                _cachedVariant = pending.Variant;
                _cachedColors = HexListToColors(pending.Colors);
                _cachedLabels = pending.Labels != null ? new List<string>(pending.Labels) : new List<string>();
                _cachedDomainMin = pending.DomainMin;
                _cachedDomainMax = pending.DomainMax;
                _cachedUnit = pending.Unit;

                EmitCached(DA);
                DA.SetData(7, $"Downloaded: {pending.Name} ({pending.Variant}, {_cachedColors.Count} colors)");
                return;
            }

            // 2. Cached for this id?
            if (legendId == _cachedId && _cachedColors != null)
            {
                EmitCached(DA);
                return;
            }

            // 3. In-flight already?
            if (_isFetching) { DA.SetData(7, "Downloading..."); return; }

            // 4. Start the fetch (network only).
            _isFetching = true;
            var capturedId = legendId;
            Task.Run(async () =>
            {
                try
                {
                    var l = await client.GetColorLegendAsync(capturedId).ConfigureAwait(false);
                    lock (_lock) { _pendingLegend = l; _pendingId = capturedId; }
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
            DA.SetData(7, "Downloading...");
        }

        private void EmitCached(IGH_DataAccess DA)
        {
            DA.SetData(0, _cachedName);
            DA.SetData(1, _cachedVariant);
            DA.SetDataList(2, _cachedColors);
            DA.SetDataList(3, _cachedLabels);
            if (_cachedDomainMin.HasValue) DA.SetData(4, _cachedDomainMin.Value);
            if (_cachedDomainMax.HasValue) DA.SetData(5, _cachedDomainMax.Value);
            if (!string.IsNullOrEmpty(_cachedUnit)) DA.SetData(6, _cachedUnit);
        }

        private static List<Color> HexListToColors(string[] hexes)
        {
            var result = new List<Color>();
            if (hexes == null) return result;
            foreach (var hex in hexes)
            {
                if (TryParseHex(hex, out var color))
                    result.Add(color);
            }
            return result;
        }

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.Black;
            if (string.IsNullOrEmpty(hex)) return false;
            var s = hex.StartsWith("#") ? hex.Substring(1) : hex;
            if (s.Length != 6) return false;
            try
            {
                int r = Convert.ToInt32(s.Substring(0, 2), 16);
                int g = Convert.ToInt32(s.Substring(2, 2), 16);
                int b = Convert.ToInt32(s.Substring(4, 2), 16);
                color = Color.FromArgb(r, g, b);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override Bitmap Icon => IconLoader.Load("DownloadColorLegend");
    }
}
