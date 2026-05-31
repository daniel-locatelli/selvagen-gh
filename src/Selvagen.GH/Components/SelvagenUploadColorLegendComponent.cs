using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadColorLegendComponent : SelvagenUploadComponentBase, IInlineTypeDropdown
    {
        // Title-cased for display; lowercased only when sent to the backend.
        private static readonly string[] VariantOptions = { "Gradient", "Discrete", "Stepped" };
        private string _selectedVariant = "Gradient";

        public SelvagenUploadColorLegendComponent()
            : base("Upload Color Legend", "SvUpLegend",
                "Upload a color legend to the platform. [Upload de Legenda de Cores]")
        { }

        public override Guid ComponentGuid => new Guid("f3a12ee1-3b46-4389-94a6-7564edf5b07c");

        // ── Inline type dropdown (gradient / discrete) ─────────────────────
        public string[] DropdownOptions => VariantOptions;
        public string DropdownSelected
        {
            get => _selectedVariant;
            set
            {
                if (_selectedVariant == value || Array.IndexOf(VariantOptions, value) < 0) return;
                _selectedVariant = value;
                ExpireSolution(true);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Legend type (gradient / discrete) is chosen via the inline dropdown above
            // the Upload button — see DropdownSelected — not as a wired input.
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Legend display name [Nome]", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "C", "List of colors [Cores]", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Lb", "Per-color labels (discrete/stepped) [Rótulos]", GH_ParamAccess.list);
            pManager.AddNumberParameter("Domain Min", "DMin", "Start of value range. Defaults to 0 when not connected. [Domínio Mín]", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Domain Max", "DMax", "End of value range. Defaults to 1 when not connected. [Domínio Máx]", GH_ParamAccess.item, 1.0);
            pManager.AddTextParameter("Unit", "U", "Display unit, e.g. %, °, m [Unidade]", GH_ParamAccess.item);

            pManager[3].Optional = true; // Labels
            pManager[4].Optional = true; // Domain Min
            pManager[5].Optional = true; // Domain Max
            pManager[6].Optional = true; // Unit
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Legend ID", "LgdID", "ID of the created/updated legend [ID da Legenda]", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status [Status]", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "", unit = "";
            // Domain Min/Max have parameter-level defaults (0 and 1), so DA.GetData
            // is guaranteed to populate them whether or not a wire is connected.
            double domainMin = 0, domainMax = 1;

            var colors = new List<Color>();
            var labels = new List<string>();

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref name);
            DA.GetDataList(2, colors);
            DA.GetDataList(3, labels);
            DA.GetData(4, ref domainMin);
            DA.GetData(5, ref domainMax);
            DA.GetData(6, ref unit);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || colors.Count == 0 || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Name, and at least one Color before uploading.");
                SetReady(DA, 1);
                return;
            }

            // Stepped legends render one number centered under each color band; the web
            // renderer keys its layout to `colors` and cannot repair a mismatch (missing
            // label → empty cell, extra labels → dropped). The plugin is the authoritative
            // enforcement point. Require exactly one label per color and abort otherwise —
            // never send a mismatched payload. NOTE: feed Colors/Labels as equal-length
            // flat lists; GH's longest-list padding can silently duplicate the last item
            // of the shorter list, which this check is meant to catch rather than mask.
            if (string.Equals(_selectedVariant, "Stepped", StringComparison.OrdinalIgnoreCase)
                && labels.Count != colors.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Stepped legend requires one label per color: got {colors.Count} colors and {labels.Count} labels. " +
                    "Feed Colors and Labels as equal-length flat lists. Upload aborted.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var hexColors = new string[colors.Count];
                for (int i = 0; i < colors.Count; i++)
                {
                    var c = colors[i];
                    hexColors[i] = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }

                var payload = new ColorLegendPayload
                {
                    Variant = _selectedVariant.ToLowerInvariant(),
                    Colors = hexColors,
                    Labels = labels.Count > 0 ? labels.ToArray() : null,
                    DomainMin = (float)domainMin,
                    DomainMax = (float)domainMax,
                    Unit = !string.IsNullOrEmpty(unit) ? unit : null,
                };

                var result = Task.Run(() => client.UpsertColorLegendAsync(projectId, name, payload)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name}");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, 1, ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        // ── Persistence ────────────────────────────────────────────────────
        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("SelectedVariant", _selectedVariant);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SelectedVariant"))
            {
                // Normalize to the canonical Title-cased option so definitions saved
                // when options were stored lowercase still display and match correctly.
                var stored = reader.GetString("SelectedVariant");
                foreach (var option in VariantOptions)
                {
                    if (string.Equals(option, stored, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedVariant = option;
                        break;
                    }
                }
            }
            return base.Read(reader);
        }

        protected override Bitmap Icon => IconLoader.Load("UploadColorLegend");
    }
}
