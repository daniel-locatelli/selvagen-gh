# Grasshopper dropdown selectors with cascade updates — design

**Status:** draft — awaiting written review
**Date:** 2026-05-14
**Scope:** `Selvagen.GH` data components (`SelvagenClientsComponent`, `SelvagenProjectsComponent`, `SelvagenListAssetsComponent`)

---

## Problem

Two user complaints from a recent round of feedback:

1. **Selection workflow is clunky.** To pick a client, the user must read the `IDs`/`Names` list outputs of `List Clients`, manually wire them into a Grasshopper `Value List` (or similar), and then wire that into `List Projects.ClientID`. Three steps for what feels like one decision.

2. **Cascade is broken.** When the user *does* change which `ClientID` is fed into `List Projects`, the projects list does not refresh — because `_cachedProjects` is only invalidated by the `Refresh` boolean toggle, not by changes to the `ClientID` input value. The same bug affects `List Projects → List Assets`.

## Goals

- Each data component (`Clients`, `Projects`, `ListAssets`) has an **inline dropdown** on its canvas face that lets the user pick one item directly, plus a **right-click context-menu mirror** of the same selection.
- Selecting in the dropdown **cascades**: downstream components automatically re-fetch with the new filter and re-populate their own dropdowns.
- Selection **persists**: saving the .gh file remembers which item was picked; reopening the file restores the selection. If the item no longer exists on the server, the component warns and clears the selection.
- The cache-invalidation bug is fixed across all three components, not just the one the user complained about.
- Existing scripted users who consume the `IDs` / `Names` list outputs still have them available.

## Non-goals

- Replacing the existing `Refresh` boolean toggle. It stays as a manual force-refresh.
- Adding any kind of "create new client" / "create new project" capability from within Grasshopper.
- Supporting multi-select. One dropdown picks one item.
- Auto-selecting the first item when nothing is picked. Selection must be explicit so opening an old definition doesn't silently trigger a real API call against a randomly-chosen client.

---

## Architecture

Two new shared classes live in `src/Selvagen.GH/Components/`:

```
SelvagenSelectableComponentBase<TItem>   : GH_TaskCapableComponent<TItem[]>
    └── owns: cached items array, selected ID, cache key, persistence,
              fetch lifecycle, output emission
SelvagenSelectorAttributes               : GH_ComponentAttributes
    └── owns: inline dropdown layout/rendering, mouse hit-testing,
              ToolStripDropDown menu construction
```

The three concrete components are refactored to inherit from the base:

```
SelvagenClientsComponent     → fetches via client.ListClientsAsync()
SelvagenProjectsComponent    → fetches via client.ListProjectsByClientAsync(clientId)
SelvagenListAssetsComponent  → fetches via client.ListAssetsAsync(projectId, assetType)
```

Each concrete class supplies four hooks to the base:

- `FetchAsync(SelvagenClient client, IGH_DataAccess da) → Task<TItem[]>` — how to load items.
- `GetId(TItem item) → string` — extract the ID from one item.
- `GetDisplayName(TItem item) → string` — extract the user-visible name.
- `GetCacheKey(IGH_DataAccess da) → object[]` — list of input values that, when changed, invalidate the cached items.

`GetCacheKey` returns:
- `[]` for `Clients` (no filtering inputs).
- `[clientId]` for `Projects`.
- `[projectId, assetType]` for `List Assets`.

The base class is generic over `TItem` so we can share code without losing type information about the model objects (`FirmInfo`, `ProjectInfo`, asset DTOs).

---

## Component outputs

All three components share the same output shape. **Singletons-first** — this is a breaking change to the order of existing outputs, accepted because the plugin is young and users will rewire.

| Port | Name | Nickname | Type | Notes |
|---|---|---|---|---|
| 0 | `SelectedID` | `ID` | Text (item) | The picked item's UUID. Empty string if nothing picked. |
| 1 | `SelectedName` | `Name` | Text (item) | The picked item's display name. Empty string if nothing picked. |
| 2 | `IDs` | `IDs` | Text (list) | All UUIDs (was port 0 in the old design). |
| 3 | `Names` | `Names` | Text (list) | All display names (was port 1 in the old design). |

`List Projects` keeps its optional `ClientID` input parameter so users who prefer the wired-input style can still use it; the dropdown is an alternative to that, not a replacement.

---

## Inline dropdown UI

`SelvagenSelectorAttributes` overrides `Layout()` and `Render()` from `GH_ComponentAttributes`.

### Layout

```
┌──────────────────────────────┐
│  ●  List Clients           ● │  ← input row (Refresh)
│                              │
│   ┌──────────────────────┐   │
│   │  ▼  Acme Engineering │   │  ← dropdown row: 22 px tall, full inner width
│   └──────────────────────┘   │
│                              │
│ ─●  SelectedID               │
│ ─●  SelectedName             │  ← output rows, shifted down by ~26 px
│ ─●  IDs                      │
│ ─●  Names                    │
└──────────────────────────────┘
```

The attributes class calls `base.Layout()` to get the default rectangles, expands the component bounds by ~26 px (22 row + 4 padding) below the input row, and shifts the output anchors down by the same amount.

### Render

On `GH_CanvasChannel.Objects`:

1. Call `base.Render(canvas, graphics, channel)` for the standard component chrome.
2. Draw the dropdown rectangle using `GH_Capsule` (Grasshopper's standard rounded-rect primitive — gives the widget the same look as a Value List).
3. Draw a `▼` glyph on the left.
4. Draw the display text. The display text is one of:
   - The selected item's `GetDisplayName(item)`.
   - `"— Select —"` if items have been loaded but nothing is picked.
   - `"Not logged in"` if `SessionManager.Current == null`.
   - `"Loading…"` if a fetch task is currently in flight.
   - `"<missing item>"` (italic) if the persisted `_selectedId` does not match any current item.
5. Truncate the text with ellipsis if it overflows the rectangle.

### Mouse handling

`RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)`:

- If left button **and** the mouse is inside the dropdown rectangle:
  - If `_cachedItems` is null or empty: do nothing (the popup would be useless), return `Handled`.
  - Otherwise build a `ToolStripDropDown` with one item per cached entry, bold-face the currently-selected one, attach a click handler that calls `Owner.SetSelectedId(id)`.
  - Anchor the popup at the bottom-left of the dropdown rect (Grasshopper's canvas → screen coordinate conversion).
  - Return `GH_ObjectResponse.Handled`.
- Otherwise fall through to `base.RespondToMouseDown` so context menu, dragging, etc. still work.

### Right-click context menu mirror

In the base component, override `AppendAdditionalComponentMenuItems(ToolStripDropDown menu)`:

- Insert a "Select" submenu near the top of the right-click menu.
- The submenu mirrors the dropdown popup exactly: one item per cached entry, bold-face the current selection, on-click calls `SetSelectedId`.
- If `_cachedItems` is null or empty, replace the submenu with a single disabled `"(no items)"` item.

The right-click menu exists primarily so MCP automation (Cordyceps) can drive selection — Cordyceps operates at component-data level and cannot click custom-painted canvas widgets, but it can invoke component menu items.

---

## Data flow & cache lifecycle

### State on the base class

```
_cachedItems       : TItem[]       null until first successful fetch
_cachedKey         : object[]      last cache-key tuple that produced _cachedItems
_selectedId        : string        persisted; survives file save/load; null when nothing picked
_refreshWasTrue    : bool          edge detector for the manual Refresh toggle
_lastFetchError    : string        most recent fetch error message, or null
```

### `SolveInstance` — two-phase via `GH_TaskCapableComponent<TItem[]>`

Grasshopper calls `SolveInstance` twice per fetch: once with `InPreSolve == true` to enlist the task, then again with `InPreSolve == false` after the solver has awaited the task.

**InPreSolve phase:**

1. Bail early if `SessionManager.Current == null`: set a Warning runtime message, leave outputs empty, return.
2. Read inputs into local vars (`refresh`, plus any filter inputs like `clientId`, `assetType`).
3. Compute `currentKey = GetCacheKey(DA)`.
4. Decide `needsFetch`:
   - `_cachedItems == null`, OR
   - `!SequenceEqual(currentKey, _cachedKey)` (input value changed → cache stale), OR
   - `refresh && !_refreshWasTrue` (edge detect on the Refresh boolean).
5. Update `_refreshWasTrue = refresh`.
6. If `needsFetch`: enlist `TaskList.Add(Task.Run(() => FetchAsync(client, da)))`.

**Solve phase:**

1. If a task was enlisted: `GetSolveResults(DA, out var items)` — Grasshopper has already awaited it.
2. If `items != null`:
   - `_cachedItems = items; _cachedKey = currentKey;`
   - Reconcile `_selectedId` (see below).
3. Set outputs from `_cachedItems` and `_selectedId`.

### Reconciliation of `_selectedId`

After every successful fetch:

- `_selectedId == null` → leave null. Status shows `"— Select —"`.
- `_selectedId != null` and an item in `_cachedItems` has matching ID → keep it.
- `_selectedId != null` and no match → null out `_selectedId`, raise a Warning runtime message `"Previously-selected item no longer exists"`, render the dropdown as `"<missing item>"` until the user picks again. **Never auto-pick** — silent re-selection on the user's behalf would be confusing.

### Selection from the dropdown

```
public void SetSelectedId(string id)
{
    if (id == _selectedId) return;          // no-op: don't cascade for re-clicking the same item
    _selectedId = id;
    ExpireSolution(recompute: true);        // re-solves this component, cascades downstream
}
```

The downstream cascade is automatic. Grasshopper's solver sees outputs change and marks dependents dirty; the next solve pass on `List Projects` runs `SolveInstance`, computes its new `currentKey`, sees the mismatch with `_cachedKey`, and re-fetches.

### Cascade chain in action

```
User picks "Acme" in List Clients dropdown
  ↓
ListClients.SetSelectedId("acme-uuid") → ExpireSolution(true)
  ↓
ListClients.SolveInstance → Port 0 SelectedID = "acme-uuid"
  ↓
Solver detects ListProjects.ClientID input changed (wired to ListClients.Port 0)
  ↓
ListProjects.SolveInstance:
    currentKey = ["acme-uuid"]
    _cachedKey = ["old-uuid"]
    needsFetch = true (key mismatch)
    InPreSolve: enlists FetchAsync(client, "acme-uuid")
    Solve: receives projects[], reconciles _selectedId (likely no match → null),
           sets outputs, dropdown re-renders with new project list
  ↓
User picks a project → same cascade fires into ListAssets
```

---

## Persistence

```
public override bool Write(GH_IO.Serialization.GH_IWriter writer)
{
    if (!base.Write(writer)) return false;
    if (_selectedId != null) writer.SetString("SelectedId", _selectedId);
    return true;
}

public override bool Read(GH_IO.Serialization.GH_IReader reader)
{
    if (!base.Read(reader)) return false;
    string id = null;
    reader.TryGetString("SelectedId", ref id);
    _selectedId = id;
    return true;
}
```

`_cachedItems` is **not** persisted. On file reopen:

1. Component loads with `_selectedId` restored from the .gh file.
2. Dropdown shows `"Not logged in"` until a Login component runs upstream.
3. After login, next solve fetches the item list, reconciles `_selectedId` against the fresh list, and renders the dropdown accordingly.

---

## Error handling

**Fetch errors (network, 4xx, 5xx):**
- Caught inside the task body, stashed in `_lastFetchError`.
- Solve phase reads `_lastFetchError`, sets an Error runtime message, and keeps the existing `_cachedItems` intact. The user sees the error in the canvas tooltip and the dropdown remains usable with stale data until the next successful refresh. (A flaky network shouldn't wipe a user's workflow.)
- Inner-exception flattening preserved from the current code: `ex.InnerException?.Message ?? ex.Message`.

**Persisted ID no longer matches any item:**
- See reconciliation above. Null out + Warning + `"<missing item>"` rendering.

**Empty result set (logged in, but no clients / projects / assets):**
- `_cachedItems = new TItem[0]`. No warning — empty is a valid state.
- Dropdown shows `"— Select —"` but clicking it shows a `ToolStripDropDown` with one disabled item `"(no items)"` so the popup isn't confusingly blank.

---

## Testing

### Unit tests (`Selvagen.Core.Tests`, net8.0, xUnit)

Lift the cache decision and reconciliation logic out of the Grasshopper component into pure helpers so they can be tested without a GH runtime. Two helpers in a new file `Selvagen.Core/Components/SelectorCache.cs`:

```
public static class CacheDecision
{
    public static bool NeedsFetch(
        object[] cachedItems,   // null or array
        object[] cachedKey,
        object[] currentKey,
        bool refresh,
        bool refreshWasTrue);
}

public static class Reconcile
{
    public static string SelectId<T>(
        IEnumerable<T> items,
        string persistedId,
        Func<T, string> getId);   // returns persistedId if found, else null
}
```

Tests cover:
- First solve (`cachedItems == null` → needs fetch).
- Same key, no refresh (no fetch).
- Different key (needs fetch).
- Refresh edge: `false→true` triggers, `true→true` does not.
- Reconcile: id present, id missing, id null, items empty.

These live in `tests/Selvagen.Core.Tests/Components/SelectorCacheTests.cs`.

### Integration tests (`tests/integration/`, Cordyceps-driven)

Cordyceps is an MCP server for Grasshopper that exposes component graph mutation (add components, set values, wire ports) and inspection (read outputs, runtime messages, snapshots). It cannot click custom canvas widgets, but it can invoke right-click context-menu items — which is why we mirror the dropdown selection in the menu.

Folder layout:

```
tests/integration/
  bootstrap.gh                  # one-time hand-saved file with Cordyceps component on canvas
  run.ps1                       # PowerShell launcher
  scenarios/
    test_clients_cascade.py     # Login → pick client → pick project → check assets
    test_persistence.py         # save file, reopen, selection restored
    test_missing_item.py        # selected client deleted server-side → warning surfaces
  conftest.py                   # async MCP ClientSession fixture
  requirements.txt              # mcp, pytest, pytest-asyncio
  README.md                     # short pointer to docs/INTEGRATION_TESTING.md
```

Stack: pytest + pytest-asyncio + the official `mcp` Python SDK. `run.ps1` launches Rhino with `-runscript=_-Grasshopper _Open <bootstrap.gh> _Enter`, waits for the Cordyceps HTTP server (port 26929) to respond, then `pytest tests/integration/scenarios/`.

Credentials come from environment variables `SELVAGEN_TEST_EMAIL` and `SELVAGEN_TEST_PASSWORD`. The Supabase URL/anon key already live in `%APPDATA%\Selvagen\selvagen.config.json`.

### Manual verification (residual)

Two things remain non-automatable:
- The inline dropdown's *visual* rendering (pixel-level).
- The inline dropdown's *click behaviour* (pixel-level mouse simulation on canvas).

Both share the `SetSelectedId` code path with the right-click menu, which **is** automated — so what remains is just confirming "the widget looks right and clicks open the menu." Quick smoke test, no formal harness needed.

---

## Developer documentation

New file: `docs/INTEGRATION_TESTING.md`. Sections:

1. **Overview** — One paragraph: "what this is and why we have two test layers."
2. **Prerequisites** — Rhino 8.21+ on Windows or macOS, Cordyceps installed, Selvagen plugin deployed, test credentials in env vars, `bootstrap.gh` committed.
3. **One-time setup** — Numbered steps for installing Cordyceps, hand-creating `bootstrap.gh`, registering the MCP server with Claude Code, setting env vars.
4. **Running tests** — `pwsh tests/integration/run.ps1`. From Claude Code: ask Claude to run the integration suite.
5. **Writing a new test** — Skeleton example using `ClientSession`, `gh_canvas`, `gh_wire`, `gh_inspect`.
6. **Troubleshooting** — Common MCP / port / auth failures.

Also update `README.md`:
- Fix stale "net48 + net7.0" claim (actual targets are `net48` + `net8.0` / `net8.0-windows`).
- Add a "Running Integration Tests" paragraph pointing at the new doc.

CI is out of scope. Rhino-on-CI is solvable but a separate decision; we'll capture it as a follow-up.

---

## Breaking changes

1. **Output port order changes.** Old ports `0=IDs (list), 1=Names (list)` become `0=SelectedID (item), 1=SelectedName (item), 2=IDs (list), 3=Names (list)`. Existing .gh files wired against port 0 expecting a list will need rewiring. The plugin is young; acceptable.

2. **Implicit user contract: explicit selection.** Old components implicitly assumed downstream consumers iterated over the lists. New components default to "no selection" until the user picks. Wiring `SelectedID` into a downstream `ProjectID` will pass an empty string until the user clicks the dropdown — at which point the cascade fires. Downstream components must handle empty-string inputs gracefully (they already do).

---

## Open follow-ups (out of scope here)

- Headless Rhino on CI for integration tests.
- A similar selector pattern for `SelvagenLoginComponent` (e.g., a saved-credentials dropdown).
- README updates for the corrected target framework matrix.
