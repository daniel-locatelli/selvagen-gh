using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Upload-flavored action component. Provides a grayscale gradient button
    /// labeled "Upload"/"Uploading...". Existing upload components should not
    /// need any change beyond the inheritance chain — the public surface
    /// (UploadRequested, IsUploading, RequestUpload) is preserved as aliases
    /// for the new generalized properties.
    /// </summary>
    public abstract class SelvagenUploadComponentBase : SelvagenActionComponentBase
    {
        protected SelvagenUploadComponentBase(string name, string nickname, string description, string subcategory = "08 Assets")
            : base(name, nickname, description, subcategory) { }

        // ── Aliases preserving the prior API ───────────────────────────────
        public bool IsUploading
        {
            get => IsRunning;
            protected set => IsRunning = value;
        }

        public bool UploadRequested => ActionRequested;

        public void RequestUpload() => RequestAction();

        // ── ISelvagenActionButton ──────────────────────────────────────────
        public override string ActionLabel        => "Upload";
        public override string ActionLabelRunning => "Uploading...";
        public override Color  ButtonGradientTop    => Color.FromArgb(130, 130, 130);
        public override Color  ButtonGradientBottom => Color.FromArgb(50, 50, 50);

        // ── Existing helpers kept here for backward compat ─────────────────
        protected void SetReady(IGH_DataAccess DA, int statusIndex)
        {
            DA.SetData(statusIndex, "Ready to upload.");
        }

        protected void SetUploadError(IGH_DataAccess DA, int statusIndex, Exception ex)
        {
            SetActionError(DA, statusIndex, ex);
        }
    }
}
