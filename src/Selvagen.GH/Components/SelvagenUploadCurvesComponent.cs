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
                "Upload curves from Rhino to the platform. [Upload de Curvas]")
        { }

        public override Guid ComponentGuid => new Guid("e4f5a6b7-c8d9-0123-4567-890abcdef123");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curves", "Crv", "Rhino curves to upload [Curvas do Rhino]", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the curve set [Nome de Exibição]", GH_ParamAccess.item);
            pManager.AddColourParameter("Color", "C", "Per-curve colour (one per curve, or a single colour for all) [Cor por Curva]", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Per-curve line thickness in pixels (one per curve, or a single value for all) [Espessura de Linha]", GH_ParamAccess.list);

            Params.Input[3].Optional = true;
            Params.Input[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Curve Set ID", "CrvID", "ID of the created curve set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Emit a finished async result, if one is waiting.
            if (TryFinishAsync<(Selvagen.Core.Models.UploadResult Upload, int Count)>(DA, 1, (da, t) =>
                {
                    da.SetData(0, t.Upload.Id);
                    da.SetData(1, $"Uploaded: {t.Upload.Name} ({t.Count} curves)");
                }))
                return;

            string projectId = "", name = "";
            var curves = new List<Curve>();
            var colors = new List<Color>();
            var thicknesses = new List<double>();

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, curves);
            DA.GetData(2, ref name);
            DA.GetDataList(3, colors);
            DA.GetDataList(4, thicknesses);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (IsRunningAsync) { DA.SetData(1, "Uploading..."); return; }
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || curves.Count == 0 || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Curves, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            // Convert Rhino geometry on the solver thread; only the HTTP call goes async.
            int count = curves.Count;
            var curveSet = CurveConverter.ToCurveSet(
                curves,
                colors: colors.Count > 0 ? colors : null,
                linewidths: thicknesses.Count > 0 ? thicknesses : null);
            StartAsync(async () =>
            {
                var r = await client.UploadCurvesAsync(projectId, name, curveSet).ConfigureAwait(false);
                return (r, count);
            });
            DA.SetData(1, "Uploading...");
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadCurves");
    }
}
