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
