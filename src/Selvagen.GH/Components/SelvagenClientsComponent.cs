using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenClientsComponent : SelvagenSelectableComponentBase<FirmInfo>
    {
        public SelvagenClientsComponent()
            : base("List Clients", "SvClients",
                "List clients of the firm. Pick one from the inline dropdown to feed downstream components. [PT: Listar Clientes]",
                "02 Admin")
        { }

        public override Guid ComponentGuid => new Guid("F23D9E81-A7C2-4B1D-8F9E-3D4C5B6A7E8F");

        protected override object[] CaptureInputs(IGH_DataAccess da) => new object[0];

        protected override Task<FirmInfo[]> FetchAsync(SelvagenClient client, object[] inputs)
            => client.ListClientsAsync();

        protected override string GetId(FirmInfo item) => item.Id;
        protected override string GetDisplayName(FirmInfo item) => item.LegalName;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Clients");
    }
}
