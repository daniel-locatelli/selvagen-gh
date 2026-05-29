using System;
using System.Threading.Tasks;
using System.Drawing;
using Grasshopper.Kernel;
using Selvagen.Core.Api;

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

        private volatile bool _isRunningAsync;
        private Exception _asyncError;
        private bool _resultPending;
        private object _asyncResultBox;
        private readonly object _asyncLock = new object();

        /// <summary>True while a click-triggered network action is in flight.</summary>
        public bool IsRunningAsync => _isRunningAsync;

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
            var msg = ex.Unwrap().Message;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
            DA.SetData(statusIndex, $"Error: {msg}");
        }

        protected void ForceCanvasRefresh()
        {
            try { Grasshopper.Instances.ActiveCanvas?.Refresh(); }
            catch { /* canvas may not be visible during headless solves */ }
        }

        /// <summary>
        /// Run a click-triggered network action off the solver thread, then re-solve
        /// to emit its result. Capture all inputs (and any Rhino geometry conversion)
        /// BEFORE calling this — the worker lambda must not touch the solver thread or
        /// Rhino geometry.
        /// </summary>
        protected void StartAsync<TResult>(Func<Task<TResult>> work)
        {
            if (_isRunningAsync) return;
            _isRunningAsync = true;
            IsRunning = true;
            lock (_asyncLock) { _asyncError = null; }
            ForceCanvasRefresh();

            Task.Run(async () =>
            {
                try
                {
                    var r = await work().ConfigureAwait(false);
                    lock (_asyncLock) { _asyncResultBox = r; _resultPending = true; }
                }
                catch (Exception ex)
                {
                    lock (_asyncLock) { _asyncError = ex; }
                }
                finally
                {
                    _isRunningAsync = false;
                    IsRunning = false;
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        if (OnPingDocument() != null) ExpireSolution(true);
                    }));
                }
            });
        }

        /// <summary>
        /// Call at the TOP of SolveInstance. If a finished async result or error is
        /// waiting, emits it via the callback / runtime message and returns true
        /// (the caller should then return immediately).
        /// </summary>
        protected bool TryFinishAsync<TResult>(IGH_DataAccess DA, int statusIndex, Action<IGH_DataAccess, TResult> emitSuccess)
        {
            Exception err; bool pending; object box;
            lock (_asyncLock)
            {
                err = _asyncError; pending = _resultPending; box = _asyncResultBox;
                _asyncError = null;
                if (pending) _resultPending = false;
            }
            if (err != null) { SetActionError(DA, statusIndex, err); return true; }
            if (pending) { emitSuccess(DA, (TResult)box); return true; }
            return false;
        }

        public override void CreateAttributes()
        {
            m_attributes = new SelvagenActionAttributes(this);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null; // subclasses override
    }
}
