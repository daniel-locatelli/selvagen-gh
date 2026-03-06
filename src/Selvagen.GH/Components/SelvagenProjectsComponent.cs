using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenProjectsComponent : GH_Component
    {
        private ProjectInfo[] _cachedProjects;
        private bool _refreshWasTrue;

        public SelvagenProjectsComponent()
            : base("List Projects", "SvProjects",
                "List projects from the platform.",
                "Selvagen", "Data")
        { }

        public override Guid ComponentGuid => new Guid("c2d3e4f5-a6b7-8901-2345-67890abcdef1");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ClientID", "Id", "Optional client filter", GH_ParamAccess.item, "");
            pManager[0].Optional = true;
            pManager.AddBooleanParameter("Refresh", "R", "Force a re-fetch", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("IDs", "ID", "Project IDs", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Project names", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string clientId = "";
            bool refresh = false;

            DA.GetData(0, ref clientId);
            DA.GetData(1, ref refresh);

            var client = SessionManager.Current;
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetDataList(0, new List<string>());
                DA.SetDataList(1, new List<string>());
                return;
            }

            bool needsFetch = _cachedProjects == null || (refresh && !_refreshWasTrue);
            _refreshWasTrue = refresh;

            if (needsFetch)
            {
                try
                {
                    PluginLogger.Log("SelvagenProjectsComponent: Fetching projects...");
                    if (string.IsNullOrEmpty(clientId))
                        _cachedProjects = Task.Run(() => client.ListProjectsAsync()).GetAwaiter().GetResult();
                    else
                        _cachedProjects = Task.Run(() => client.ListProjectsByClientAsync(clientId)).GetAwaiter().GetResult();
                    PluginLogger.Log($"SelvagenProjectsComponent: Found {_cachedProjects.Length} projects.");
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.InnerException?.Message ?? ex.Message;
                    PluginLogger.Log($"SelvagenProjectsComponent Error: {errorMsg}");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMsg);
                    DA.SetDataList(0, new List<string>());
                    DA.SetDataList(1, new List<string>());
                    return;
                }
            }

            DA.SetDataList(0, _cachedProjects.Select(p => p.Id).ToList());
            DA.SetDataList(1, _cachedProjects.Select(p => p.Name).ToList());
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Projects");
    }
}
