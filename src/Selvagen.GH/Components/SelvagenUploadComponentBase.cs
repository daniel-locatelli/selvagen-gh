using System;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Base class for Selvagen upload components, providing shared error handling
    /// and the common "Upload" tab category.
    /// </summary>
    public abstract class SelvagenUploadComponentBase : GH_Component
    {
        protected SelvagenUploadComponentBase(string name, string nickname, string description)
            : base(name, nickname, description, "Selvagen", "Upload") { }

        protected void SetWaiting(IGH_DataAccess DA)
        {
            DA.SetData(0, null);
            DA.SetData(1, "Waiting...");
        }

        protected void SetUploadError(IGH_DataAccess DA, Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
            DA.SetData(0, null);
            DA.SetData(1, $"Error: {msg}");
        }
    }
}
