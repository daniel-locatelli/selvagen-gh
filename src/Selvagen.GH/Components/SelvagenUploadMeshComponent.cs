using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadMeshComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadMeshComponent()
            : base("Upload Mesh", "SvUpMesh",
                "Upload a Rhino mesh to the platform.")
        { }

        public override Guid ComponentGuid => new Guid("d3e4f5a6-b7c8-9012-3456-7890abcdef12");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Target project ID", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Rhino mesh to upload", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Display name for the mesh", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("MeshID", "ID", "ID of the created mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            Mesh mesh = null;
            bool upload = false;

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref mesh);
            DA.GetData(2, ref name);
            DA.GetData(3, ref upload);

            var client = SessionManager.Current;

            if (!upload || client == null || mesh == null)
            {
                if (client == null && upload)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetWaiting(DA);
                return;
            }

            try
            {
                var geometry = MeshConverter.ToBufferGeometry(mesh);
                var result = Task.Run(() => client.UploadMeshAsync(projectId, name, geometry)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name}");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, ex);
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadMesh");
    }
}
