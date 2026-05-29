using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Base class for per-module components (Topography, Geology, Analyses, Optimizations).
    /// Handles create-or-update logic: finds or creates the module record, then PATCHes all provided values.
    /// Inherits the in-canvas Upload button from SelvagenUploadComponentBase.
    /// </summary>
    public abstract class SelvagenModuleComponentBase : SelvagenUploadComponentBase
    {
        protected abstract string ModuleTable { get; }

        protected SelvagenModuleComponentBase(string name, string nickname, string description, string subcategory = "Modules")
            : base(name, nickname, description, subcategory) { }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Record ID", "RecID", "Module record ID", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Operation status", GH_ParamAccess.item);
        }

        /// <summary>
        /// Subclasses implement this to read their specific optional inputs and return
        /// a dictionary of column_name → value for the PATCH request.
        /// </summary>
        protected abstract Dictionary<string, object> CollectValues(IGH_DataAccess DA);

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (TryFinishAsync<(string RecordId, bool Created, int FieldCount)>(DA, 1, (da, r) =>
                {
                    da.SetData(0, r.RecordId);
                    da.SetData(1, $"{(r.Created ? "Created" : "Updated")}: {ModuleTable} ({r.FieldCount} fields)");
                }))
                return;

            string projectId = "";
            DA.GetData(0, ref projectId);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (IsRunningAsync) { DA.SetData(1, "Saving..."); return; }
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

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

            var values = CollectValues(DA); // solver thread — reads inputs
            StartAsync(async () =>
            {
                var existing = await client.ListModuleRecordsAsync(ModuleTable, projectId).ConfigureAwait(false);
                string recordId; bool created = false;
                if (existing != null && existing.Length > 0) recordId = existing[0].Id;
                else { var rec = await client.CreateModuleRecordAsync(ModuleTable, projectId).ConfigureAwait(false); recordId = rec.Id; created = true; }
                if (values.Count > 0)
                {
                    PluginLogger.Log($"{GetType().Name}: PATCHing {values.Count} fields on {ModuleTable}/{recordId}...");
                    await client.UpdateModuleAsync(ModuleTable, recordId, values).ConfigureAwait(false);
                }
                return (recordId, created, values.Count);
            });
            DA.SetData(1, "Saving...");
        }

        // ── Helpers for reading optional inputs ────────────────────────────

        protected static bool TryGetText(IGH_DataAccess DA, int index, out string value)
        {
            value = null;
            string temp = null;
            if (DA.GetData(index, ref temp) && !string.IsNullOrEmpty(temp))
            {
                value = temp;
                return true;
            }
            return false;
        }

        protected static bool TryGetNumber(IGH_DataAccess DA, int index, out double value)
        {
            value = 0;
            return DA.GetData(index, ref value);
        }

        protected static bool TryGetInt(IGH_DataAccess DA, int index, out int value)
        {
            value = 0;
            return DA.GetData(index, ref value);
        }

        /// <summary>
        /// Read a JSON string input and parse it into a JsonElement for inclusion in the PATCH payload.
        /// </summary>
        protected static bool TryGetJson(IGH_DataAccess DA, int index, out JsonElement value)
        {
            value = default;
            string json = null;
            if (DA.GetData(index, ref json) && !string.IsNullOrEmpty(json))
            {
                value = JsonDocument.Parse(json).RootElement.Clone();
                return true;
            }
            return false;
        }
    }
}
