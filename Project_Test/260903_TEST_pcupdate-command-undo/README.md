# PCUpdate Command Undo Regression

This focused regression verifies that `PCUpdate` reuses Rhino's active command Undo record instead
of requesting a nested record. It also verifies that close/automatic rollback operations remain
limited to records opened by the update service and that the corrected plug-in is patch-versioned.

Run the static/contract smoke in both configurations:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Release
```

Run the native ambient-record probe in a supported Rhino test host:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Release -- --rhino-undo
```
