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
    /// Uses manual async instead of GH_TaskCapableComponent because the latter
    /// silently skips the Solve phase for zero-input components.
    /// </summary>
    public abstract class SelvagenSelectableComponentBase<TItem>
        : GH_Component, ISelectorComponent
    {
        protected SelvagenSelectableComponentBase(string name, string nickname, string description, string subcategory)
            : base(name, nickname, description, "Selvagen", subcategory)
        { }

        // ── State ────────────────────────────────────────────────────────────

        protected TItem[] _cachedItems;
        protected object[] _cachedKey;
        protected string _selectedId;
        private bool _forceRefresh;
        private volatile bool _isFetching;
        private volatile string _lastFetchError;
        private readonly object _stateLock = new object();

        // ── Hooks subclasses implement ───────────────────────────────────────

        /// <summary>Read filter inputs synchronously into a captured array (runs on GH solver thread).</summary>
        protected abstract object[] CaptureInputs(IGH_DataAccess da);

        /// <summary>Async fetch using captured inputs (runs on worker thread).</summary>
        protected abstract Task<TItem[]> FetchAsync(SelvagenClient client, object[] inputs);

        protected abstract string GetId(TItem item);
        protected abstract string GetDisplayName(TItem item);

        /// <summary>Default: cache key is the captured inputs array. Override to project a subset.</summary>
        protected virtual object[] GetCacheKey(object[] inputs) => inputs;

        /// <summary>Subclasses register their filter inputs here.</summary>
        protected virtual void RegisterFilterInputs(GH_InputParamManager pManager) { }

        protected virtual string SelectedIdLabel => "Selected ID";
        protected virtual string SelectedIdNick => "ID";
        protected virtual string SelectedNameLabel => "Selected Name";
        protected virtual string SelectedNameNick => "Name";

        // ── Input/output registration ────────────────────────────────────────

        protected sealed override void RegisterInputParams(GH_InputParamManager pManager)
        {
            RegisterFilterInputs(pManager);
        }

        protected sealed override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(SelectedIdLabel, SelectedIdNick, "The picked item's UUID. Empty if nothing picked.", GH_ParamAccess.item);
            pManager.AddTextParameter(SelectedNameLabel, SelectedNameNick, "The picked item's display name. Empty if nothing picked.", GH_ParamAccess.item);
            pManager.AddTextParameter("All IDs", "IDs", "All item UUIDs.", GH_ParamAccess.list);
            pManager.AddTextParameter("All Names", "Names", "All item display names.", GH_ParamAccess.list);
        }

        // ── SolveInstance — single-phase with manual async ──────────────────

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var client = SessionManager.Current;
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                EmitOutputs(DA);
                return;
            }

            object[] inputs = CaptureInputs(DA);
            object[] currentKey = GetCacheKey(inputs);

            bool needsFetch = CacheDecision.NeedsFetch(
                hasCachedItems: _cachedItems != null,
                cachedKey: _cachedKey,
                currentKey: currentKey,
                forceRefresh: _forceRefresh);

            _forceRefresh = false;

            if (needsFetch && !_isFetching)
            {
                _isFetching = true;
                _lastFetchError = null;
                var capturedKey = currentKey;

                Task.Run(async () =>
                {
                    try
                    {
                        var items = await FetchAsync(client, inputs).ConfigureAwait(false);
                        if (items != null)
                        {
                            string currentSelected;
                            lock (_stateLock)
                            {
                                _cachedItems = items;
                                _cachedKey = capturedKey;
                                currentSelected = _selectedId;
                            }
                            string reconciled = Reconcile.SelectId(items, currentSelected, GetId);
                            lock (_stateLock)
                            {
                                _selectedId = reconciled;
                            }
                        }
                        else
                        {
                            _lastFetchError ??= "Fetch returned no data.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _lastFetchError = ex.Unwrap().Message;
                        PluginLogger.Log($"{GetType().Name} fetch error: {_lastFetchError}");
                    }
                    finally
                    {
                        _isFetching = false;
                        Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                        {
                            if (OnPingDocument() != null)
                                ExpireSolution(true);
                        }));
                    }
                });
            }

            if (_lastFetchError != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, _lastFetchError);
            }

            EmitOutputs(DA);
        }

        private void EmitOutputs(IGH_DataAccess DA)
        {
            TItem[] items;
            string selId;
            lock (_stateLock)
            {
                items = _cachedItems;
                selId = _selectedId;
            }

            string selectedId = selId ?? "";
            string selectedName = "";
            if (selId != null && items != null)
            {
                var match = items.FirstOrDefault(i => GetId(i) == selId);
                if (match != null) selectedName = GetDisplayName(match);
            }

            var ids = items == null ? new List<string>() : items.Select(GetId).ToList();
            var names = items == null ? new List<string>() : items.Select(GetDisplayName).ToList();

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
                TItem[] items; string selId;
                lock (_stateLock) { items = _cachedItems; selId = _selectedId; }
                if (items == null) return "Loading…";
                if (selId != null)
                {
                    var match = items.FirstOrDefault(i => GetId(i) == selId);
                    return match != null ? GetDisplayName(match) : "<missing item>";
                }
                return "— Select —";
            }
        }

        public bool HasItems => _cachedItems != null && _cachedItems.Length > 0;

        public IEnumerable<(string Id, string Name)> GetMenuItems()
        {
            TItem[] items;
            lock (_stateLock) { items = _cachedItems; }
            if (items == null) yield break;
            foreach (var item in items)
                yield return (GetId(item), GetDisplayName(item));
        }

        public string SelectedId => _selectedId;

        public void SetSelectedId(string id)
        {
            if (id == _selectedId) return;
            _selectedId = id;
            ExpireSolution(true);
        }

        public void RequestUpdate()
        {
            _forceRefresh = true;
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
