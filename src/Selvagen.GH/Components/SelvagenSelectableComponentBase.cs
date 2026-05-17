using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Components;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Shared base for the three Selvagen "Data" components that fetch a list of items
    /// and let the user pick one via an inline dropdown.
    /// </summary>
    public abstract class SelvagenSelectableComponentBase<TItem>
        : GH_TaskCapableComponent<TItem[]>, ISelectorComponent
    {
        protected SelvagenSelectableComponentBase(string name, string nickname, string description, string subcategory)
            : base(name, nickname, description, "Selvagen", subcategory)
        { }

        // ── State ────────────────────────────────────────────────────────────

        protected TItem[] _cachedItems;
        protected object[] _cachedKey;
        protected string _selectedId;
        protected bool _refreshWasTrue;
        private volatile string _lastFetchError;

        // ── Hooks subclasses implement ───────────────────────────────────────

        /// <summary>Read filter inputs synchronously into a captured array (runs on GH solver thread).</summary>
        protected abstract object[] CaptureInputs(IGH_DataAccess da);

        /// <summary>Async fetch using captured inputs (runs on worker thread).</summary>
        protected abstract Task<TItem[]> FetchAsync(SelvagenClient client, object[] inputs);

        protected abstract string GetId(TItem item);
        protected abstract string GetDisplayName(TItem item);

        /// <summary>Default: cache key is the captured inputs array. Override to project a subset.</summary>
        protected virtual object[] GetCacheKey(object[] inputs) => inputs;

        /// <summary>Subclasses register their filter inputs here. Refresh is appended automatically.</summary>
        protected virtual void RegisterFilterInputs(GH_InputParamManager pManager) { }

        // ── Input/output registration ────────────────────────────────────────

        protected sealed override void RegisterInputParams(GH_InputParamManager pManager)
        {
            RegisterFilterInputs(pManager);
            pManager.AddBooleanParameter("Refresh", "R", "Force a re-fetch", GH_ParamAccess.item, false);
        }

        protected sealed override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("SelectedID", "ID", "The picked item's UUID. Empty if nothing picked.", GH_ParamAccess.item);
            pManager.AddTextParameter("SelectedName", "Name", "The picked item's display name. Empty if nothing picked.", GH_ParamAccess.item);
            pManager.AddTextParameter("IDs", "IDs", "All item UUIDs.", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "Names", "All item display names.", GH_ParamAccess.list);
        }

        protected int RefreshInputIndex => Params.Input.Count - 1;

        // ── SolveInstance — two-phase via GH_TaskCapableComponent ────────────

        // Per-solve scratch state, set in InPreSolve, consumed in Solve. Cleared in BeforeSolveInstance.
        private bool _pendingFetch;
        private object[] _pendingKey;

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            _pendingFetch = false;
            _pendingKey = null;
            // _lastFetchError survives across solves until cleared by a successful fetch attempt
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var client = SessionManager.Current;
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                EmitOutputs(DA);
                return;
            }

            if (InPreSolve)
            {
                bool refresh = false;
                DA.GetData(RefreshInputIndex, ref refresh);

                object[] inputs = CaptureInputs(DA);
                object[] currentKey = GetCacheKey(inputs);

                bool needsFetch = CacheDecision.NeedsFetch(
                    hasCachedItems: _cachedItems != null,
                    cachedKey: _cachedKey,
                    currentKey: currentKey,
                    refresh: refresh,
                    refreshWasTrue: _refreshWasTrue);

                _refreshWasTrue = refresh;
                _pendingFetch = needsFetch;
                _pendingKey = currentKey;

                if (needsFetch)
                {
                    _lastFetchError = null;
                    TaskList.Add(Task.Run(async () =>
                    {
                        try
                        {
                            return await FetchAsync(client, inputs).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _lastFetchError = ex.InnerException?.Message ?? ex.Message;
                            PluginLogger.Log($"{GetType().Name} fetch error: {_lastFetchError}");
                            return null;
                        }
                    }));
                }
                return;
            }

            // Solve phase: pull fetch result if one was enlisted, then emit outputs.
            if (_pendingFetch && GetSolveResults(DA, out TItem[] items) && items != null)
            {
                _cachedItems = items;
                _cachedKey = _pendingKey;

                string reconciled = Reconcile.SelectId(_cachedItems, _selectedId, GetId);
                if (_selectedId != null && reconciled == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Previously-selected item no longer exists.");
                }
                _selectedId = reconciled;
            }

            if (_lastFetchError != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, _lastFetchError);
            }

            EmitOutputs(DA);
        }

        private void EmitOutputs(IGH_DataAccess DA)
        {
            string selectedId = _selectedId ?? "";
            string selectedName = "";
            if (_selectedId != null && _cachedItems != null)
            {
                var match = _cachedItems.FirstOrDefault(i => GetId(i) == _selectedId);
                if (match != null) selectedName = GetDisplayName(match);
            }

            var ids = _cachedItems == null
                ? new List<string>()
                : _cachedItems.Select(GetId).ToList();
            var names = _cachedItems == null
                ? new List<string>()
                : _cachedItems.Select(GetDisplayName).ToList();

            DA.SetData(0, selectedId);
            DA.SetData(1, selectedName);
            DA.SetDataList(2, ids);
            DA.SetDataList(3, names);
        }

        // ── ISelectorComponent ───────────────────────────────────────────────

        public string CurrentDisplayText
        {
            get
            {
                if (SessionManager.Current == null) return "Not logged in";
                if (_cachedItems == null) return "Loading…";
                if (_selectedId != null)
                {
                    var match = _cachedItems.FirstOrDefault(i => GetId(i) == _selectedId);
                    if (match != null) return GetDisplayName(match);
                    return "<missing item>";
                }
                return "— Select —";
            }
        }

        public bool HasItems => _cachedItems != null && _cachedItems.Length > 0;

        public IEnumerable<(string Id, string Name)> GetMenuItems()
        {
            if (_cachedItems == null) yield break;
            foreach (var item in _cachedItems)
                yield return (GetId(item), GetDisplayName(item));
        }

        public string SelectedId => _selectedId;

        public void SetSelectedId(string id)
        {
            if (id == _selectedId) return;
            _selectedId = id;
            ExpireSolution(true);
        }

        // ── Right-click menu mirror ──────────────────────────────────────────

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var selectMenu = new ToolStripMenuItem("Select");
            if (!HasItems)
            {
                var empty = new ToolStripMenuItem("(no items)") { Enabled = false };
                selectMenu.DropDownItems.Add(empty);
            }
            else
            {
                foreach (var (id, name) in GetMenuItems())
                {
                    string capturedId = id;
                    var item = new ToolStripMenuItem(name)
                    {
                        Checked = id == _selectedId,
                    };
                    item.Click += (s, e) => SetSelectedId(capturedId);
                    selectMenu.DropDownItems.Add(item);
                }
            }
            menu.Items.Insert(0, selectMenu);
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            if (!string.IsNullOrEmpty(_selectedId))
                writer.SetString("SelectedId", _selectedId);
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            string id = null;
            reader.TryGetString("SelectedId", ref id);
            _selectedId = string.IsNullOrEmpty(id) ? null : id;
            return true;
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            Attributes = new SelvagenSelectorAttributes(this);
        }
    }
}
