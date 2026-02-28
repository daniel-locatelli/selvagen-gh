using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Selvagen.Core.Api;
using Selvagen.Core.Converters;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadCurvesComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadCurvesComponent()
            : base("Selvagen Upload Curves", "SvUpCrv",
                "Upload curves from Rhino to the Selvagen platform.")
        { }

        public override Guid ComponentGuid => new Guid("e4f5a6b7-c8d9-0123-4567-890abcdef123");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Client", "C", "Authenticated Selvagen client", GH_ParamAccess.item);
            pManager.AddTextParameter("ProjectID", "PID", "Target project ID", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curves", "Crv", "Rhino curves to upload", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the curve set", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("CurveSetID", "ID", "ID of the created curve set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object clientObj = null;
            string projectId = "", name = "";
            var curves = new List<Curve>();
            bool upload = false;

            DA.GetData(0, ref clientObj);
            DA.GetData(1, ref projectId);
            DA.GetDataList(2, curves);
            DA.GetData(3, ref name);
            DA.GetData(4, ref upload);

            if (!upload || !(clientObj is SelvagenClient client) || curves.Count == 0)
            {
                SetWaiting(DA);
                return;
            }

            try
            {
                var curveSet = CurveConverter.ToCurveSet(curves);
                var result = Task.Run(() => client.UploadCurvesAsync(projectId, name, curveSet)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({curves.Count} curves)");
            }
            catch (Exception ex)
            {
                SetUploadError(DA, ex);
            }
        }
    }
}
