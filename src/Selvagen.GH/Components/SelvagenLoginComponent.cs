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
            : base("Login", "SvLogin",
                "Authenticate with the platform using email and password.",
                "Selvagen", "Auth")
        {
            // Route all SelvagenClient (Core) logs through PluginLogger
            SelvagenClient.LogAction = PluginLogger.Log;
        }

        public override Guid ComponentGuid => new Guid("b1c2d3e4-f5a6-7890-1234-567890abcdef");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Email", "E", "User email address", GH_ParamAccess.item);
            pManager.AddTextParameter("Password", "P", "User password", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Login", "L", "Set to true to login", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Session", "S", "Authenticated API session", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "St", "Connection status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            PluginLogger.Log("Evaluating SelvagenLoginComponent SolveInstance");

            string email = "", password = "";
            bool login = false;

            DA.GetData(0, ref email);
            DA.GetData(1, ref password);
            DA.GetData(2, ref login);

            if (!login)
            {
                DA.SetData(0, _client);
                DA.SetData(1, _statusMessage);
                return;
            }

            try
            {
                _client?.Dispose();
                
                string url = SelvagenConfig.SupabaseUrl;
                string key = SelvagenConfig.SupabaseAnonKey;

                PluginLogger.Log($"Logging in to {url} with email {email}");

                _client = new SelvagenClient(url, key);
                var result = Task.Run(() => _client.LoginAsync(email, password)).GetAwaiter().GetResult();
                SessionManager.Current = _client;
                _statusMessage = $"Logged in as {result.User?.Email ?? email}";
                PluginLogger.Log($"Login successful: {_statusMessage}");
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error: {ex.InnerException?.Message ?? ex.Message}";
                PluginLogger.Log($"Login failed: {_statusMessage}");
                _client = null;
                SessionManager.Current = null;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, _statusMessage);
            }

            DA.SetData(0, _client);
            DA.SetData(1, _statusMessage);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Login");
    }
}
