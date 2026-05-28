using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Shared base for components that expose an in-canvas action button
    /// (Upload, Delete, etc.). Generalizes the flag → ExpireSolution → consume-once
    /// pattern that the original <see cref="SelvagenUploadComponentBase"/> used.
    /// </summary>
    public abstract class SelvagenActionComponentBase : GH_Component, ISelvagenActionButton
    {
        private bool _actionRequested;

        protected SelvagenActionComponentBase(string name, string nickname, string description, string subcategory)
            : base(name, nickname, description, "Selvagen", subcategory) { }

        /// <summary>
        /// Set true while the network call is in flight. Subclasses should toggle
        /// this around their work and call ForceCanvasRefresh() if they want the
        /// "Uploading..."/"Deleting..." label to appear instantly.
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// One-shot flag: true exactly once after RequestAction() was called, and
        /// only when read. Subclasses check this in SolveInstance to decide whether
        /// the current run is a click-driven action vs. an upstream-data refresh.
        /// </summary>
        public bool ActionRequested
        {
            get
            {
                if (!_actionRequested) return false;
                _actionRequested = false;
                return true;
            }
        }

        public void RequestAction()
        {
            _actionRequested = true;
            ExpireSolution(true);
        }

        // ── ISelvagenActionButton — subclass fills these in ────────────────
        public abstract string ActionLabel { get; }
        public abstract string ActionLabelRunning { get; }
        public abstract Color ButtonGradientTop { get; }
        public abstract Color ButtonGradientBottom { get; }

        // ── Common error helpers ────────────────────────────────────────────
        protected void SetReady(IGH_DataAccess DA, int statusIndex, string readyMessage = "Ready.")
        {
            DA.SetData(statusIndex, readyMessage);
        }

        protected void SetActionError(IGH_DataAccess DA, int statusIndex, Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
            DA.SetData(statusIndex, $"Error: {msg}");
        }

        protected void ForceCanvasRefresh()
        {
            try { Grasshopper.Instances.ActiveCanvas?.Refresh(); }
            catch { /* canvas may not be visible during headless solves */ }
        }

        public override void CreateAttributes()
        {
            m_attributes = new SelvagenActionAttributes(this);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null; // subclasses override
    }
}
