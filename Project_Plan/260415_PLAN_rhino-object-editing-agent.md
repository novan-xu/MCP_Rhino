# Rhino Object Editing Agent

## 1. Background

The project needed a batch-editing capability for Rhino objects, not just filtering. The target workflow is to locate objects by existing screening rules and then modify metadata and display-related properties directly in the source 3dm file.

## 2. Goal

Build an object editing capability that supports:

- user text / user attribute updates
- user text removal
- layer reassignment
- display color changes
- preview before apply
- direct overwrite of the original Rhino file

The implementation must also reserve extension points for future archive/risk control and export/report formatting.

## 3. Architecture Mapping

- `Tools/`: expose preview and apply editing entry points
- `Skills/`: coordinate object selection plus preview/apply workflows
- `Agents/`: provide a future-ready editing agent entry point
- `Application/`: orchestrate editing, validation, formatting, and overwrite flow
- `Domain/`: define edit operation enums and models
- `Contracts/`: define preview/apply requests and structured responses
- `Infrastructure/`: implement Rhino3dm attribute mutation, validation, and placeholder safeguard/formatter behavior

## 4. Key Design Decisions

### 4.1 Reuse Existing Filtering

Object selection is not rebuilt from scratch. The new editing workflow reuses the current filtering capability so both screening and editing share the same targeting logic.

### 4.2 Operation List Model

Editing is represented as a list of operations instead of separate hardcoded flows. The first version supports:

- `SetUserText`
- `RemoveUserText`
- `SetLayer`
- `SetDisplayColor`

This keeps the editing pipeline extensible.

### 4.3 Preview and Apply Split

The capability is split into preview and apply workflows so callers can inspect the intended changes before mutating the model.

### 4.4 Direct Overwrite with Reserved Safeguard Hook

The current implementation writes back to the original file directly, matching the user workflow. A safeguard interface is still reserved so archive or rollback logic can be inserted later without redesigning the editing flow.

### 4.5 Structured Result First, Report Later

The editing flow returns structured preview and execution results rather than committing to a final reporting format. This keeps the feature compatible with a future export/report agent.

## 5. Files Added or Modified

### Modified

- `src/MCP_Rhino.Server/Program.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`

### Added

- `src/MCP_Rhino.Server/Domain/Enums/ObjectEditOperationType.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoDisplayColor.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoObjectEditOperation.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ObjectColorRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ObjectEditOperationRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewObjectEditsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyObjectEditsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ObjectEditWarning.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ObjectEditOperationResult.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ObjectEditPreviewResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ObjectEditExecutionResponse.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IObjectEditValidator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IObjectEditOperationApplier.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IFileMutationSafeguard.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IEditResultFormatter.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectEditingService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoObjectEditValidator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoObjectEditOperationApplier.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/NoOpFileMutationSafeguard.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/PassThroughEditResultFormatter.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectSelectionSkill.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectEditPreviewSkill.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectEditApplySkill.cs`
- `src/MCP_Rhino.Server/Agents/Editing/RhinoObjectEditingAgent.cs`
- `src/MCP_Rhino.Server/Tools/Editing/PreviewObjectEditsTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/ApplyObjectEditsTool.cs`

## 6. Usage

### CLI Developer Commands

- `preview-object-edits`
- `apply-object-edits`

### Edit Spec Examples

- `set-user:HSS_D=8.5`
- `remove-user:HSS_D`
- `set-layer:01_MET-IEF-Curtain Wall::Surfaces - PNL`
- `set-color:255,0,0`

Multiple operations can be combined with semicolons.

## 7. Future Extensions

- archive/rollback implementation behind safeguard interface
- export/report formatting agent behind formatter interface
- automatic layer creation when target layer is missing
- additional property edits such as naming, material, linetype, or print settings
- richer decision logic in the editing agent