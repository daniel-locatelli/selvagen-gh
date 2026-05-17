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
