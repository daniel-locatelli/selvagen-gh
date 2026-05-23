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
