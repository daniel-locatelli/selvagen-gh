using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadCurvesComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadCurvesComponent()
            : base("Upload Curves", "SvUpCrv",
                "Upload curves from Rhino to the platform.")
        { }

        public override Guid ComponentGuid => new Guid("e4f5a6b7-c8d9-0123-4567-890abcdef123");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Target project ID", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curves", "Crv", "Rhino curves to upload", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the curve set", GH_ParamAccess.item);
            pManager.AddColourParameter("Color", "C", "Per-curve colour (one per curve, or a single colour for all)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Line thickness in pixels", GH_ParamAccess.item, 1.5);
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);

            // Color is optional
            Params.Input[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("CurveSetID", "ID", "ID of the created curve set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var curves = new List<Curve>();
            var colors = new List<Color>();
            double thickness = 1.5;
            bool upload = false;

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, curves);
            DA.GetData(2, ref name);
            DA.GetDataList(3, colors);
            DA.GetData(4, ref thickness);
            DA.GetData(5, ref upload);

            var client = SessionManager.Current;

            if (!upload || client == null || curves.Count == 0)
            {
                if (client == null && upload)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetWaiting(DA);
                return;
            }

            try
            {
                var curveSet = CurveConverter.ToCurveSet(
                    curves,
                    colors: colors.Count > 0 ? colors : null,
                    linewidth: thickness);
                var result = Task.Run(() => client.UploadCurvesAsync(projectId, name, curveSet)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({curves.Count} curves)");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, ex);
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadCurves");
    }
}
