using System;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public abstract class SelvagenUploadComponentBase : GH_Component
    {
        private bool _uploadRequested;

        protected SelvagenUploadComponentBase(string name, string nickname, string description)
            : base(name, nickname, description, "Selvagen", "08 Assets") { }

        public bool IsUploading { get; protected set; }

        public bool UploadRequested
        {
            get
            {
                if (!_uploadRequested) return false;
                _uploadRequested = false;
                return true;
            }
        }

        public void RequestUpload()
        {
            _uploadRequested = true;
            ExpireSolution(true);
        }

        public override void CreateAttributes()
        {
            m_attributes = new SelvagenUploadAttributes(this);
        }

        protected void SetReady(IGH_DataAccess DA, int statusIndex)
        {
            DA.SetData(statusIndex, "Ready to upload.");
        }

        protected void SetUploadError(IGH_DataAccess DA, int statusIndex, Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
            DA.SetData(statusIndex, $"Error: {msg}");
        }

        protected void ForceCanvasRefresh()
        {
            try { Grasshopper.Instances.ActiveCanvas?.Refresh(); }
            catch { }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
    }
}
