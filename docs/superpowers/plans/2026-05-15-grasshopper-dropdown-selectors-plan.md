# Grasshopper Dropdown Selectors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add inline dropdowns to `List Clients`, `List Projects`, and `List Assets` so users pick items directly on the canvas; fix the cascade-invalidation bug; persist selections across save/load.

**Architecture:** A generic abstract base `SelvagenSelectableComponentBase<TItem>` (inherits `GH_TaskCapableComponent<TItem[]>`) owns all shared state, fetch lifecycle, persistence, output emission, cache invalidation (keyed on filter inputs), and right-click menu. A custom `SelvagenSelectorAttributes` subclass of `GH_ComponentAttributes` paints the inline dropdown widget and translates clicks into `SetSelectedId` calls. Cache-decision and reconciliation logic lives in pure helpers (`Selvagen.Core/Components/SelectorCache.cs`) so it can be unit-tested without a Grasshopper runtime. Three concrete components implement only the four hooks: `FetchAsync`, `GetId`, `GetDisplayName`, `GetCacheKey`. Integration tests drive the full cascade through the Cordyceps MCP server (which can invoke right-click menu items but not click custom canvas widgets — hence the menu mirror).

**Tech Stack:** C# (multi-target net48 + net8.0-windows + net8.0), Grasshopper 8.x SDK (`GH_TaskCapableComponent`, `GH_ComponentAttributes`, `GH_Capsule`, `GH_IO.Serialization`), xUnit (unit tests), pytest + pytest-asyncio + the official Python `mcp` SDK + Cordyceps MCP (integration tests).

**Spec:** `docs/superpowers/specs/2026-05-14-grasshopper-dropdown-selectors-design.md`

---

## File map

**Create:**
- `src/Selvagen.Core/Components/SelectorCache.cs` — pure helpers `CacheDecision.NeedsFetch` and `Reconcile.SelectId<T>`
- `src/Selvagen.GH/Components/ISelectorComponent.cs` — non-generic interface so attributes can talk to the generic base
- `src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs` — abstract base
- `src/Selvagen.GH/Components/SelvagenSelectorAttributes.cs` — inline dropdown widget
- `tests/Selvagen.Core.Tests/Components/SelectorCacheTests.cs` — unit tests
- `tests/integration/requirements.txt`
- `tests/integration/conftest.py`
- `tests/integration/run.ps1`
- `tests/integration/README.md`
- `tests/integration/scenarios/__init__.py`
- `tests/integration/scenarios/test_clients_cascade.py`
- `tests/integration/scenarios/test_persistence.py`
- `tests/integration/scenarios/test_missing_item.py`
- `docs/INTEGRATION_TESTING.md`

**Modify:**
- `src/Selvagen.GH/Components/SelvagenClientsComponent.cs` — refactor to inherit base
- `src/Selvagen.GH/Components/SelvagenProjectsComponent.cs` — refactor
- `src/Selvagen.GH/Components/SelvagenListAssetsComponent.cs` — refactor; drop `Types` output
- `README.md` — fix stale "net48 + net7.0" claim → `net48` + `net8.0` / `net8.0-windows`; add Integration Tests section

`tests/integration/bootstrap.gh` already exists (binary, hand-saved). Do not modify.

---

## Task 1: Pure cache helpers + unit tests (TDD)

**Files:**
- Create: `src/Selvagen.Core/Components/SelectorCache.cs`
- Test: `tests/Selvagen.Core.Tests/Components/SelectorCacheTests.cs`

- [ ] **Step 1.1: Write the failing tests**

Create `tests/Selvagen.Core.Tests/Components/SelectorCacheTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Selvagen.Core.Components;
using Xunit;

namespace Selvagen.Core.Tests.Components
{
    public class CacheDecisionTests
    {
        [Fact]
        public void NeedsFetch_True_When_NoCachedItems()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: false,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: false,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_False_When_KeysMatch_AndNoRefreshEdge()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a", "meshes" },
                currentKey: new object[] { "a", "meshes" },
                refresh: false,
                refreshWasTrue: false);

            Assert.False(result);
        }

        [Fact]
        public void NeedsFetch_True_When_KeysDiffer()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "old-id" },
                currentKey: new object[] { "new-id" },
                refresh: false,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_True_On_RefreshEdge_FalseToTrue()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: true,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_False_When_Refresh_HeldHigh()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: true,
                refreshWasTrue: true);

            Assert.False(result);
        }

        [Fact]
        public void NeedsFetch_False_When_KeysMatch_AndRefreshDropped()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: new object[] { "a" },
                currentKey: new object[] { "a" },
                refresh: false,
                refreshWasTrue: true);

            Assert.False(result);
        }

        [Fact]
        public void NeedsFetch_TreatsNullCachedKey_AsMismatch()
        {
            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: null,
                currentKey: new object[] { "a" },
                refresh: false,
                refreshWasTrue: false);

            Assert.True(result);
        }

        [Fact]
        public void NeedsFetch_KeyEquality_Uses_ValueComparison()
        {
            // string interning aside, equal-valued object[] should compare equal
            var k1 = new object[] { "a", 1 };
            var k2 = new object[] { "a", 1 };

            var result = CacheDecision.NeedsFetch(
                hasCachedItems: true,
                cachedKey: k1,
                currentKey: k2,
                refresh: false,
                refreshWasTrue: false);

            Assert.False(result);
        }
    }

    public class ReconcileTests
    {
        private record Item(string Id, string Name);

        [Fact]
        public void SelectId_Returns_PersistedId_When_Present()
        {
            var items = new[] { new Item("a", "Alpha"), new Item("b", "Beta") };

            var result = Reconcile.SelectId(items, "b", x => x.Id);

            Assert.Equal("b", result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Persisted_Missing()
        {
            var items = new[] { new Item("a", "Alpha"), new Item("b", "Beta") };

            var result = Reconcile.SelectId(items, "ghost", x => x.Id);

            Assert.Null(result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Persisted_Null()
        {
            var items = new[] { new Item("a", "Alpha") };

            var result = Reconcile.SelectId(items, null, x => x.Id);

            Assert.Null(result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Items_Empty()
        {
            var items = new Item[0];

            var result = Reconcile.SelectId(items, "a", x => x.Id);

            Assert.Null(result);
        }

        [Fact]
        public void SelectId_Returns_Null_When_Items_Null()
        {
            var result = Reconcile.SelectId<Item>(null, "a", x => x.Id);

            Assert.Null(result);
        }
    }
}
```

- [ ] **Step 1.2: Run tests — confirm they fail to compile**

```powershell
dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj
```

Expected: build error — `Selvagen.Core.Components` namespace does not exist.

- [ ] **Step 1.3: Implement helpers**

Create `src/Selvagen.Core/Components/SelectorCache.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Selvagen.Core.Components
{
    /// <summary>
    /// Decides whether a selectable component must re-fetch its item list.
    /// Pure logic, lifted out of the Grasshopper component so it can be unit-tested.
    /// </summary>
    public static class CacheDecision
    {
        public static bool NeedsFetch(
            bool hasCachedItems,
            object[] cachedKey,
            object[] currentKey,
            bool refresh,
            bool refreshWasTrue)
        {
            if (!hasCachedItems) return true;
            if (cachedKey == null) return true;
            if (!KeysEqual(cachedKey, currentKey)) return true;
            if (refresh && !refreshWasTrue) return true;
            return false;
        }

        private static bool KeysEqual(object[] a, object[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!Equals(a[i], b[i])) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Reconciles a persisted selection ID against the current item list.
    /// Returns the persisted ID if it still exists, otherwise null.
    /// Never auto-picks a different item — silent re-selection would surprise users.
    /// </summary>
    public static class Reconcile
    {
        public static string SelectId<T>(IEnumerable<T> items, string persistedId, Func<T, string> getId)
        {
            if (string.IsNullOrEmpty(persistedId)) return null;
            if (items == null) return null;
            return items.Any(x => getId(x) == persistedId) ? persistedId : null;
        }
    }
}
```

- [ ] **Step 1.4: Run tests — confirm all pass**

```powershell
dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj
```

Expected: PASS — all 13 new tests + previously-passing tests.

- [ ] **Step 1.5: Commit**

```powershell
git add src/Selvagen.Core/Components/SelectorCache.cs tests/Selvagen.Core.Tests/Components/SelectorCacheTests.cs
git commit -m "feat(core): add SelectorCache helpers for fetch decision and selection reconciliation"
```

---

## Task 2: Non-generic selector interface

**Files:**
- Create: `src/Selvagen.GH/Components/ISelectorComponent.cs`

The custom-attributes class must talk to the base component without knowing `TItem`. A small interface decouples them.

- [ ] **Step 2.1: Create the interface**

Create `src/Selvagen.GH/Components/ISelectorComponent.cs`:

```csharp
using System.Collections.Generic;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Non-generic surface that <see cref="SelvagenSelectorAttributes"/> uses
    /// to render the inline dropdown without knowing the concrete item type.
    /// </summary>
    public interface ISelectorComponent
    {
        /// <summary>The text shown inside the dropdown rectangle right now.</summary>
        string CurrentDisplayText { get; }

        /// <summary>True if there is at least one cached item available to pick.</summary>
        bool HasItems { get; }

        /// <summary>Item id+display-name pairs in display order. Empty if none cached.</summary>
        IEnumerable<(string Id, string Name)> GetMenuItems();

        /// <summary>The currently-picked item id, or null/empty if nothing picked.</summary>
        string SelectedId { get; }

        /// <summary>Pick an item by id. No-op when id matches current selection.</summary>
        void SetSelectedId(string id);
    }
}
```

- [ ] **Step 2.2: Verify it compiles**

```powershell
dotnet build src/Selvagen.GH/Selvagen.GH.csproj
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 2.3: Commit**

```powershell
git add src/Selvagen.GH/Components/ISelectorComponent.cs
git commit -m "feat(gh): add ISelectorComponent interface for selector attributes"
```

---

## Task 3: Selectable component base class

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs`

This is the largest single file in the plan (~280 LOC). It owns: input/output registration, two-phase `SolveInstance`, fetch error stash, reconciliation, persistence (`Read`/`Write`), `SetSelectedId`, right-click menu mirror, and `CreateAttributes` to install the custom widget. **Note:** custom attributes class is created in Task 4 — for this task we keep `CreateAttributes` returning the default; we'll override it once `SelvagenSelectorAttributes` exists, in Task 4.

### Intentional deviation from spec

The spec defines `FetchAsync(SelvagenClient client, IGH_DataAccess da)` and `GetCacheKey(IGH_DataAccess da)`. We split `FetchAsync` into two hooks:

1. `CaptureInputs(IGH_DataAccess da)` — synchronous; reads the filter input values into a captured array. Runs only on the GH solver thread.
2. `FetchAsync(SelvagenClient client, object[] inputs)` — async; uses captured values. Runs on a thread-pool worker.

Reason: `IGH_DataAccess` is not thread-safe — calling `da.GetData(...)` from a `Task.Run` worker is undefined behaviour in Grasshopper. The spec's signature would only be safe if Grasshopper guarantees the solver thread blocks on the task before reusing `DA`, which it does not. Splitting input capture from fetch keeps the contract clear and safe.

`GetCacheKey` is replaced by deriving the cache key from the captured inputs array — by default the inputs array *is* the cache key, which removes a redundant per-component method.

- [ ] **Step 3.1: Create the base class**

Create `src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs`:

```csharp
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
        protected string _lastFetchError;

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

        public override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
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
    }
}
```

- [ ] **Step 3.2: Verify it compiles**

```powershell
dotnet build src/Selvagen.GH/Selvagen.GH.csproj
```

Expected: BUILD SUCCEEDED. (Existing concrete components don't yet inherit it; build still passes.)

- [ ] **Step 3.3: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs
git commit -m "feat(gh): add SelvagenSelectableComponentBase with two-phase fetch and persistence"
```

---

## Task 4: Custom attributes — inline dropdown widget

**Files:**
- Create: `src/Selvagen.GH/Components/SelvagenSelectorAttributes.cs`
- Modify: `src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs` (add `CreateAttributes` override)

- [ ] **Step 4.1: Create the attributes class**

Create `src/Selvagen.GH/Components/SelvagenSelectorAttributes.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Custom canvas attributes that paint an inline dropdown rectangle inside the
    /// component face and route clicks into <see cref="ISelectorComponent.SetSelectedId"/>.
    /// </summary>
    internal class SelvagenSelectorAttributes : GH_ComponentAttributes
    {
        private const int DropdownHeight = 22;
        private const int DropdownPadding = 4;
        private const int InnerSidePadding = 6;

        private RectangleF _dropdownRect;

        public SelvagenSelectorAttributes(GH_Component owner) : base(owner) { }

        private ISelectorComponent Selector => (ISelectorComponent)Owner;

        protected override void Layout()
        {
            base.Layout();

            // Expand the component bounds downward and shift outputs down to make
            // room for the dropdown row between the input row and output rows.
            var bounds = Bounds;
            int extra = DropdownHeight + DropdownPadding;

            bounds.Height += extra;
            Bounds = bounds;

            // Move all output param attributes (right side) down by `extra`.
            foreach (var output in Owner.Params.Output)
            {
                if (output.Attributes == null) continue;
                var b = output.Attributes.Bounds;
                b.Y += extra;
                output.Attributes.Bounds = b;
                var p = output.Attributes.Pivot;
                p.Y += extra;
                output.Attributes.Pivot = p;
            }

            // Compute dropdown rectangle: full inner width, just below the input row.
            float topOfDropdown = Bounds.Top + ComputeInputRowsHeight() + DropdownPadding / 2f;
            _dropdownRect = new RectangleF(
                Bounds.Left + InnerSidePadding,
                topOfDropdown,
                Bounds.Width - 2 * InnerSidePadding,
                DropdownHeight);
        }

        /// <summary>
        /// Approximate height occupied by input parameter rows. Grasshopper uses
        /// ~20 px per row plus a small header band; this estimate is good enough
        /// for placing the dropdown directly under the input row(s).
        /// </summary>
        private int ComputeInputRowsHeight()
        {
            // Use the actual span of input attributes if available.
            float top = Bounds.Top;
            float bottom = top;
            foreach (var input in Owner.Params.Input)
            {
                if (input.Attributes == null) continue;
                var b = input.Attributes.Bounds;
                if (b.Bottom > bottom) bottom = b.Bottom;
            }
            return (int)Math.Max(20, bottom - top);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;

            // Capsule looks like a Value List dropdown.
            var capsule = GH_Capsule.CreateCapsule(_dropdownRect, GH_Palette.Black);
            capsule.Render(graphics, Selected, Owner.Locked, false);
            capsule.Dispose();

            // ▼ glyph
            using (var glyphFont = GH_FontServer.NewFont("Verdana", 7f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var glyphRect = new RectangleF(_dropdownRect.X + 4, _dropdownRect.Y, 12, _dropdownRect.Height);
                var glyphFmt = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
                graphics.DrawString("▼", glyphFont, textBrush, glyphRect, glyphFmt);

                var textRect = new RectangleF(
                    _dropdownRect.X + 18,
                    _dropdownRect.Y,
                    _dropdownRect.Width - 22,
                    _dropdownRect.Height);
                var textFmt = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                };
                using (var labelFont = GH_FontServer.NewFont("Verdana", 7.0f, FontStyle.Regular))
                {
                    graphics.DrawString(Selector.CurrentDisplayText, labelFont, textBrush, textRect, textFmt);
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && _dropdownRect.Contains(e.CanvasLocation))
            {
                ShowDropdownMenu(sender);
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }

        private void ShowDropdownMenu(GH_Canvas canvas)
        {
            var menu = new ToolStripDropDown { AutoClose = true };

            if (!Selector.HasItems)
            {
                menu.Items.Add(new ToolStripMenuItem("(no items)") { Enabled = false });
            }
            else
            {
                foreach (var (id, name) in Selector.GetMenuItems())
                {
                    string capturedId = id;
                    var item = new ToolStripMenuItem(name)
                    {
                        Font = id == Selector.SelectedId
                            ? new Font(menu.Font, FontStyle.Bold)
                            : menu.Font,
                    };
                    item.Click += (s, ev) => Selector.SetSelectedId(capturedId);
                    menu.Items.Add(item);
                }
            }

            // Anchor at the bottom-left of the dropdown rect, in screen coordinates.
            var canvasPt = new PointF(_dropdownRect.Left, _dropdownRect.Bottom);
            var screenPt = canvas.Viewport.ProjectPoint(canvasPt);
            menu.Show(canvas, new Point((int)screenPt.X, (int)screenPt.Y));
        }
    }
}
```

- [ ] **Step 4.2: Wire it into the base class**

In `src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs`, add this override after the `Exposure` property:

```csharp
        public override void CreateAttributes()
        {
            Attributes = new SelvagenSelectorAttributes(this);
        }
```

- [ ] **Step 4.3: Verify it compiles**

```powershell
dotnet build src/Selvagen.GH/Selvagen.GH.csproj
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 4.4: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenSelectorAttributes.cs src/Selvagen.GH/Components/SelvagenSelectableComponentBase.cs
git commit -m "feat(gh): add SelvagenSelectorAttributes inline dropdown widget"
```

---

## Task 5: Refactor SelvagenClientsComponent

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenClientsComponent.cs`

- [ ] **Step 5.1: Replace the file**

Overwrite `src/Selvagen.GH/Components/SelvagenClientsComponent.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenClientsComponent : SelvagenSelectableComponentBase<FirmInfo>
    {
        public SelvagenClientsComponent()
            : base("List Clients", "SvClients",
                "List clients of the firm. Pick one from the inline dropdown to feed downstream components.",
                "Data")
        { }

        public override Guid ComponentGuid => new Guid("F23D9E81-A7C2-4B1D-8F9E-3D4C5B6A7E8F");

        protected override object[] CaptureInputs(IGH_DataAccess da) => new object[0];

        protected override Task<FirmInfo[]> FetchAsync(SelvagenClient client, object[] inputs)
            => client.ListClientsAsync();

        protected override string GetId(FirmInfo item) => item.Id;
        protected override string GetDisplayName(FirmInfo item) => item.LegalName;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Clients");
    }
}
```

- [ ] **Step 5.2: Build**

```powershell
dotnet build src/Selvagen.GH/Selvagen.GH.csproj
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 5.3: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenClientsComponent.cs
git commit -m "refactor(gh): port SelvagenClientsComponent to selectable base with inline dropdown"
```

---

## Task 6: Refactor SelvagenProjectsComponent

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenProjectsComponent.cs`

- [ ] **Step 6.1: Replace the file**

Overwrite `src/Selvagen.GH/Components/SelvagenProjectsComponent.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenProjectsComponent : SelvagenSelectableComponentBase<ProjectInfo>
    {
        public SelvagenProjectsComponent()
            : base("List Projects", "SvProjects",
                "List projects from the platform. Optionally filter by ClientID; pick one from the inline dropdown.",
                "Data")
        { }

        public override Guid ComponentGuid => new Guid("c2d3e4f5-a6b7-8901-2345-67890abcdef1");

        protected override void RegisterFilterInputs(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ClientID", "Id", "Optional client filter", GH_ParamAccess.item, "");
            pManager[0].Optional = true;
        }

        protected override object[] CaptureInputs(IGH_DataAccess da)
        {
            string clientId = "";
            da.GetData(0, ref clientId);
            return new object[] { clientId ?? "" };
        }

        protected override Task<ProjectInfo[]> FetchAsync(SelvagenClient client, object[] inputs)
        {
            string clientId = (string)inputs[0];
            return string.IsNullOrEmpty(clientId)
                ? client.ListProjectsAsync()
                : client.ListProjectsByClientAsync(clientId);
        }

        protected override string GetId(ProjectInfo item) => item.Id;
        protected override string GetDisplayName(ProjectInfo item) => item.Name;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Projects");
    }
}
```

- [ ] **Step 6.2: Build**

```powershell
dotnet build src/Selvagen.GH/Selvagen.GH.csproj
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 6.3: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenProjectsComponent.cs
git commit -m "refactor(gh): port SelvagenProjectsComponent to selectable base; cascade now triggers refetch"
```

---

## Task 7: Refactor SelvagenListAssetsComponent

**Files:**
- Modify: `src/Selvagen.GH/Components/SelvagenListAssetsComponent.cs`

The old component has a third `Types` output (only populated for meshes). The base class fixes a 4-output shape, so this output is dropped. Mention this in the commit message.

- [ ] **Step 7.1: Replace the file**

Overwrite `src/Selvagen.GH/Components/SelvagenListAssetsComponent.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenListAssetsComponent : SelvagenSelectableComponentBase<AssetInfo>
    {
        public SelvagenListAssetsComponent()
            : base("List Assets", "SvAssets",
                "List meshes, curve sets, or text 3D sets for a project. Pick one from the inline dropdown.",
                "Data")
        { }

        public override Guid ComponentGuid => new Guid("A17B2C3D-E4F5-6789-0ABC-DEF123456789");

        protected override void RegisterFilterInputs(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project ID to list assets for", GH_ParamAccess.item, "");
            pManager[0].Optional = true;
            pManager.AddTextParameter("AssetType", "T", "Asset type: meshes, curve_sets, or text_3d_sets", GH_ParamAccess.item, "meshes");
        }

        protected override object[] CaptureInputs(IGH_DataAccess da)
        {
            string projectId = "";
            string assetType = "meshes";
            da.GetData(0, ref projectId);
            da.GetData(1, ref assetType);
            return new object[] { projectId ?? "", assetType ?? "meshes" };
        }

        protected override async Task<AssetInfo[]> FetchAsync(SelvagenClient client, object[] inputs)
        {
            string projectId = (string)inputs[0];
            string assetType = (string)inputs[1];

            if (string.IsNullOrEmpty(projectId)) return new AssetInfo[0];

            switch (assetType.ToLowerInvariant())
            {
                case "meshes":
                case "mesh":
                    return await client.ListMeshesAsync(projectId).ConfigureAwait(false);
                case "curve_sets":
                case "curves":
                    return await client.ListCurveSetsAsync(projectId).ConfigureAwait(false);
                case "text_3d_sets":
                case "labels":
                case "text":
                    return await client.ListText3DSetsAsync(projectId).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown asset type: {assetType}. Use meshes, curve_sets, or text_3d_sets.");
            }
        }

        protected override string GetId(AssetInfo item) => item.Id;
        protected override string GetDisplayName(AssetInfo item) => item.Name;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("ListAssets");
    }
}
```

- [ ] **Step 7.2: Build all targets**

```powershell
dotnet build Selvagen.GH.sln
```

If no solution file, build each project:

```powershell
dotnet build src/Selvagen.Core/Selvagen.Core.csproj
dotnet build src/Selvagen.GH/Selvagen.GH.csproj
dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj
```

Expected: all BUILD SUCCEEDED, all tests PASS.

- [ ] **Step 7.3: Commit**

```powershell
git add src/Selvagen.GH/Components/SelvagenListAssetsComponent.cs
git commit -m "refactor(gh): port SelvagenListAssetsComponent to selectable base; drop Types output for shape parity"
```

---

## Task 8: Integration test scaffolding

**Files:**
- Create: `tests/integration/requirements.txt`
- Create: `tests/integration/conftest.py`
- Create: `tests/integration/run.ps1`
- Create: `tests/integration/README.md`
- Create: `tests/integration/scenarios/__init__.py`

Cordyceps exposes its MCP server on a known port (default 26929) when Grasshopper is open with the Cordyceps component on the canvas. The Python `mcp` SDK speaks HTTP-streamable to it. Tests dispatch tool calls (`gh_canvas`, `gh_wire`, `gh_inspect`) through a pytest fixture that yields a connected `ClientSession`.

- [ ] **Step 8.1: Write `requirements.txt`**

Create `tests/integration/requirements.txt`:

```
mcp>=1.0
pytest>=8
pytest-asyncio>=0.23
```

- [ ] **Step 8.2: Write `conftest.py`**

Create `tests/integration/conftest.py`:

```python
"""Shared pytest fixtures for Cordyceps-driven integration tests."""
import os
import asyncio
import pytest
import pytest_asyncio
from contextlib import AsyncExitStack
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

CORDYCEPS_URL = os.environ.get("CORDYCEPS_URL", "http://localhost:26929/mcp")


@pytest_asyncio.fixture
async def gh():
    """Yields a connected MCP ClientSession to the running Cordyceps server."""
    async with AsyncExitStack() as stack:
        read, write, _ = await stack.enter_async_context(streamablehttp_client(CORDYCEPS_URL))
        session = await stack.enter_async_context(ClientSession(read, write))
        await session.initialize()
        yield session


@pytest.fixture
def credentials():
    """Reads test credentials from env. Skips test if missing."""
    email = os.environ.get("SELVAGEN_TEST_EMAIL")
    password = os.environ.get("SELVAGEN_TEST_PASSWORD")
    if not email or not password:
        pytest.skip("SELVAGEN_TEST_EMAIL / SELVAGEN_TEST_PASSWORD not set")
    return email, password
```

- [ ] **Step 8.3: Write `run.ps1`**

Create `tests/integration/run.ps1`:

```powershell
# Launch Rhino with the bootstrap Grasshopper file, wait for Cordyceps,
# then run pytest. Assumes Rhino 8 is installed at the default location.
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path "$PSScriptRoot/../.."
$bootstrap = Resolve-Path "$PSScriptRoot/bootstrap.gh"
$rhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"

if (-not (Test-Path $rhinoExe)) {
    throw "Rhino 8 not found at $rhinoExe. Edit tests/integration/run.ps1 to point at your install."
}

# Launch Rhino+GH if not already running with Cordyceps responding
$cordycepsUrl = $env:CORDYCEPS_URL
if (-not $cordycepsUrl) { $cordycepsUrl = "http://localhost:26929/mcp" }

function Test-CordycepsAlive {
    try {
        $r = Invoke-WebRequest -Uri $cordycepsUrl -Method POST -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
        return $true
    } catch { return $false }
}

if (-not (Test-CordycepsAlive)) {
    Write-Host "Launching Rhino with bootstrap.gh..."
    Start-Process -FilePath $rhinoExe -ArgumentList "/runscript=`"_-Grasshopper _Open `"$bootstrap`" _Enter`""
    Write-Host "Waiting for Cordyceps server..."
    $waited = 0
    while (-not (Test-CordycepsAlive)) {
        Start-Sleep -Seconds 2
        $waited += 2
        if ($waited -ge 60) { throw "Cordyceps did not come up within 60 seconds." }
    }
}

# Set up Python venv if missing
$venv = Join-Path $PSScriptRoot ".venv"
if (-not (Test-Path $venv)) {
    Write-Host "Creating venv..."
    python -m venv $venv
    & "$venv/Scripts/pip.exe" install -r "$PSScriptRoot/requirements.txt"
}

& "$venv/Scripts/python.exe" -m pytest "$PSScriptRoot/scenarios" -v
```

- [ ] **Step 8.4: Write `README.md`**

Create `tests/integration/README.md`:

```markdown
# Integration tests

Cordyceps-driven end-to-end tests for the Selvagen Grasshopper components.
Full setup and writing-tests guide: [`docs/INTEGRATION_TESTING.md`](../../docs/INTEGRATION_TESTING.md).

## Quick start

```powershell
$env:SELVAGEN_TEST_EMAIL = "you@example.com"
$env:SELVAGEN_TEST_PASSWORD = "secret"
pwsh tests/integration/run.ps1
```
```

- [ ] **Step 8.5: Empty package init**

Create `tests/integration/scenarios/__init__.py` (empty file — marks the directory as a Python package).

- [ ] **Step 8.6: Commit**

```powershell
git add tests/integration/requirements.txt tests/integration/conftest.py tests/integration/run.ps1 tests/integration/README.md tests/integration/scenarios/__init__.py
git commit -m "test: scaffold Cordyceps-driven integration test harness"
```

---

## Task 9: Cascade scenario test

**Files:**
- Create: `tests/integration/scenarios/test_clients_cascade.py`

This is the headline test: confirms that picking a client through the right-click menu cascades a refresh into `List Projects`, and picking a project cascades into `List Assets`.

- [ ] **Step 9.1: Write the test**

Create `tests/integration/scenarios/test_clients_cascade.py`:

```python
"""Cascade: pick client → projects re-fetch → pick project → assets re-fetch."""
import json
import pytest

pytestmark = pytest.mark.asyncio


async def call(gh, tool, **args):
    """Helper: call a Cordyceps tool and return its parsed JSON content."""
    result = await gh.call_tool(tool, args)
    text = result.content[0].text
    try:
        return json.loads(text)
    except (json.JSONDecodeError, ValueError):
        return text


async def test_cascade_clients_projects_assets(gh, credentials):
    email, password = credentials

    # Clean canvas
    await call(gh, "gh_document", action="clear")

    # Place Login + provide credentials and trigger
    login = await call(gh, "gh_canvas", action="add", name="Selvagen/Login", x=100, y=100)
    email_panel = await call(gh, "gh_canvas", action="constant", text=email, x=-100, y=80)
    pw_panel = await call(gh, "gh_canvas", action="constant", text=password, x=-100, y=120)
    login_toggle = await call(gh, "gh_canvas", action="constant", value=True, x=-100, y=160)
    await call(gh, "gh_wire", action="connect", source=email_panel["guid"], target=login["guid"], target_input=0)
    await call(gh, "gh_wire", action="connect", source=pw_panel["guid"], target=login["guid"], target_input=1)
    await call(gh, "gh_wire", action="connect", source=login_toggle["guid"], target=login["guid"], target_input=2)

    # Place List Clients, List Projects, List Assets
    clients = await call(gh, "gh_canvas", action="add", name="Selvagen/List Clients", x=400, y=100)
    projects = await call(gh, "gh_canvas", action="add", name="Selvagen/List Projects", x=700, y=100)
    assets = await call(gh, "gh_canvas", action="add", name="Selvagen/List Assets", x=1000, y=100)

    # Wire SelectedID outputs forward
    await call(gh, "gh_wire", action="connect", source=clients["guid"], source_output=0,
               target=projects["guid"], target_input=0)
    await call(gh, "gh_wire", action="connect", source=projects["guid"], source_output=0,
               target=assets["guid"], target_input=0)

    # Solve
    await call(gh, "gh_document", action="recompute")

    # Read clients output: IDs at port 2, Names at port 3
    out = await call(gh, "gh_inspect", action="outputs", guid=clients["guid"])
    client_ids = out["outputs"][2]["values"]
    assert len(client_ids) > 0, "Expected at least one client; check test account"
    first_client = client_ids[0]

    # Pick first client via right-click menu mirror
    await call(gh, "gh_canvas", action="set", guid=clients["guid"],
               menu=f"Select/{out['outputs'][3]['values'][0]}")

    await call(gh, "gh_document", action="recompute")

    # Verify cascade: List Projects should now have ClientID = first_client and have re-fetched
    proj_out = await call(gh, "gh_inspect", action="outputs", guid=projects["guid"])
    # Either non-empty IDs, or no warnings about errors
    proj_ids = proj_out["outputs"][2]["values"]

    msgs = await call(gh, "gh_inspect", action="status", guid=projects["guid"])
    errors = [m for m in msgs.get("messages", []) if m.get("level") == "Error"]
    assert not errors, f"Projects component reported errors: {errors}"

    if proj_ids:
        # Pick the first project and verify assets cascade
        proj_names = proj_out["outputs"][3]["values"]
        await call(gh, "gh_canvas", action="set", guid=projects["guid"],
                   menu=f"Select/{proj_names[0]}")
        await call(gh, "gh_document", action="recompute")

        asset_msgs = await call(gh, "gh_inspect", action="status", guid=assets["guid"])
        asset_errors = [m for m in asset_msgs.get("messages", []) if m.get("level") == "Error"]
        assert not asset_errors, f"Assets component reported errors: {asset_errors}"
```

> **Note for executor:** the exact tool argument names (`menu=`, `target_input=`, etc.) follow the Cordyceps API as documented in its `gh_canvas`/`gh_wire`/`gh_inspect` `help` actions. If the actual tool surface differs, run `await gh.call_tool("gh_canvas", {"action": "help"})` and adjust the wrappers above to match. The Cordyceps `set` action with a `menu` arg is what mirrors a right-click→submenu→item click.

- [ ] **Step 9.2: Skip-by-default — no execution attempt yet**

The test will not run without a live Rhino+Cordyceps environment. We commit it now and let the user run it when ready.

- [ ] **Step 9.3: Commit**

```powershell
git add tests/integration/scenarios/test_clients_cascade.py
git commit -m "test(integration): add clients→projects→assets cascade scenario"
```

---

## Task 10: Persistence scenario test

**Files:**
- Create: `tests/integration/scenarios/test_persistence.py`

- [ ] **Step 10.1: Write the test**

Create `tests/integration/scenarios/test_persistence.py`:

```python
"""Saving the file with a selected client preserves the choice on reopen."""
import json
import os
import tempfile
import pytest

pytestmark = pytest.mark.asyncio


async def call(gh, tool, **args):
    result = await gh.call_tool(tool, args)
    text = result.content[0].text
    try:
        return json.loads(text)
    except (json.JSONDecodeError, ValueError):
        return text


async def test_selection_persists_across_save_and_reopen(gh, credentials):
    email, password = credentials

    await call(gh, "gh_document", action="clear")

    # Login
    login = await call(gh, "gh_canvas", action="add", name="Selvagen/Login", x=100, y=100)
    e = await call(gh, "gh_canvas", action="constant", text=email, x=-100, y=80)
    p = await call(gh, "gh_canvas", action="constant", text=password, x=-100, y=120)
    t = await call(gh, "gh_canvas", action="constant", value=True, x=-100, y=160)
    await call(gh, "gh_wire", action="connect", source=e["guid"], target=login["guid"], target_input=0)
    await call(gh, "gh_wire", action="connect", source=p["guid"], target=login["guid"], target_input=1)
    await call(gh, "gh_wire", action="connect", source=t["guid"], target=login["guid"], target_input=2)

    # List Clients + pick first
    clients = await call(gh, "gh_canvas", action="add", name="Selvagen/List Clients", x=400, y=100)
    await call(gh, "gh_document", action="recompute")

    out = await call(gh, "gh_inspect", action="outputs", guid=clients["guid"])
    names = out["outputs"][3]["values"]
    ids = out["outputs"][2]["values"]
    assert len(names) > 0

    chosen_id = ids[0]
    chosen_name = names[0]
    await call(gh, "gh_canvas", action="set", guid=clients["guid"], menu=f"Select/{chosen_name}")
    await call(gh, "gh_document", action="recompute")

    # Save to a temp .gh file
    with tempfile.TemporaryDirectory() as td:
        save_path = os.path.join(td, "persist.gh")
        await call(gh, "gh_document", action="save", path=save_path)

        # Clear and reopen
        await call(gh, "gh_document", action="clear")
        await call(gh, "gh_document", action="open", path=save_path)

    # After reopen, the SelectedID output should still be the chosen client.
    # Need to log in again because client cache lives only in memory.
    await call(gh, "gh_document", action="recompute")
    out2 = await call(gh, "gh_inspect", action="outputs", guid=clients["guid"])
    assert out2["outputs"][0]["values"][0] == chosen_id, \
        f"Expected persisted SelectedID {chosen_id}, got {out2['outputs'][0]['values'][0]}"
```

- [ ] **Step 10.2: Commit**

```powershell
git add tests/integration/scenarios/test_persistence.py
git commit -m "test(integration): add save/reopen persistence scenario"
```

---

## Task 11: Missing-item warning scenario test

**Files:**
- Create: `tests/integration/scenarios/test_missing_item.py`

- [ ] **Step 11.1: Write the test**

Create `tests/integration/scenarios/test_missing_item.py`:

```python
"""When the persisted ID no longer matches any item, a warning surfaces."""
import json
import pytest

pytestmark = pytest.mark.asyncio


async def call(gh, tool, **args):
    result = await gh.call_tool(tool, args)
    text = result.content[0].text
    try:
        return json.loads(text)
    except (json.JSONDecodeError, ValueError):
        return text


async def test_missing_persisted_id_warns_and_clears(gh, credentials):
    """We simulate a stale persisted ID by directly setting it via the Cordyceps
    component-state surface (or, fallback: edit the saved .gh file before reopen).

    NOTE: Cordyceps' state-injection surface for custom component fields may
    require extending the integration test bootstrap. If `gh_canvas action=set
    field=...` is not supported for the persisted SelectedId, this test should
    be marked xfail with a TODO until the surface exists.
    """
    email, password = credentials

    await call(gh, "gh_document", action="clear")

    # Login
    login = await call(gh, "gh_canvas", action="add", name="Selvagen/Login", x=100, y=100)
    e = await call(gh, "gh_canvas", action="constant", text=email, x=-100, y=80)
    p = await call(gh, "gh_canvas", action="constant", text=password, x=-100, y=120)
    t = await call(gh, "gh_canvas", action="constant", value=True, x=-100, y=160)
    await call(gh, "gh_wire", action="connect", source=e["guid"], target=login["guid"], target_input=0)
    await call(gh, "gh_wire", action="connect", source=p["guid"], target=login["guid"], target_input=1)
    await call(gh, "gh_wire", action="connect", source=t["guid"], target=login["guid"], target_input=2)

    clients = await call(gh, "gh_canvas", action="add", name="Selvagen/List Clients", x=400, y=100)

    # Try to set the SelectedId field to an id we know cannot exist.
    # If the action is unsupported, mark xfail.
    try:
        await call(gh, "gh_canvas", action="set", guid=clients["guid"],
                   field="SelectedId", value="00000000-0000-0000-0000-000000000000")
    except Exception as ex:
        pytest.xfail(f"Cordyceps does not support field-set on this component: {ex}")

    await call(gh, "gh_document", action="recompute")

    msgs = await call(gh, "gh_inspect", action="status", guid=clients["guid"])
    warnings = [m for m in msgs.get("messages", []) if m.get("level") == "Warning"]
    assert any("no longer exists" in (m.get("text") or "").lower() for m in warnings), \
        f"Expected 'no longer exists' warning, got messages: {msgs}"

    # SelectedID output should be empty after reconciliation cleared the stale id
    out = await call(gh, "gh_inspect", action="outputs", guid=clients["guid"])
    assert out["outputs"][0]["values"][0] == "", "Expected SelectedID cleared after reconciliation"
```

- [ ] **Step 11.2: Commit**

```powershell
git add tests/integration/scenarios/test_missing_item.py
git commit -m "test(integration): add missing-persisted-id warning scenario"
```

---

## Task 12: Integration testing developer doc

**Files:**
- Create: `docs/INTEGRATION_TESTING.md`

- [ ] **Step 12.1: Write the doc**

Create `docs/INTEGRATION_TESTING.md`:

```markdown
# Integration testing

End-to-end tests for the Selvagen Grasshopper components, driven through the
Cordyceps MCP server. Two test layers exist:

1. **Unit tests** (`tests/Selvagen.Core.Tests/`) — pure C# / xUnit, no Rhino.
   Cover the cache-decision and reconciliation helpers in `Selvagen.Core.Components`.
2. **Integration tests** (`tests/integration/`) — Python / pytest, drive a live
   Rhino + Grasshopper instance through Cordyceps to exercise the actual
   component lifecycle, persistence, and cascade behaviour.

We need the integration layer because the inline-dropdown UI, the cascade
between components, and the .gh save/load round-trip cannot be validated
without a real Grasshopper runtime.

## Prerequisites

- Windows 10/11 or macOS 13+
- Rhino 8.21 or newer
- [Cordyceps](https://github.com/Cordyceps-MCP/cordyceps) installed as a
  Grasshopper plugin and registered with your Claude Code MCP config
- The Selvagen plugin built and copied to `%APPDATA%\Grasshopper\Libraries\` (Windows)
  or `~/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper/Libraries/` (macOS)
- Python 3.10+
- A test Selvagen account with read access to at least one client and project
- `tests/integration/bootstrap.gh` — a hand-saved .gh file containing only a
  Cordyceps component (already committed)

## One-time setup

1. Install Cordyceps into Grasshopper following its README.
2. Register Cordyceps with Claude Code (or add its server URL to the
   `CORDYCEPS_URL` environment variable). Default URL: `http://localhost:26929/mcp`.
3. Build the Selvagen plugin: `dotnet build src/Selvagen.GH/Selvagen.GH.csproj`
   and copy the resulting `.gha` to your Grasshopper Libraries folder.
4. Set test credentials in your shell:

   ```powershell
   $env:SELVAGEN_TEST_EMAIL = "you@example.com"
   $env:SELVAGEN_TEST_PASSWORD = "your-test-password"
   ```

## Running tests

From PowerShell at the repo root:

```powershell
pwsh tests/integration/run.ps1
```

The launcher will:

1. Open Rhino with `bootstrap.gh` if Cordyceps is not already responding.
2. Wait up to 60 s for the Cordyceps HTTP server.
3. Create a Python venv at `tests/integration/.venv` if missing.
4. Run `pytest tests/integration/scenarios -v`.

From within Claude Code: ask Claude to run the integration suite. It will
shell out to `run.ps1`.

## Writing a new scenario

1. Create a new file under `tests/integration/scenarios/`, named
   `test_<short-description>.py`.
2. Use the `gh` and `credentials` fixtures from `conftest.py`.
3. Wrap tool calls in the `call(gh, "...", **args)` helper to parse JSON results.
4. Always start with `await call(gh, "gh_document", action="clear")` to wipe
   the canvas before adding components.
5. Inspect outputs via `gh_inspect action=outputs guid=...`. Output ports for
   selectable components are: 0=SelectedID, 1=SelectedName, 2=IDs (list),
   3=Names (list).

Skeleton:

```python
import json, pytest
pytestmark = pytest.mark.asyncio

async def call(gh, tool, **args):
    r = await gh.call_tool(tool, args)
    try: return json.loads(r.content[0].text)
    except: return r.content[0].text

async def test_my_thing(gh, credentials):
    email, password = credentials
    await call(gh, "gh_document", action="clear")
    # … place components, wire, recompute, inspect …
```

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `ConnectionError` on `streamablehttp_client` | Cordyceps not running; open `bootstrap.gh` in Grasshopper. |
| `pytest.skip("SELVAGEN_TEST_EMAIL not set")` | Env vars missing in the shell that ran `run.ps1`. |
| Components show `"Not logged in"` | Login component never solved; verify Login toggle is `True`. |
| `Selvagen/List Clients` not found | Plugin not deployed; rebuild and copy `.gha` to Libraries. |
| Right-click `Select/<name>` returns "menu item not found" | The display name has special characters — verify `out["outputs"][3]["values"][0]` matches exactly, including casing. |

## Out of scope (follow-ups)

- Headless Rhino on CI. Rhino can be scripted from CLI but integration with
  GitHub Actions runners requires either a self-hosted Windows runner or a
  Rhino.Compute container; tracked separately.
```

- [ ] **Step 12.2: Commit**

```powershell
git add docs/INTEGRATION_TESTING.md
git commit -m "docs: add INTEGRATION_TESTING guide"
```

---

## Task 13: README updates

**Files:**
- Modify: `README.md`

The repo's working tree shows `README.md` already has uncommitted edits — read the current state before patching, and produce a single commit that covers (a) any in-progress edits the user wants kept, (b) the framework-target fix, and (c) the integration-tests pointer.

- [ ] **Step 13.1: Read current README**

```powershell
# View what's currently committed:
git show HEAD:README.md
# View the working-tree version with uncommitted changes:
```

Then `Read` the file at `C:\repos\selvagen-gh\README.md`.

- [ ] **Step 13.2: Fix the framework-target line**

Locate any text claiming `net48 + net7.0` and replace with `net48 + net8.0` (or `net48 + net8.0 + net8.0-windows`, whichever phrasing matches the file's prose). Use Edit with full surrounding context to make the replacement unique. Leave any other in-progress README edits alone.

- [ ] **Step 13.3: Add Running Integration Tests section**

After the existing "Building" or "Running" section (whichever comes last), append:

```markdown
## Running integration tests

End-to-end tests drive a live Grasshopper instance through the Cordyceps MCP
server. See [`docs/INTEGRATION_TESTING.md`](docs/INTEGRATION_TESTING.md) for
prerequisites and setup. Quick run:

```powershell
$env:SELVAGEN_TEST_EMAIL = "you@example.com"
$env:SELVAGEN_TEST_PASSWORD = "your-test-password"
pwsh tests/integration/run.ps1
```
```

- [ ] **Step 13.4: Commit**

```powershell
git add README.md
git commit -m "docs(readme): fix target framework matrix and add integration tests pointer"
```

---

## Task 14: End-to-end smoke verification

After all code is committed, perform a manual smoke test.

- [ ] **Step 14.1: Final build + tests**

```powershell
dotnet build src/Selvagen.Core/Selvagen.Core.csproj
dotnet build src/Selvagen.GH/Selvagen.GH.csproj
dotnet test tests/Selvagen.Core.Tests/Selvagen.Core.Tests.csproj
```

Expected: all build, all tests pass.

- [ ] **Step 14.2: Manual UI smoke test**

1. Copy the built `.gha` to the Grasshopper Libraries folder.
2. Open Rhino + Grasshopper with a fresh document.
3. Place `Selvagen/Login`, log in.
4. Place `Selvagen/List Clients`. Confirm the inline dropdown appears below the input row, shows `"Loading…"` then `"— Select —"`.
5. Click the dropdown — confirm a popup with all clients appears.
6. Pick one — confirm `SelectedID` and `SelectedName` outputs populate.
7. Place `Selvagen/List Projects`, wire its `ClientID` input from `List Clients.SelectedID`. Confirm projects list re-fetches when you pick a different client.
8. Place `Selvagen/List Assets`, wire its `ProjectID` input from `List Projects.SelectedID`. Confirm assets re-fetch when you pick a different project.
9. Save the .gh file, close Grasshopper, reopen — confirm the selected client/project/asset persist (after re-login, since cached items aren't persisted).
10. Right-click any selectable component and confirm the `Select` submenu mirrors the inline dropdown.

- [ ] **Step 14.3: Report results**

If any UI step fails, file an issue with reproduction steps; do **not** silently fix in this branch. The user reviews the smoke before declaring the feature done.

---

## Self-review (against the spec)

The plan covers:

| Spec section | Tasks |
|---|---|
| Pure helpers (`CacheDecision`, `Reconcile`) + tests | 1 |
| `SelvagenSelectableComponentBase<TItem>` | 3 |
| `SelvagenSelectorAttributes` (inline dropdown) | 4 |
| Output port reshape (4 ports, singletons first) | 3 (RegisterOutputParams) |
| Right-click menu mirror | 3 (AppendAdditionalComponentMenuItems) |
| Two-phase `SolveInstance` via `GH_TaskCapableComponent` | 3 |
| Cache invalidation by key change | 1 + 3 |
| `_lastFetchError` + keep stale data on error | 3 |
| `_selectedId` reconciliation + Warning + `<missing item>` | 3 + 11 |
| Persistence (`Read`/`Write`) | 3 + 10 |
| `SelvagenClientsComponent` refactor | 5 |
| `SelvagenProjectsComponent` refactor (keeps optional `ClientID`) | 6 |
| `SelvagenListAssetsComponent` refactor (drops `Types` output) | 7 |
| Integration scaffolding (run.ps1, conftest, requirements) | 8 |
| Cascade scenario | 9 |
| Persistence scenario | 10 |
| Missing-item scenario | 11 |
| `docs/INTEGRATION_TESTING.md` | 12 |
| `README.md` framework-target fix + integration pointer | 13 |
| Manual UI smoke (residual non-automatable testing) | 14 |

No spec sections without a task.
