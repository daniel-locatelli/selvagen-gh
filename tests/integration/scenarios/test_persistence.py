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
