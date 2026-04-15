# Rhino Object Filter Agent

## 1. Background

The project needed a reusable Rhino object filtering capability that could work from single conditions or arbitrary combinations of conditions. The long-term goal is to support future screening dimensions without rebuilding the agent every time.

## 2. Goal

Build a filtering framework that can identify Rhino objects by different rules, including:

- layer-based filtering
- user attribute key/value filtering
- object type filtering
- combined filtering with AND / OR logic

The framework should also support layer ambiguity resolution through candidate confirmation.

## 3. Architecture Mapping

- `Tools/`: expose atomic filtering capabilities and a composite filtering entry tool
- `Skills/`: provide reusable filtering workflows and condition orchestration
- `Agents/`: provide a target-driven filtering agent entry point
- `Application/`: implement the filter engine and criterion evaluators
- `Domain/`: define filter criteria, object info, filter results, and enums
- `Contracts/`: define request DTOs for tools and workflows
- `Infrastructure/`: provide Rhino3dm file access

## 4. Key Design Decisions

### 4.1 Extensible Criteria Model

Filtering is driven by a common `RhinoObjectFilterCriteria` model so new conditions can be added later without redesigning the agent.

### 4.2 Evaluator-Based Execution

The application layer uses pluggable criterion evaluators:

- layer evaluator
- object type evaluator
- user attribute evaluator

This keeps the filter engine open for future conditions.

### 4.3 Layer Candidate Confirmation

Because Rhino layers are hierarchical and may contain duplicate names in different branches, the composite filter workflow resolves candidate layers first and asks for confirmation when necessary.

### 4.4 Dual Entry Strategy

The capability is available both through:

- atomic tools for layer, type, and user attributes
- a composite filtering workflow for arbitrary combinations

## 5. Files Added or Modified

### Modified

- `src/MCP_Rhino.Server/Program.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/Server/ToolRegistration.cs`

### Added

- `src/MCP_Rhino.Server/Application/Interfaces/IRhinoDocumentRepository.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IObjectFilterCriterionEvaluator.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectFilterService.cs`
- `src/MCP_Rhino.Server/Application/Services/Filters/LayerFilterCriterionEvaluator.cs`
- `src/MCP_Rhino.Server/Application/Services/Filters/ObjectTypeFilterCriterionEvaluator.cs`
- `src/MCP_Rhino.Server/Application/Services/Filters/UserAttributeFilterCriterionEvaluator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoDocumentRepository.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/UserAttributeConditionRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/FindLayerCandidatesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/FilterObjectsByLayerRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/FilterObjectsByTypeRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/FilterObjectsByUserAttributesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/FilterObjectsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/OperationResponse.cs`
- `src/MCP_Rhino.Server/Domain/Enums/FilterMatchMode.cs`
- `src/MCP_Rhino.Server/Domain/Enums/RhinoObjectType.cs`
- `src/MCP_Rhino.Server/Domain/Enums/UserAttributeComparisonMode.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoObjectUserAttributeEntry.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoLayerCandidate.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoUserAttributeCondition.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoObjectInfo.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoObjectFilterCriteria.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoObjectFilterResult.cs`
- `src/MCP_Rhino.Server/Skills/Inspection/LayerObjectFilterSkill.cs`
- `src/MCP_Rhino.Server/Skills/Inspection/ObjectTypeFilterSkill.cs`
- `src/MCP_Rhino.Server/Skills/Inspection/UserAttributeObjectFilterSkill.cs`
- `src/MCP_Rhino.Server/Skills/Inspection/CompositeObjectFilterSkill.cs`
- `src/MCP_Rhino.Server/Agents/Inspection/RhinoObjectFilterAgent.cs`
- `src/MCP_Rhino.Server/Tools/Layers/FindLayerCandidatesTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/FilterObjectsByLayerTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsByTypeTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsByUserAttributesTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsTool.cs`

## 6. Usage

### CLI Test Commands

- `find-layer-candidates`
- `filter-objects-by-layer`
- `filter-objects-by-type`
- `filter-objects-by-user-attributes`
- `filter-objects-agent`

### Filtering Examples

- filter by layer
- filter by object type
- filter by user attribute conditions
- filter by combined conditions with `mode=All` or `mode=Any`

## 7. Future Extensions

- name-based filtering
- spatial / bounding-box filtering
- material or color filtering
- block metadata filtering
- geometry-derived rule filtering
- richer agent decision logic over arbitrary condition combinations