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
                "Upload text labels from Rhino to the platform. [Upload de Rótulos 3D]")
        { }

        public override Guid ComponentGuid => new Guid("f5a6b7c8-d9e0-1234-5678-90abcdef1234");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Target project ID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Planes", "Pl", "Label placement planes (origin = position, orientation drives text rotation) [Planos de Posicionamento]", GH_ParamAccess.list);
            pManager.AddTextParameter("Texts", "T", "Label text strings [Textos dos Rótulos]", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "Display name for the label set [Nome de Exibição]", GH_ParamAccess.item);
            pManager.AddColourParameter("Color", "C", "Per-label text colour (one per label, or a single colour for all) [Cor do Texto]", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Justification", "J", "Per-label justification (0=BotLeft, 1=BotCenter, 2=BotRight, 3=MidLeft, 4=MidCenter, 5=MidRight, 6=TopLeft, 7=TopCenter, 8=TopRight) [Justificação do Texto]", GH_ParamAccess.list);
            pManager.AddNumberParameter("Size", "S", "Text size in model units (one per label or one for all). [Tamanho do Texto]", GH_ParamAccess.list);

            Params.Input[4].Optional = true;
            Params.Input[5].Optional = true;
            Params.Input[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Label Set ID", "LblID", "ID of the created label set", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Upload status", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "", name = "";
            var planes = new List<Plane>();
            var texts = new List<string>();
            var colors = new List<Color>();
            var justifications = new List<int>();
            var sizes = new List<double>();

            DA.GetData(0, ref projectId);
            DA.GetDataList(1, planes);
            DA.GetDataList(2, texts);
            DA.GetData(3, ref name);
            DA.GetDataList(4, colors);
            DA.GetDataList(5, justifications);
            DA.GetDataList(6, sizes);

            var client = SessionManager.Current;

            if (!UploadRequested)
            {
                if (client == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                SetReady(DA, 1);
                return;
            }

            if (client == null || planes.Count == 0 || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provide Project ID, Planes, Texts, and Name before uploading.");
                SetReady(DA, 1);
                return;
            }

            try
            {
                IsUploading = true;
                ForceCanvasRefresh();

                var labelSet = LabelConverter.ToLabelSet(
                    planes,
                    texts,
                    colors: colors.Count > 0 ? colors : null,
                    justifications: justifications.Count > 0 ? justifications : null,
                    fontSizes: sizes.Count > 0 ? sizes : null);
                var result = Task.Run(() => client.UploadLabelSetAsync(projectId, name, labelSet)).GetAwaiter().GetResult();

                DA.SetData(0, result.Id);
                DA.SetData(1, $"Uploaded: {result.Name} ({planes.Count} labels)");
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

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("UploadLabels");
    }
}
