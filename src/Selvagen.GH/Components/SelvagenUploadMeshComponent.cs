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
                "Upload a Rhino mesh to the platform. [Upload de Malha]")
        { }

        public override Guid ComponentGuid => new Guid("d3e4f5a6-b7c8-9012-3456-7890abcdef12");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Rhino mesh to upload", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Display name for the mesh", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Mesh ID", "MshID", "ID of the created mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            Mesh mesh = null;

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref mesh);
            DA.GetData(2, ref name);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || mesh == null || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Mesh, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var geometry = MeshConverter.ToBufferGeometry(mesh);
                var result = Task.Run(() => client.UploadMeshAsync(projectId, name, geometry)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name}");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, 1, ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadMesh");
    }
}
