using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadColorLegendComponent : SelvagenUploadComponentBase
    {
        public SelvagenUploadColorLegendComponent()
            : base("Upload Color Legend", "SvUpLegend",
                "Upload a color legend to the platform. [Upload de Legenda de Cores]")
        { }

        public override Guid ComponentGuid => new Guid("f3a12ee1-3b46-4389-94a6-7564edf5b07c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Legend display name [Nome]", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Variant", "V", "0 = gradient, 1 = discrete [Variante]", GH_ParamAccess.item, 0);
            pManager.AddColourParameter("Colors", "C", "List of colors [Cores]", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Lb", "Per-color labels (discrete) [Rótulos]", GH_ParamAccess.list);
            pManager.AddNumberParameter("Domain Min", "DMin", "Start of value range. Defaults to 0 when not connected. [Domínio Mín]", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Domain Max", "DMax", "End of value range. Defaults to 1 when not connected. [Domínio Máx]", GH_ParamAccess.item, 1.0);
            pManager.AddTextParameter("Unit", "U", "Display unit, e.g. %, °, m [Unidade]", GH_ParamAccess.item);

            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;

            var variantParam = Params.Input[2] as Grasshopper.Kernel.Parameters.Param_Integer;
            variantParam?.AddNamedValue("Gradient", 0);
            variantParam?.AddNamedValue("Discrete", 1);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Legend ID", "LgdID", "ID of the created/updated legend [ID da Legenda]", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status [Status]", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "", unit = "";
            int variant = 0;
            // Domain Min/Max have parameter-level defaults (0 and 1), so DA.GetData
            // is guaranteed to populate them whether or not a wire is connected.
            double domainMin = 0, domainMax = 1;

            var colors = new List<Color>();
            var labels = new List<string>();

            DA.GetData(0, ref projectId);
            DA.GetData(1, ref name);
            DA.GetData(2, ref variant);
            DA.GetDataList(3, colors);
            DA.GetDataList(4, labels);
            DA.GetData(5, ref domainMin);
            DA.GetData(6, ref domainMax);
            DA.GetData(7, ref unit);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || colors.Count == 0 || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Name, and at least one Color before uploading.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var hexColors = new string[colors.Count];
                for (int i = 0; i < colors.Count; i++)
                {
                    var c = colors[i];
                    hexColors[i] = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }

                var payload = new ColorLegendPayload
                {
                    Variant = variant == 1 ? "discrete" : "gradient",
                    Colors = hexColors,
                    Labels = labels.Count > 0 ? labels.ToArray() : null,
                    DomainMin = (float)domainMin,
                    DomainMax = (float)domainMax,
                    Unit = !string.IsNullOrEmpty(unit) ? unit : null,
                };

                var result = Task.Run(() => client.UpsertColorLegendAsync(projectId, name, payload)).GetAwaiter().GetResult();

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

        protected override Bitmap Icon => IconLoader.Load("UploadColorLegend");
    }
}
