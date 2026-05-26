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
        private string _cachedName;

        public SelvagenDownloadLabelsComponent()
            : base("Download Labels", "SvDnLbl",
                "Download a label set from the platform as Rhino planes and text. [Download de Rótulos 3D]")
        { }

        public override Guid ComponentGuid => new Guid("7c9ef67a-f96d-4107-a424-2b3e393a3982");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Asset ID", "AstID", "Text 3D set asset ID (from List Assets)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPlaneParameter("Planes", "Pl", "Label placement planes (position + rotation)", GH_ParamAccess.list);
            pManager.AddTextParameter("Texts", "Txt", "Label text strings", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "Per-label color", GH_ParamAccess.list);
            pManager.AddNumberParameter("Font Sizes", "Fs", "Per-label font size", GH_ParamAccess.list);
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
                DA.SetData(5, "Provide an Asset ID.");
                return;
            }

            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(5, "Not logged in.");
                return;
            }

            if (assetId == _cachedId && _cachedPlanes != null)
            {
                DA.SetDataList(0, _cachedPlanes);
                DA.SetDataList(1, _cachedTexts);
                DA.SetDataList(2, _cachedColors);
                DA.SetDataList(3, _cachedFontSizes);
                DA.SetData(4, _cachedName);
                DA.SetData(5, $"Cached: {_cachedName} ({_cachedPlanes.Count} labels)");
                return;
            }

            try
            {
                var asset = Task.Run(() => client.GetText3DSetAsync(assetId)).GetAwaiter().GetResult();

                if (asset.TextData == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Label set has no inline text data.");
                    DA.SetData(5, "No text data.");
                    return;
                }

                TextConverter.FromText3DSet(asset.TextData,
                    out var planes, out var texts, out var colors, out var fontSizes);

                _cachedId = assetId;
                _cachedPlanes = planes;
                _cachedTexts = texts;
                _cachedColors = colors;
                _cachedFontSizes = fontSizes;
                _cachedName = asset.Name;

                DA.SetDataList(0, planes);
                DA.SetDataList(1, texts);
                DA.SetDataList(2, colors);
                DA.SetDataList(3, fontSizes);
                DA.SetData(4, asset.Name);
                DA.SetData(5, $"Downloaded: {asset.Name} ({planes.Count} labels)");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(5, $"Error: {msg}");
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("DownloadLabels");
    }
}
