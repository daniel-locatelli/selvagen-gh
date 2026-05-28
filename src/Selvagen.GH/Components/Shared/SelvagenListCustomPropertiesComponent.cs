using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Lists all custom properties for a project. Inline dropdown shows keys;
    /// selecting one emits Selected Key/Value/Type. All Keys/Values/Types are
    /// always emitted as parallel lists.
    /// Note: does NOT extend SelvagenSelectableComponentBase because that base
    /// assumes items have distinct ID and Name. Custom properties use the key
    /// for both, and we emit 6 outputs instead of 4. We re-use SelvagenSelectorAttributes
    /// (via ISelectorComponent) for the dropdown chrome.
    /// </summary>
    public class SelvagenListCustomPropertiesComponent : GH_Component, ISelectorComponent
    {
        private CustomPropertyInfo[] _cached;
        private string _cachedProjectId;
        private string _selectedKey;
        private bool _forceRefresh;
        private volatile bool _isFetching;
        private volatile string _lastFetchError;

        public SelvagenListCustomPropertiesComponent()
            : base("List Custom Properties", "SvListProps",
                   "List all custom properties for a project. Pick one in the dropdown to get the Selected outputs. [Listar Propriedades Personalizadas]",
                   "Selvagen", "07 Shared") { }

        public override Guid ComponentGuid => new Guid("A1000006-0001-4000-8000-000000000002");
        protected override Bitmap Icon => IconLoader.Load("ListCustomProperties");
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override void CreateAttributes()
        {
            Attributes = new SelvagenSelectorAttributes(this);
        }

        // ── Params ─────────────────────────────────────────────────────────
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID to list properties for", GH_ParamAccess.item, "");
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Selected Key",   "K",   "The key picked in the inline dropdown",   GH_ParamAccess.item);
            pManager.AddTextParameter("Selected Value", "V",   "Its current value",                       GH_ParamAccess.item);
            pManager.AddTextParameter("Selected Type",  "T",   "Its value_type (text/number/boolean)",    GH_ParamAccess.item);
            pManager.AddTextParameter("All Keys",       "Ks",  "Every property's key",                    GH_ParamAccess.list);
            pManager.AddTextParameter("All Values",     "Vs",  "Every property's value",                  GH_ParamAccess.list);
            pManager.AddTextParameter("All Types",      "Ts",  "Every property's type",                   GH_ParamAccess.list);
        }

        // ── SolveInstance with cached fetch ────────────────────────────────
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectId = "";
            DA.GetData(0, ref projectId);

            var client = SessionManager.Current;
            if (client == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not logged in. Place a Login component first.");
                EmitOutputs(DA);
                return;
            }

            bool projectChanged = projectId != _cachedProjectId;
            bool needsFetch = (_cached == null) || projectChanged || _forceRefresh;
            _forceRefresh = false;

            if (needsFetch && !_isFetching && !string.IsNullOrEmpty(projectId))
            {
                _isFetching = true;
                _lastFetchError = null;
                var capturedProject = projectId;

                Task.Run(async () =>
                {
                    try
                    {
                        var items = await client.ListCustomPropertiesAsync(capturedProject).ConfigureAwait(false);
                        _cached = items ?? new CustomPropertyInfo[0];
                        _cachedProjectId = capturedProject;

                        // Keep the selection if its key still exists
                        if (_selectedKey != null && !_cached.Any(c => c.Key == _selectedKey))
                            _selectedKey = null;
                    }
                    catch (Exception ex)
                    {
                        _lastFetchError = ex.InnerException?.Message ?? ex.Message;
                        PluginLogger.Log($"{GetType().Name} fetch error: {_lastFetchError}");
                    }
                    finally
                    {
                        _isFetching = false;
                        Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                        {
                            if (OnPingDocument() != null) ExpireSolution(true);
                        }));
                    }
                });
            }

            if (_lastFetchError != null)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, _lastFetchError);

            EmitOutputs(DA);
        }

        private void EmitOutputs(IGH_DataAccess DA)
        {
            string selectedKey  = _selectedKey ?? "";
            string selectedVal  = "";
            string selectedType = "";

            var keys   = new List<string>();
            var vals   = new List<string>();
            var types  = new List<string>();
            if (_cached != null)
            {
                foreach (var c in _cached)
                {
                    keys.Add(c.Key);
                    vals.Add(c.Value);
                    types.Add(c.ValueType);
                    if (c.Key == _selectedKey)
                    {
                        selectedVal  = c.Value;
                        selectedType = c.ValueType;
                    }
                }
            }

            DA.SetData(0, selectedKey);
            DA.SetData(1, selectedVal);
            DA.SetData(2, selectedType);
            DA.SetDataList(3, keys);
            DA.SetDataList(4, vals);
            DA.SetDataList(5, types);
        }

        // ── ISelectorComponent ─────────────────────────────────────────────
        public string CurrentDisplayText
        {
            get
            {
                if (SessionManager.Current == null) return "Not logged in";
                if (_cached == null) return "Loading…";
                if (_selectedKey != null)
                {
                    if (_cached.Any(c => c.Key == _selectedKey)) return _selectedKey;
                    return "<missing key>";
                }
                return "— Select —";
            }
        }

        public bool HasItems => _cached != null && _cached.Length > 0;

        public IEnumerable<(string Id, string Name)> GetMenuItems()
        {
            if (_cached == null) yield break;
            foreach (var c in _cached) yield return (c.Key, c.Key);
        }

        public string SelectedId => _selectedKey;

        public void SetSelectedId(string id)
        {
            if (id == _selectedKey) return;
            _selectedKey = id;
            ExpireSolution(true);
        }

        public void RequestUpdate()
        {
            _forceRefresh = true;
            ExpireSolution(true);
        }

        // ── Right-click "Select" mirror (same UX as List Assets) ────────────
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            var selectMenu = new ToolStripMenuItem("Select");
            if (!HasItems)
            {
                selectMenu.DropDownItems.Add(new ToolStripMenuItem("(no items)") { Enabled = false });
            }
            else
            {
                foreach (var (id, name) in GetMenuItems())
                {
                    string capturedId = id;
                    var item = new ToolStripMenuItem(name) { Checked = id == _selectedKey };
                    item.Click += (s, e) => SetSelectedId(capturedId);
                    selectMenu.DropDownItems.Add(item);
                }
            }
            menu.Items.Insert(0, selectMenu);
        }

        // ── Persistence ────────────────────────────────────────────────────
        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            if (!string.IsNullOrEmpty(_selectedKey))
                writer.SetString("SelectedKey", _selectedKey);
            return true;
        }
        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            string key = null;
            reader.TryGetString("SelectedKey", ref key);
            _selectedKey = string.IsNullOrEmpty(key) ? null : key;
            return true;
        }
    }
}
