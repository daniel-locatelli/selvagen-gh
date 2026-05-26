using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenProjectsComponent : SelvagenSelectableComponentBase<ProjectInfo>
    {
        public SelvagenProjectsComponent()
            : base("List Projects", "SvProjects",
                "List projects from the platform. Optionally filter by Client ID; pick one from the inline dropdown. [Listar Projetos]",
                "02 Admin")
        { }

        protected override string SelectedIdLabel => "Project ID";
        protected override string SelectedIdNick => "PrjID";
        protected override string SelectedNameLabel => "Project Name";
        protected override string SelectedNameNick => "PrjName";

        public override Guid ComponentGuid => new Guid("c2d3e4f5-a6b7-8901-2345-67890abcdef1");

        protected override void RegisterFilterInputs(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Client ID", "CltID", "Optional client filter", GH_ParamAccess.item, "");
            pManager[0].Optional = true;
        }

        protected override object[] CaptureInputs(IGH_DataAccess da)
        {
            string clientId = "";
            da.GetData(0, ref clientId);
            return new object[] { clientId ?? "" };
        }

        protected override Task<ProjectInfo[]> FetchAsync(SelvagenClient client, object[] inputs)
        {
            string clientId = (string)inputs[0];
            return string.IsNullOrEmpty(clientId)
                ? client.ListProjectsAsync()
                : client.ListProjectsByClientAsync(clientId);
        }

        protected override string GetId(ProjectInfo item) => item.Id;
        protected override string GetDisplayName(ProjectInfo item) => item.Name;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Projects");
    }
}
