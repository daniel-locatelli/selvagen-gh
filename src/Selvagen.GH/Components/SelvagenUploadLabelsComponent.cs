using System;
using System.Collections.Generic;
using System.Drawing;
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
            pManager.AddPlaneParameter("Planes", "Pl", "Label placement planes (origin = position, orientation drives text rotation)", GH_ParamAccess.list);
            pManager.AddTextParameter("Texts", "T", "Label text strings", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the label set", GH_ParamAccess.item);
            pManager.AddColourParameter("Color", "C", "Per-label text colour (one per label, or a single colour for all)", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);

            Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("TextSetID", "ID", "ID of the created text set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var planes = new List<Plane>();
            var texts = new List<string>();
            var colors = new List<Color>();
            bool upload = false;

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, planes);
            DA.GetDataList(2, texts);
            DA.GetData(3, ref name);
            DA.GetDataList(4, colors);
            DA.GetData(5, ref upload);

            var client = SessionManager.Current;

            if (!upload || client == null || planes.Count == 0)
            {
                if (client == null && upload)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetWaiting(DA);
                return;
            }

            try
            {
                var textSet = TextConverter.FromPlanesAndTexts(
                    planes,
                    texts,
                    colors: colors.Count > 0 ? colors : null);
                var result = Task.Run(() => client.UploadText3DAsync(projectId, name, textSet)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({planes.Count} labels)");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, ex);
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadLabels");
    }
}
