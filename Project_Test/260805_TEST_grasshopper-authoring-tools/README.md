# Grasshopper Authoring Tools TEST

This folder owns the unique CLI slug `grasshopper-authoring-tools-smoke-test` and Rhino command
`_McpGrasshopperAuthoringToolsSmoke`.

## Verified local API baseline

- Rhino / RhinoCommon: `8.33.26188.13001`
- Grasshopper: `8.33.26188.13001`
- GH_IO: `8.33.26188.13001`
- GrasshopperPlugin.rhp: `8.33.26188.13001`, assembly/plugin id
  `B45A29B1-4343-4035-989E-044E8580D9CF`
- Installed assembly locations: Rhino 8 `System/netcore` and `Plug-ins/Grasshopper`

The `ApiProbe` project records the exact public API surface used by the adapter. It proved that
`GH_Document.RhinoDocument.RuntimeSerialNumber` provides the required binding to the Router-selected
Rhino document, while `GH_DocumentServer` enumerates all live definitions without using the active
canvas. It also verified component proxy creation, native GH undo records, solution control, and
volatile data access.

## Commands

```powershell
dotnet run --project Project_Test\260805_TEST_grasshopper-authoring-tools\ApiProbe\GrasshopperApiProbe.csproj -c Release
dotnet clean src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:McpRhinoCliHost=true
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:McpRhinoCliHost=true
src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.exe grasshopper-authoring-tools-smoke-test
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

For live verification, save an anonymous Rhino document and run
`_McpGrasshopperAuthoringToolsSmoke`. The command starts GH1 through Rhino APIs, verifies the
definition binding, and searches the installed component catalog. No `.3dm`, `.gh`, or `.ghx`
fixture is committed.

The JSON sample intentionally leaves the arithmetic component GUID as all-zero. A caller must
replace it with the exact GUID returned by `SearchGrasshopperComponents` before previewing.

## Recorded result

- API probe: exit code 0 on Rhino/Grasshopper `8.33.26188.13001`; verified the exact
  Grasshopper plug-in id used for lazy loading.
- Debug and Release solution builds: exit code 0, 0 warnings, 0 errors.
- Debug and Release contract/safety smokes: passed; 169 total tools and exactly 10 Grasshopper tools.
- Plugin outputs contained no duplicate Grasshopper, GH_IO, RhinoCommon, Rhino UI, or Eto assembly.
- Live Rhino smoke: not run automatically; use the command above with a saved anonymous document.
