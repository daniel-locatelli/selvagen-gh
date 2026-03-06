using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenClientsComponent : GH_Component
    {
        private FirmInfo[] _cachedClients;
        private bool _refreshWasTrue;

        public SelvagenClientsComponent()
            : base("List Clients", "SvClients",
                "List clients of the firm.",
                "Selvagen", "Data")
        { }

        public override Guid ComponentGuid => new Guid("F23D9E81-A7C2-4B1D-8F9E-3D4C5B6A7E8F");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Refresh", "R", "Force a re-fetch", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("IDs", "ID", "Client IDs", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Client names", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool refresh = false;
            DA.GetData(0, ref refresh);

            var client = SessionManager.Current;
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                DA.SetDataList(0, new List<string>());
                DA.SetDataList(1, new List<string>());
                return;
            }

            bool needsFetch = _cachedClients == null || (refresh && !_refreshWasTrue);
            _refreshWasTrue = refresh;

            if (needsFetch)
            {
                try
                {
                    PluginLogger.Log("SelvagenClientsComponent: Fetching clients...");
                    _cachedClients = Task.Run(() => client.ListClientsAsync()).GetAwaiter().GetResult();
                    PluginLogger.Log($"SelvagenClientsComponent: Found {_cachedClients.Length} clients.");
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.InnerException?.Message ?? ex.Message;
                    PluginLogger.Log($"SelvagenClientsComponent Error: {errorMsg}");
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMsg);
                    DA.SetDataList(0, new List<string>());
                    DA.SetDataList(1, new List<string>());
                    return;
                }
            }

            DA.SetDataList(0, _cachedClients.Select(c => c.Id).ToList());
            DA.SetDataList(1, _cachedClients.Select(c => c.LegalName).ToList());
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Clients");
    }
}
