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
    public class SelvagenDownloadLabelsComponent : SelvagenDownloadComponentBase
    {
        private string _cachedId;
        private List<Plane> _cachedPlanes;
        private List<string> _cachedTexts;
        private List<Color> _cachedColors;
        private List<double> _cachedFontSizes;
        private List<int> _cachedJustifications;
        private string _cachedName;

        private volatile bool _isFetching;
        private LabelSetAssetFull _pendingAsset;
        private string _pendingId;
        private string _fetchError;
        private readonly object _lock = new object();

        public SelvagenDownloadLabelsComponent()
            : base("Download Labels", "SvDnLbl",
                "Download a label set from the platform as Rhino planes and text. [Download de Rótulos 3D]")
        { }

        public override Guid ComponentGuid => new Guid("7c9ef67a-f96d-4107-a424-2b3e393a3982");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Label set asset ID (from List Assets) [ID do Asset de Rótulos]", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Planes", "Pl", "Label placement planes (position + rotation)", GH_ParamAccess.list);
            pManager.AddTextParameter("Texts", "Txt", "Label text strings", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "Per-label color", GH_ParamAccess.list);
            pManager.AddNumberParameter("Font Sizes", "Fs", "Per-label font size", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Justification", "J", "Per-label justification (0-8)", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Asset name", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Download status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string assetId = "";
            DA.GetData(0, ref assetId);

            var client = SessionManager.Current;

            if (string.IsNullOrEmpty(assetId)) { DA.SetData(6, "Provide an Asset ID."); return; }
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(6, "Not logged in.");
                return;
            }

            // 1. A finished fetch waiting? Build geometry on the solver thread, cache, emit.
            LabelSetAssetFull pending; string pendingId; string err;
            lock (_lock) { pending = _pendingAsset; pendingId = _pendingId; _pendingAsset = null; err = _fetchError; _fetchError = null; }
            if (err != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                DA.SetData(6, $"Error: {err}");
                return;
            }
            if (pending != null && pendingId == assetId)
            {
                if (pending.TextData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Label set has no inline text data.");
                    DA.SetData(6, "No text data.");
                    return;
                }

                LabelConverter.FromLabelSet(pending.TextData,
                    out var planes, out var texts, out var colors, out var fontSizes, out var justifications);

                _cachedId = assetId;
                _cachedPlanes = planes;
                _cachedTexts = texts;
                _cachedColors = colors;
                _cachedFontSizes = fontSizes;
                _cachedJustifications = justifications;
                _cachedName = pending.Name;

                DA.SetDataList(0, planes);
                DA.SetDataList(1, texts);
                DA.SetDataList(2, colors);
                DA.SetDataList(3, fontSizes);
                DA.SetDataList(4, justifications);
                DA.SetData(5, pending.Name);
                DA.SetData(6, $"Downloaded: {pending.Name} ({planes.Count} labels)");
                return;
            }

            // 2. Cached for this id?
            if (assetId == _cachedId && _cachedPlanes != null)
            {
                DA.SetDataList(0, _cachedPlanes);
                DA.SetDataList(1, _cachedTexts);
                DA.SetDataList(2, _cachedColors);
                DA.SetDataList(3, _cachedFontSizes);
                DA.SetDataList(4, _cachedJustifications);
                DA.SetData(5, _cachedName);
                DA.SetData(6, $"Cached: {_cachedName} ({_cachedPlanes.Count} labels)");
                return;
            }

            // 3. In-flight already?
            if (_isFetching) { DA.SetData(6, "Downloading..."); return; }

            // 4. Start the fetch (network only; geometry is built on the re-solve above).
            _isFetching = true;
            var capturedId = assetId;
            Task.Run(async () =>
            {
                try
                {
                    var a = await client.GetLabelSetAsync(capturedId).ConfigureAwait(false);
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
            DA.SetData(6, "Downloading...");
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadLabels");
    }
}
