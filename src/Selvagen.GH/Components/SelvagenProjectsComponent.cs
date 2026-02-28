using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

namespace Selvagen.GH.Components
{
    public class SelvagenProjectsComponent : GH_Component
    {
        public SelvagenProjectsComponent()
            : base("Selvagen Projects", "SvProjects",
                "List projects from the Selvagen platform.",
                "Selvagen", "Data")
        { }

        public override Guid ComponentGuid => new Guid("c2d3e4f5-a6b7-8901-2345-67890abcdef1");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Client", "C", "Authenticated Selvagen client", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Refresh", "R", "Set to true to fetch projects", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("IDs", "ID", "Project IDs", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Project names", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object clientObj = null;
            bool refresh = false;

            DA.GetData(0, ref clientObj);
            DA.GetData(1, ref refresh);

            if (!refresh || !(clientObj is SelvagenClient client))
            {
                DA.SetDataList(0, new List<string>());
                DA.SetDataList(1, new List<string>());
                return;
            }

            try
            {
                var projects = Task.Run(() => client.ListProjectsAsync()).GetAwaiter().GetResult();

                DA.SetDataList(0, projects.Select(p => p.Id).ToList());
                DA.SetDataList(1, projects.Select(p => p.Name).ToList());
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.InnerException?.Message ?? ex.Message);
                DA.SetDataList(0, new List<string>());
                DA.SetDataList(1, new List<string>());
            }
        }
    }
}
