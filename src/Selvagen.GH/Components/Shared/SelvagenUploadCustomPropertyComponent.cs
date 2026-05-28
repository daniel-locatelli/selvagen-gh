using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadCustomPropertyComponent : SelvagenActionComponentBase, IInlineTypeDropdown
    {
        private static readonly string[] TypeOptions = { "text", "number", "boolean" };
        private string _selectedType = "text";

        public SelvagenUploadCustomPropertyComponent()
            : base("Upload Custom Property", "SvUpProp",
                   "Upload typed key/value pairs to a project's custom properties. " +
                   "Pick a type (text/number/boolean), wire matching-length Key and Value lists. " +
                   "[Subir Propriedade Personalizada]",
                   "07 Shared") { }

        public override Guid ComponentGuid => new Guid("A1000006-0001-4000-8000-000000000001");
        protected override Bitmap Icon => IconLoader.Load("UploadCustomProperty");

        // ── Action-button surface (grayscale, matches Upload aesthetic) ────
        public override string ActionLabel        => "Upload";
        public override string ActionLabelRunning => "Uploading...";
        public override Color  ButtonGradientTop    => Color.FromArgb(130, 130, 130);
        public override Color  ButtonGradientBottom => Color.FromArgb(50, 50, 50);

        // ── Inline type dropdown ───────────────────────────────────────────
        public string[] DropdownOptions => TypeOptions;
        public string DropdownSelected
        {
            get => _selectedType;
            set
            {
                if (_selectedType == value || Array.IndexOf(TypeOptions, value) < 0) return;
                _selectedType = value;
                Message = _selectedType;
                ExpireSolution(true);
            }
        }

        // ── Params ─────────────────────────────────────────────────────────
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("Key", "K", "Property keys (snake_case)", GH_ParamAccess.list);
            pManager.AddTextParameter("Value", "V", "Property values (same length as Keys)", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Preview", "Prev", "Per-pair preview as 'key = value' strings", GH_ParamAccess.list);
            pManager.AddTextParameter("Status",  "S",    "Operation status",                          GH_ParamAccess.item);
            pManager.AddTextParameter("Record IDs", "IDs", "UUIDs of upserted rows",                  GH_ParamAccess.list);
        }

        // ── SolveInstance ──────────────────────────────────────────────────
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "";
            var keys   = new List<string>();
            var values = new List<string>();
            DA.GetData(0, ref projectId);
            DA.GetDataList(1, keys);
            DA.GetDataList(2, values);

            // Preview is always emitted, regardless of click state
            var pairCount = Math.Min(keys.Count, values.Count);
            var preview = new List<string>(pairCount);
            for (int i = 0; i < pairCount; i++)
                preview.Add($"{keys[i]} = {values[i]}");
            DA.SetDataList(0, preview);

            var client = SessionManager.Current;

            // Idle: emit Ready, return
            if (!ActionRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(1, "Ready to upload.");
                return;
            }

            // Action requested: validate aggressively before any network call
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetData(1, "Not logged in.");
                return;
            }
            if (string.IsNullOrEmpty(projectId))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Project ID is required.");
                DA.SetData(1, "Missing Project ID");
                return;
            }
            if (keys.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Nothing to upload.");
                DA.SetData(1, "Nothing to upload.");
                return;
            }
            if (keys.Count != values.Count)
            {
                var msg = $"List length mismatch: Keys={keys.Count}, Values={values.Count}. All lists must be the same length.";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(1, msg);
                return;
            }

            // Key validation — collect ALL invalid keys before failing, with deduplicated suggestions
            var invalidIndices = new List<int>();
            var invalidRaws    = new List<string>();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!CustomPropertyKeyValidator.IsValid(keys[i]?.Trim()))
                {
                    invalidIndices.Add(i);
                    invalidRaws.Add(keys[i]);
                }
            }
            if (invalidIndices.Count > 0)
            {
                var suggestions = CustomPropertyKeyValidator.SuggestBatch(invalidRaws);
                var lines = new List<string>(invalidIndices.Count);
                for (int j = 0; j < invalidIndices.Count; j++)
                    lines.Add($"  '{invalidRaws[j]}' → did you mean: {suggestions[j]}");
                var msg = "Invalid key(s). Must be snake_case (lowercase, digits, underscores, start with letter):\n"
                          + string.Join("\n", lines);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                DA.SetData(1, "Invalid key(s) — see runtime message");
                return;
            }

            // Value validation per active type
            for (int i = 0; i < values.Count; i++)
            {
                if (!IsValueValidForType(values[i], _selectedType, out var typeError))
                {
                    var msg = $"Value '{values[i]}' is not a valid {_selectedType} (key: {keys[i]}). {typeError}";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
                    DA.SetData(1, "Invalid value — see runtime message");
                    return;
                }
            }

            // Build payload + send
            var payload = new CustomPropertyUpsert[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                payload[i] = new CustomPropertyUpsert
                {
                    ProjectId = projectId,
                    Key       = keys[i].Trim(),
                    Value     = NormalizeValueForType(values[i], _selectedType),
                    ValueType = _selectedType,
                };
            }

            try
            {
                IsRunning = true;
                ForceCanvasRefresh();

                var results = Task.Run(() => client.UpsertCustomPropertiesAsync(projectId, payload))
                                  .GetAwaiter().GetResult();

                var ids = new List<string>(results.Length);
                foreach (var r in results) ids.Add(r.Id);

                DA.SetData(1, $"Upserted {results.Length} properties");
                DA.SetDataList(2, ids);
            }
            catch (Exception ex)
            {
                SetActionError(DA, 1, ex);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private static bool IsValueValidForType(string raw, string type, out string error)
        {
            error = null;
            if (raw == null) raw = "";
            switch (type)
            {
                case "text":
                    return true;
                case "number":
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return true;
                    error = "Use a decimal number with '.' as separator (e.g. 6.4, -0.5, 1e10).";
                    return false;
                case "boolean":
                    if (bool.TryParse(raw, out _)) return true;
                    error = "Use 'true' or 'false'.";
                    return false;
                default:
                    error = $"Unknown type: {type}";
                    return false;
            }
        }

        private static string NormalizeValueForType(string raw, string type)
        {
            switch (type)
            {
                case "boolean":
                    // Canonicalize to lowercase to satisfy the DB CHECK ('true'|'false')
                    return bool.Parse(raw) ? "true" : "false";
                default:
                    return raw ?? "";
            }
        }

        // ── Persistence ────────────────────────────────────────────────────
        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("SelectedType", _selectedType);
            return base.Write(writer);
        }
        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SelectedType"))
                _selectedType = reader.GetString("SelectedType");
            Message = _selectedType;
            return base.Read(reader);
        }
    }
}
