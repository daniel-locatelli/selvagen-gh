using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

namespace Selvagen.GH.Components
{
    public class SelvagenProjectModulesComponent : GH_Component
    {
        private List<ModuleRecordInfo> _cachedModules;
        private string _lastProjectId;
        private bool _refreshWasTrue;

        public SelvagenProjectModulesComponent()
            : base("Project Modules", "SvModules",
                "List available modules (Topography, Geology, etc.) for a project.",
                "Selvagen", "Data")
        { }

        public override Guid ComponentGuid => new Guid("C45E0B92-B8D3-4C2E-90AF-4E5D6C7B8F9A");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "ID", "Project ID to list modules for", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Refresh", "R", "Force a re-fetch", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("ModuleNames", "N", "Module names (e.g. topography, geology)", GH_ParamAccess.list);
            pManager.AddTextParameter("RecordIDs", "ID", "Record IDs (empty string if no record exists yet)", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Exists", "E", "Whether a record exists for each module", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "";
            bool refresh = false;

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref refresh);

            var client = SessionManager.Current;
            if (client == null || string.IsNullOrEmpty(projectId))
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                else
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "ProjectID is required.");
                return;
            }

            bool needsFetch = _cachedModules == null || projectId != _lastProjectId || (refresh && !_refreshWasTrue);
            _refreshWasTrue = refresh;
            _lastProjectId = projectId;

            if (needsFetch)
            {
                try
                {
                    PluginLogger.Log($"SelvagenProjectModulesComponent: Fetching modules for project {projectId}...");
                    _cachedModules = FetchModules(client, projectId);
                    int existing = _cachedModules.Count(m => !string.IsNullOrEmpty(m.Id));
                    PluginLogger.Log($"SelvagenProjectModulesComponent: {existing}/{_cachedModules.Count} modules have records.");
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.InnerException?.Message ?? ex.Message;
                    PluginLogger.Log($"SelvagenProjectModulesComponent Error: {errorMsg}");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMsg);
                    return;
                }
            }

            int existingCount = _cachedModules.Count(m => !string.IsNullOrEmpty(m.Id));
            if (existingCount == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No module records exist yet for this project.");

            DA.SetDataList(0, _cachedModules.Select(m => m.Name).ToList());
            DA.SetDataList(1, _cachedModules.Select(m => m.Id).ToList());
            DA.SetDataList(2, _cachedModules.Select(m => !string.IsNullOrEmpty(m.Id)).ToList());
        }

        private static readonly string[] ModuleTables = { "topography", "geology", "analyses", "optimizations" };

        private List<ModuleRecordInfo> FetchModules(SelvagenClient client, string projectId)
        {
            var modules = new List<ModuleRecordInfo>();

            foreach (var table in ModuleTables)
            {
                string recordId = "";
                try
                {
                    var records = Task.Run(() =>
                        client.ListModuleRecordsAsync(table, projectId))
                        .GetAwaiter().GetResult();

                    if (records != null && records.Length > 0)
                        recordId = records[0].Id;
                }
                catch (Exception ex)
                {
                    PluginLogger.Log($"Warning: Failed to query module {table}: {ex.Message}");
                }

                modules.Add(new ModuleRecordInfo { Name = table, Id = recordId });
            }

            return modules;
        }

        private class ModuleRecordInfo
        {
            public string Name { get; set; }
            public string Id { get; set; }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Modules");
    }
}
