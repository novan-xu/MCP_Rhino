# PCUpdate Locked Managed Objects Regression

This focused regression verifies that retained and obsolete managed dependencies use RhinoCommon's
mode-bypassing replace/delete overloads. It also verifies retained object-level locked/hidden mode
preservation, rejects unlock/show workarounds, and checks the package patch version.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Release
```
