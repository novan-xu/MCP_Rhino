extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Extrusion = rhinocommon::Rhino.Geometry.Extrusion;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBooleanOperator : ILiveBooleanOperator
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveBooleanOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<ArchitecturalBooleanPreviewResponse> Preview(
        string filePath,
        IReadOnlyList<ArchitecturalBooleanOperationSpec> entries)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<List<PreparedBooleanEntry>> prepared = Prepare(document, entries);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<ArchitecturalBooleanPreviewResponse>.Fail(prepared.Message);
            }

            List<ArchitecturalBooleanEntryResponse> results = prepared.Data.Select(ToPreviewResponse).ToList();
            return OperationResponse<ArchitecturalBooleanPreviewResponse>.Ok(new ArchitecturalBooleanPreviewResponse
            {
                FilePath = filePath,
                RequestedCount = entries.Count,
                SucceededCount = results.Count(item => item.Success),
                FailedCount = results.Count(item => !item.Success),
                Results = results
            }, "Boolean preview completed.");
        });
    }

    public OperationResponse<ArchitecturalBooleanApplyResponse> Apply(
        string filePath,
        IReadOnlyList<ArchitecturalBooleanOperationSpec> entries)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyArchitecturalBooleanObjects", document =>
        {
            OperationResponse<List<PreparedBooleanEntry>> prepared = Prepare(document, entries);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Fail(prepared.Message);
            }

            int deletedCount = 0;
            int createdCount = 0;
            var results = new List<ArchitecturalBooleanEntryResponse>(prepared.Data.Count);

            foreach (PreparedBooleanEntry entry in prepared.Data)
            {
                var createdIds = new List<Guid>();
                if (!entry.Success)
                {
                    return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Fail(entry.Message);
                }

                if (entry.DeleteTargets)
                {
                    foreach (Guid objectId in entry.TargetObjectIds)
                    {
                        if (document.Objects.Delete(objectId, quiet: true))
                        {
                            deletedCount++;
                        }
                    }
                }

                if (entry.DeleteCutters)
                {
                    foreach (Guid objectId in entry.CutterObjectIds)
                    {
                        if (document.Objects.Delete(objectId, quiet: true))
                        {
                            deletedCount++;
                        }
                    }
                }

                foreach (Brep brep in entry.ResultBreps)
                {
                    Guid createdId = document.Objects.AddBrep(brep, entry.ResultAttributes.Duplicate());
                    if (createdId == Guid.Empty)
                    {
                        return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Fail("Failed to add boolean result Brep.");
                    }

                    createdIds.Add(createdId);
                    createdCount++;
                }

                results.Add(new ArchitecturalBooleanEntryResponse
                {
                    Operation = entry.Operation,
                    Success = true,
                    Message = $"Boolean {entry.Operation} applied.",
                    ResultObjectIds = createdIds,
                    ResultBoundingBoxes = entry.ResultBreps.Select(brep => LiveArchitecturalGeometryBuilder.ToResponse(brep.GetBoundingBox(true))).ToList()
                });
            }

            if (createdCount > 0 || deletedCount > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Ok(
                (createdCount > 0 || deletedCount > 0, new ArchitecturalBooleanApplyResponse
                {
                    FilePath = filePath,
                    RequestedCount = entries.Count,
                    CreatedCount = createdCount,
                    DeletedCount = deletedCount,
                    Results = results
                }),
                "Boolean apply completed.");
        });
    }

    public OperationResponse<ArchitecturalBooleanPreviewResponse> PreviewOpenings(
        string filePath,
        IReadOnlyList<ArchitecturalOpeningOperationSpec> entries)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<List<PreparedBooleanEntry>> prepared = PrepareOpenings(document, entries);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<ArchitecturalBooleanPreviewResponse>.Fail(prepared.Message);
            }

            List<ArchitecturalBooleanEntryResponse> results = prepared.Data.Select(ToPreviewResponse).ToList();
            return OperationResponse<ArchitecturalBooleanPreviewResponse>.Ok(new ArchitecturalBooleanPreviewResponse
            {
                FilePath = filePath,
                RequestedCount = entries.Count,
                SucceededCount = results.Count(item => item.Success),
                FailedCount = results.Count(item => !item.Success),
                Results = results
            }, "Opening preview completed.");
        });
    }

    public OperationResponse<ArchitecturalBooleanApplyResponse> ApplyOpenings(
        string filePath,
        IReadOnlyList<ArchitecturalOpeningOperationSpec> entries,
        ArchitecturalObjectAttributesSpec cutterAttributes)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyArchitecturalOpenings", document =>
        {
            OperationResponse<List<PreparedBooleanEntry>> prepared = PrepareOpenings(document, entries);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Fail(prepared.Message);
            }

            int deletedCount = 0;
            int createdCount = 0;
            var results = new List<ArchitecturalBooleanEntryResponse>(prepared.Data.Count);

            foreach (PreparedBooleanEntry entry in prepared.Data)
            {
                if (!entry.Success)
                {
                    return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Fail(entry.Message);
                }

                foreach (Guid objectId in entry.TargetObjectIds)
                {
                    if (document.Objects.Delete(objectId, quiet: true))
                    {
                        deletedCount++;
                    }
                }

                var createdIds = new List<Guid>();
                foreach (Brep brep in entry.ResultBreps)
                {
                    Guid createdId = document.Objects.AddBrep(brep, entry.ResultAttributes.Duplicate());
                    if (createdId == Guid.Empty)
                    {
                        return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Fail("Failed to add opening result Brep.");
                    }

                    createdIds.Add(createdId);
                    createdCount++;
                }

                if (entry.RetainedCutter is not null)
                {
                    int layerIndex = entry.ResultAttributes.LayerIndex;
                    var cutterSpec = new ArchitecturalPrimitiveSpec
                    {
                        Kind = ArchitecturalPrimitiveKind.OpeningCutter,
                        Metadata = new ArchitecturalMetadataSpec { Category = "opening-cutter" }
                    };
                    ObjectAttributes cutterObjectAttributes = LiveArchitecturalGeometryBuilder.CreateAttributes(layerIndex, cutterAttributes, cutterSpec);
                    Guid cutterId = document.Objects.Add(entry.RetainedCutter, cutterObjectAttributes);
                    if (cutterId != Guid.Empty)
                    {
                        createdIds.Add(cutterId);
                        createdCount++;
                    }
                }

                results.Add(new ArchitecturalBooleanEntryResponse
                {
                    Operation = BooleanOperationKind.Difference,
                    Success = true,
                    Message = "Opening applied.",
                    ResultObjectIds = createdIds,
                    ResultBoundingBoxes = entry.ResultBreps.Select(brep => LiveArchitecturalGeometryBuilder.ToResponse(brep.GetBoundingBox(true))).ToList()
                });
            }

            if (createdCount > 0 || deletedCount > 0)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, ArchitecturalBooleanApplyResponse Result)>.Ok(
                (createdCount > 0 || deletedCount > 0, new ArchitecturalBooleanApplyResponse
                {
                    FilePath = filePath,
                    RequestedCount = entries.Count,
                    CreatedCount = createdCount,
                    DeletedCount = deletedCount,
                    Results = results
                }),
                "Opening apply completed.");
        });
    }

    private static ArchitecturalBooleanEntryResponse ToPreviewResponse(PreparedBooleanEntry entry)
    {
        return new ArchitecturalBooleanEntryResponse
        {
            Operation = entry.Operation,
            Success = entry.Success,
            Message = entry.Message,
            ResultBoundingBoxes = entry.ResultBreps
                .Select(brep => brep.GetBoundingBox(true))
                .Where(box => box.IsValid)
                .Select(LiveArchitecturalGeometryBuilder.ToResponse)
                .ToList()
        };
    }

    private static OperationResponse<List<PreparedBooleanEntry>> Prepare(
        RhinoDoc document,
        IReadOnlyList<ArchitecturalBooleanOperationSpec> entries)
    {
        var prepared = new List<PreparedBooleanEntry>(entries.Count);
        foreach (ArchitecturalBooleanOperationSpec entry in entries)
        {
            OperationResponse<PreparedBooleanEntry> result = PrepareEntry(document, entry);
            if (!result.Success || result.Data is null)
            {
                return OperationResponse<List<PreparedBooleanEntry>>.Fail(result.Message);
            }

            prepared.Add(result.Data);
        }

        return OperationResponse<List<PreparedBooleanEntry>>.Ok(prepared);
    }

    private static OperationResponse<List<PreparedBooleanEntry>> PrepareOpenings(
        RhinoDoc document,
        IReadOnlyList<ArchitecturalOpeningOperationSpec> entries)
    {
        var prepared = new List<PreparedBooleanEntry>(entries.Count);
        foreach (ArchitecturalOpeningOperationSpec opening in entries)
        {
            if (opening.TargetObjectId == Guid.Empty)
            {
                return OperationResponse<List<PreparedBooleanEntry>>.Fail("Opening target object id is required.");
            }

            if (opening.OpeningKind == OpeningKind.Rectangular && (opening.Width <= 0d || opening.Height <= 0d))
            {
                return OperationResponse<List<PreparedBooleanEntry>>.Fail("Rectangular opening width and height must be greater than zero.");
            }

            if (opening.OpeningKind == OpeningKind.Circular && opening.Radius <= 0d)
            {
                return OperationResponse<List<PreparedBooleanEntry>>.Fail("Circular opening radius must be greater than zero.");
            }

            GeometryBase cutter = LiveArchitecturalGeometryBuilder.BuildOpeningCutter(opening);
            Brep? cutterBrep = ToBrep(cutter);
            if (cutterBrep is null)
            {
                return OperationResponse<List<PreparedBooleanEntry>>.Fail("Opening cutter could not be converted to Brep.");
            }

            var boolean = new ArchitecturalBooleanOperationSpec
            {
                Operation = BooleanOperationKind.Difference,
                TargetObjectIds = new[] { opening.TargetObjectId },
                DeleteTargets = true,
                DeleteCutters = false,
                Tolerance = opening.Tolerance
            };

            OperationResponse<PreparedBooleanEntry> entry = PrepareEntry(document, boolean, new[] { cutterBrep });
            if (!entry.Success || entry.Data is null)
            {
                return OperationResponse<List<PreparedBooleanEntry>>.Fail(entry.Message);
            }

            if (opening.RetainCutter)
            {
                entry.Data.RetainedCutter = cutter;
            }

            prepared.Add(entry.Data);
        }

        return OperationResponse<List<PreparedBooleanEntry>>.Ok(prepared);
    }

    private static OperationResponse<PreparedBooleanEntry> PrepareEntry(
        RhinoDoc document,
        ArchitecturalBooleanOperationSpec entry,
        IReadOnlyList<Brep>? suppliedCutters = null)
    {
        if (entry.TargetObjectIds.Count == 0)
        {
            return OperationResponse<PreparedBooleanEntry>.Fail("Boolean target object ids are required.");
        }

        if (entry.Operation != BooleanOperationKind.Union && (suppliedCutters is null || suppliedCutters.Count == 0) && entry.CutterObjectIds.Count == 0)
        {
            return OperationResponse<PreparedBooleanEntry>.Fail("Boolean cutter object ids are required.");
        }

        OperationResponse<List<Brep>> targets = ResolveBreps(document, entry.TargetObjectIds);
        if (!targets.Success || targets.Data is null)
        {
            return OperationResponse<PreparedBooleanEntry>.Fail(targets.Message);
        }

        OperationResponse<List<Brep>> cutters = suppliedCutters is null
            ? ResolveBreps(document, entry.CutterObjectIds)
            : OperationResponse<List<Brep>>.Ok(suppliedCutters.ToList());
        if (!cutters.Success || cutters.Data is null)
        {
            return OperationResponse<PreparedBooleanEntry>.Fail(cutters.Message);
        }

        double tolerance = entry.Tolerance > 0d ? entry.Tolerance : document.ModelAbsoluteTolerance;
        Brep[] resultBreps = entry.Operation switch
        {
            BooleanOperationKind.Union => Brep.CreateBooleanUnion(targets.Data.Concat(cutters.Data), tolerance),
            BooleanOperationKind.Difference => Brep.CreateBooleanDifference(targets.Data, cutters.Data, tolerance),
            BooleanOperationKind.Intersection => Brep.CreateBooleanIntersection(targets.Data, cutters.Data, tolerance),
            _ => Array.Empty<Brep>()
        };

        if (resultBreps.Length == 0)
        {
            return OperationResponse<PreparedBooleanEntry>.Fail($"Boolean {entry.Operation} produced no result geometry.");
        }

        RhinoObject? firstTarget = document.Objects.FindId(entry.TargetObjectIds[0]);
        if (firstTarget is null)
        {
            return OperationResponse<PreparedBooleanEntry>.Fail($"Target object not found: {entry.TargetObjectIds[0]}");
        }

        return OperationResponse<PreparedBooleanEntry>.Ok(new PreparedBooleanEntry
        {
            Operation = entry.Operation,
            TargetObjectIds = entry.TargetObjectIds,
            CutterObjectIds = entry.CutterObjectIds,
            DeleteTargets = entry.DeleteTargets,
            DeleteCutters = entry.DeleteCutters,
            ResultBreps = resultBreps,
            ResultAttributes = firstTarget.Attributes.Duplicate(),
            Success = true,
            Message = $"Boolean {entry.Operation} preview succeeded."
        });
    }

    private static OperationResponse<List<Brep>> ResolveBreps(RhinoDoc document, IReadOnlyList<Guid> objectIds)
    {
        var breps = new List<Brep>(objectIds.Count);
        foreach (Guid objectId in objectIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject is null || rhinoObject.IsDeleted)
            {
                return OperationResponse<List<Brep>>.Fail($"Object not found: {objectId}");
            }

            Brep? brep = ToBrep(rhinoObject.Geometry);
            if (brep is null)
            {
                return OperationResponse<List<Brep>>.Fail($"Object is not supported for Brep boolean operations: {objectId}");
            }

            breps.Add(brep);
        }

        return OperationResponse<List<Brep>>.Ok(breps);
    }

    private static Brep? ToBrep(GeometryBase? geometry)
    {
        return geometry switch
        {
            Brep brep => brep.DuplicateBrep(),
            Extrusion extrusion => extrusion.ToBrep(),
            Surface surface => surface.ToBrep(),
            _ => null
        };
    }

    private sealed class PreparedBooleanEntry
    {
        public BooleanOperationKind Operation { get; init; }
        public IReadOnlyList<Guid> TargetObjectIds { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<Guid> CutterObjectIds { get; init; } = Array.Empty<Guid>();
        public bool DeleteTargets { get; init; }
        public bool DeleteCutters { get; init; }
        public Brep[] ResultBreps { get; init; } = Array.Empty<Brep>();
        public ObjectAttributes ResultAttributes { get; init; } = new();
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public GeometryBase? RetainedCutter { get; set; }
    }
}

