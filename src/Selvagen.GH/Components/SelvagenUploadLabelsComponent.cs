using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadLabelsComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadLabelsComponent()
            : base("Upload Labels", "SvUpLbl",
                "Upload text labels from Rhino to the platform.")
        { }

        public override Guid ComponentGuid => new Guid("f5a6b7c8-d9e0-1234-5678-90abcdef1234");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Target project ID", GH_ParamAccess.item);
            pManager.AddPointParameter("Points", "P", "Label positions", GH_ParamAccess.list);
            pManager.AddTextParameter("Texts", "T", "Label text strings", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the label set", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("TextSetID", "ID", "ID of the created text set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var points = new List<Point3d>();
            var texts = new List<string>();
            bool upload = false;

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, points);
            DA.GetDataList(2, texts);
            DA.GetData(3, ref name);
            DA.GetData(4, ref upload);

            var client = SessionManager.Current;

            if (!upload || client == null || points.Count == 0)
            {
                if (client == null && upload)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetWaiting(DA);
                return;
            }

            try
            {
                var textSet = TextConverter.FromPointsAndTexts(points, texts);
                var result = Task.Run(() => client.UploadText3DAsync(projectId, name, textSet)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({points.Count} labels)");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, ex);
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadLabels");
    }
}
