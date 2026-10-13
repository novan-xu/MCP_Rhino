# Short offline copy names

The user-requested name is `<stem>_YYYYMMDD_HHMMSS.3dm`, using the host's local date and 24-hour time with seconds. Both bounded JS and native Rhino publish through the shared writer. GUIDs are retained only in private staging filenames. Existing new/legacy suffixes are stripped when making another copy.

Run from the repository root:

```powershell
& Project_Test/261010_TEST_offline-copy-naming/Run-Validation.ps1
```

The existing offline copy smoke owns the regression assertions: exact short shape and local time, preview metadata, repeated edits without suffix accumulation, four concurrent copies, preservation of previous output bytes, migration of old long suffixes, and cancellation while timestamp names are occupied. Its separate fixture preoccupies current/near-future names and verifies that cancellation preserves every sentinel and the source and removes the private staging file. Existing script/copy/token/MCP safety tests run alongside those assertions.

The native smoke in `../261010_TEST_offline-rhino-dwg/OfflineRhinoSmoke.csx` also checks the short suffix. It can run against a staged Router:

```powershell
dotnet run --project Project_Test/261010_TEST_offline-rhino-dwg/OfflineRhinoSmoke.csproj -c Release -- Release Packaging/MCP_Rhino/artifacts/MCP_Rhino-1.3.5/bin/MCP_Rhino.Router.exe
```

Private rename evidence is stored under ignored `Runtime_Test/local/offline-copy-naming/renamed-copy.json`. The remaining user output was renamed from its legacy suffix to `_20261010_223354.3dm` using its original local creation time. Its SHA-256 stayed `65BB9146A0C094F5CECFF26131E18E2E9C6636D58B7C1755CD269721143ACD29`. The earlier test copy was already absent and was not recreated. The original DWG is untouched.

Validated 2026-10-10 local: Debug/Release solution builds and updated offline regression suite passed; packaged 1.3.5 native smoke, Router bundle validation, 181 manifest hashes and compiled RHP identities passed. Detailed commands/results are in the matching EXET. No production installation or model rebake occurred.
