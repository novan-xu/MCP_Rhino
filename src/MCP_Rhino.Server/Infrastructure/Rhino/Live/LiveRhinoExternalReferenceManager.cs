extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using InstanceDefinition = rhinocommon::Rhino.DocObjects.InstanceDefinition;
using InstanceDefinitionArchiveFileStatus = rhinocommon::Rhino.DocObjects.InstanceDefinitionArchiveFileStatus;
using InstanceDefinitionUpdateType = rhinocommon::Rhino.DocObjects.InstanceDefinitionUpdateType;
using InstanceObject = rhinocommon::Rhino.DocObjects.InstanceObject;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoExternalReferenceManager : ILiveExternalReferenceManager
{
    public OperationResponse<IReadOnlyList<WorksessionAttachmentResult>> ListWorksession(RhinoDoc document)
    {
        try
        {
            string[] modelPaths = document.Worksession?.ModelPaths ?? Array.Empty<string>();
            List<string> attachedPaths = modelPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Where(path => !PathsEqual(path, document.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            IReadOnlyList<WorksessionAttachmentResult> results = attachedPaths
                .Select(path => new WorksessionAttachmentResult
                {
                    SourceFilePath = path,
                    Status = File.Exists(path) ? WorksessionAttachmentStatus.Attached : WorksessionAttachmentStatus.Stale,
                    Attached = true,
                    WasAlreadyAttached = true,
                    Message = File.Exists(path)
                        ? "Worksession attachment is present."
                        : "Worksession attachment is present but the source file was not found."
                })
                .ToList();

            return OperationResponse<IReadOnlyList<WorksessionAttachmentResult>>.Ok(results);
        }
        catch (Exception ex)
        {
            return OperationResponse<IReadOnlyList<WorksessionAttachmentResult>>.Fail($"Worksession list failed: {ex.Message}");
        }
    }

    public OperationResponse<IReadOnlyList<LinkedBlockResult>> UpdateLinkedBlock(RhinoDoc document, IReadOnlyList<string> definitionNames)
    {
        try
        {
            var results = new List<LinkedBlockResult>(definitionNames.Count);
            foreach (string definitionName in definitionNames)
            {
                InstanceDefinition? definition = document.InstanceDefinitions.Find(definitionName);
                if (definition is null || definition.IsDeleted)
                {
                    results.Add(new LinkedBlockResult
                    {
                        DefinitionName = definitionName,
                        Success = false,
                        Updated = false,
                        WasAlreadyDefined = false,
                        Message = "LINKED_BLOCK_DEFINITION_NOT_FOUND"
                    });
                    continue;
                }

                if (!IsLinkedDefinition(definition))
                {
                    results.Add(CreateLinkedBlockResult(definition, success: false, updated: false, "Definition is not a linked block."));
                    continue;
                }

                bool refreshed = document.InstanceDefinitions.RefreshLinkedBlock(definition);
                results.Add(CreateLinkedBlockResult(
                    definition,
                    success: refreshed,
                    updated: refreshed,
                    refreshed ? "Linked block refreshed." : "Linked block refresh failed."));
            }

            return OperationResponse<IReadOnlyList<LinkedBlockResult>>.Ok(results);
        }
        catch (Exception ex)
        {
            return OperationResponse<IReadOnlyList<LinkedBlockResult>>.Fail($"Linked block update failed: {ex.Message}");
        }
    }

    private static LinkedBlockResult CreateLinkedBlockResult(InstanceDefinition definition, bool success, bool updated, string message)
    {
        InstanceObject? firstReference = definition.GetReferences(1).FirstOrDefault();
        return new LinkedBlockResult
        {
            DefinitionId = definition.Id,
            DefinitionIndex = definition.Index,
            DefinitionName = definition.Name,
            InstanceObjectId = firstReference?.Id,
            SourceFilePath = string.IsNullOrWhiteSpace(definition.SourceArchive) ? null : definition.SourceArchive,
            Success = success,
            Updated = updated,
            WasAlreadyDefined = true,
            Message = message
        };
    }

    private static bool IsLinkedDefinition(InstanceDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.SourceArchive))
        {
            return false;
        }

        if (definition.ArchiveFileStatus == InstanceDefinitionArchiveFileStatus.NotALinkedInstanceDefinition)
        {
            return false;
        }

        return definition.UpdateType == InstanceDefinitionUpdateType.Linked
            || definition.UpdateType == InstanceDefinitionUpdateType.LinkedAndEmbedded;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
