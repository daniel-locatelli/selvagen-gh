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
