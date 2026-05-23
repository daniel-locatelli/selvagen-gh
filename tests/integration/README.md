# Integration tests

Cordyceps-driven end-to-end tests for the Selvagen Grasshopper components.
Full setup and writing-tests guide: [`docs/INTEGRATION_TESTING.md`](../../docs/INTEGRATION_TESTING.md).

## Quick start

```powershell
$env:SELVAGEN_TEST_EMAIL = "you@example.com"
$env:SELVAGEN_TEST_PASSWORD = "secret"
pwsh tests/integration/run.ps1
```
