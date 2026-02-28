using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

namespace Selvagen.GH.Components
{
    public class SelvagenLoginComponent : GH_Component
    {
        private SelvagenClient _client;
        private string _statusMessage = "Not connected";

        public SelvagenLoginComponent()
            : base("Selvagen Login", "SvLogin",
                "Authenticate with the Selvagen platform using email and password.",
                "Selvagen", "Auth")
        { }

        public override Guid ComponentGuid => new Guid("b1c2d3e4-f5a6-7890-1234-567890abcdef");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "U", "Supabase project URL", GH_ParamAccess.item);
            pManager.AddTextParameter("Key", "K", "Supabase anon key", GH_ParamAccess.item);
            pManager.AddTextParameter("Email", "E", "User email address", GH_ParamAccess.item);
            pManager.AddTextParameter("Password", "P", "User password", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Login", "L", "Set to true to login", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Client", "C", "Authenticated Selvagen client", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Connection status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string url = "", key = "", email = "", password = "";
            bool login = false;

            DA.GetData(0, ref url);
            DA.GetData(1, ref key);
            DA.GetData(2, ref email);
            DA.GetData(3, ref password);
            DA.GetData(4, ref login);

            if (!login)
            {
                DA.SetData(0, _client);
                DA.SetData(1, _statusMessage);
                return;
            }

            try
            {
                _client?.Dispose();
                _client = new SelvagenClient(url, key);
                var result = Task.Run(() => _client.LoginAsync(email, password)).GetAwaiter().GetResult();
                _statusMessage = $"Logged in as {result.User?.Email ?? email}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error: {ex.InnerException?.Message ?? ex.Message}";
                _client = null;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, _statusMessage);
            }

            DA.SetData(0, _client);
            DA.SetData(1, _statusMessage);
        }
    }
}
