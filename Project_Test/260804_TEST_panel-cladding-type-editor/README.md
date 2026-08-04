# Panel Cladding Type Editor Smoke

Build and run the deterministic smoke in both configurations:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --nologo
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- panel-cladding-type-editor-smoke-test _validation\panel-cladding-type-editor\Debug
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- panel-cladding-type-editor-smoke-test _validation\panel-cladding-type-editor\Release
```

The smoke validates key parsing and ordering, planar/curved/unsupported projection,
unit-independent deterministic signatures, PNG rendering, preservation and visual layout of an
existing `.xlsx`, hidden signature indexing, image embedding, identical-type reuse, and locked-file
preflight failure. It also verifies that the feature is exposed only as the local Rhino command and
does not restore an MCP tool, chat, Companion, or retired panel command.

The Rhino-side deterministic smoke command is:

```text
_McpPanelCladdingTypeEditorSmoke
```

Live production acceptance uses:

```text
_McpPanelCladdingEditor
```

Preselect one referenced panel Brep carrying the `CW_2.03_OFFSET_H#`,
`CW_2.04_OFFSET_V#`, and cladding keys before running the command. Saving must create one Rhino
Undo record, update the selected object's cladding/type/signature user text, and create or reuse one
visual worksheet in the selected workbook.
