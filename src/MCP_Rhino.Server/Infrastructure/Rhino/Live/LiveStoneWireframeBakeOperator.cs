extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Domain.Enums;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using InstanceDefinition = rhinocommon::Rhino.DocObjects.InstanceDefinition;
using InstanceObject = rhinocommon::Rhino.DocObjects.InstanceObject;
using InstanceReferenceGeometry = rhinocommon::Rhino.Geometry.InstanceReferenceGeometry;
using Line = rhinocommon::Rhino.Geometry.Line;
using LineCurve = rhinocommon::Rhino.Geometry.LineCurve;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Transform = rhinocommon::Rhino.Geometry.Transform;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveStoneWireframeBakeOperator : ILiveStoneWireframeBakeOperator
{
    private const int MaxNestedDepth = 24;
    private const int MaxReturnedObjectIds = 200;
    private static readonly TimeSpan PreviewTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ApplyTimeout = TimeSpan.FromSeconds(120);

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveStoneWireframeBakeOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<StoneWireframeBakeResponse> Preview(
        string filePath,
        IReadOnlyCollection<string> assemblyDefinitionNames,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            using PreparedBake prepared = Prepare(document, assemblyDefinitionNames, outputMode, surfaceHeightInches);
            StoneWireframeBakeResponse response = BuildResponse(filePath, "Preview", prepared, 0, 0, Array.Empty<Guid>());
            return OperationResponse<StoneWireframeBakeResponse>.Ok(
                response,
                prepared.Failures.Count == 0
                    ? "Stone wireframe bake preview completed."
                    : "Stone wireframe bake preview completed with failures; apply will refuse mutation.");
        }, PreviewTimeout);
    }

    public OperationResponse<StoneWireframeBakeResponse> Apply(
        string filePath,
        IReadOnlyCollection<string> assemblyDefinitionNames,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, "MCP: ApplyStoneWireframeBake", document =>
        {
            using PreparedBake prepared = Prepare(document, assemblyDefinitionNames, outputMode, surfaceHeightInches);
            if (prepared.Failures.Count > 0)
            {
                string detail = string.Join(" | ", prepared.Failures.Take(8).Select(failure => failure.Message));
                return OperationResponse<(bool Mutated, StoneWireframeBakeResponse Result)>.Fail(
                    $"Stone wireframe bake validation failed ({prepared.Failures.Count}): {detail}");
            }

            int createdDefinitionCount = 0;
            var definitionIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (PreparedDefinition definition in prepared.Definitions)
            {
                if (definition.ExistingDefinition is not null)
                {
                    definitionIndices[definition.OutputName] = definition.ExistingDefinition.Index;
                    continue;
                }

                var geometry = new GeometryBase[] { definition.OutputGeometry };
                var attributes = new ObjectAttributes[] { definition.Selection.SourceAttributes.Duplicate() };
                int index = document.InstanceDefinitions.Add(
                    definition.OutputName,
                    $"{outputMode} bake from {definition.SourceDefinition.Name}",
                    outputMode == StoneBakeOutputMode.Curve ? definition.Selection.Line.From : Point3d.Origin,
                    geometry,
                    attributes);

                if (index < 0)
                {
                    return OperationResponse<(bool Mutated, StoneWireframeBakeResponse Result)>.Fail(
                        $"Failed to create output block definition: {definition.OutputName}");
                }

                definitionIndices[definition.OutputName] = index;
                createdDefinitionCount++;
            }

            var createdObjectIds = new List<Guid>();
            foreach (PreparedPlacement placement in prepared.Placements.Where(item => !item.AlreadyExists))
            {
                if (!definitionIndices.TryGetValue(placement.Definition.OutputName, out int definitionIndex))
                {
                    return OperationResponse<(bool Mutated, StoneWireframeBakeResponse Result)>.Fail(
                        $"Output definition index was not resolved: {placement.Definition.OutputName}");
                }

                var attributes = new ObjectAttributes
                {
                    LayerIndex = placement.Occurrence.LayerIndex,
                    Name = placement.Definition.OutputName
                };
                attributes.SetUserString(
                    StoneWireframeBakeNaming.AssemblyAttributeKey,
                    placement.Occurrence.AssemblyDefinitionName);

                Guid objectId = document.Objects.AddInstanceObject(
                    definitionIndex,
                    placement.Transform,
                    attributes);
                if (objectId == Guid.Empty)
                {
                    return OperationResponse<(bool Mutated, StoneWireframeBakeResponse Result)>.Fail(
                        $"Failed to insert {placement.Definition.OutputName} for assembly {placement.Occurrence.AssemblyDefinitionName}.");
                }

                placement.OutputObjectId = objectId;
                createdObjectIds.Add(objectId);
            }

            bool mutated = createdDefinitionCount > 0 || createdObjectIds.Count > 0;
            if (mutated)
            {
                document.Views.Redraw();
            }

            StoneWireframeBakeResponse response = BuildResponse(
                filePath,
                "Apply",
                prepared,
                createdDefinitionCount,
                createdObjectIds.Count,
                createdObjectIds);
            return OperationResponse<(bool Mutated, StoneWireframeBakeResponse Result)>.Ok(
                (mutated, response),
                mutated ? "Stone wireframe bake applied." : "Stone wireframe bake was already up to date.");
        }, ApplyTimeout);
    }

    private static PreparedBake Prepare(RhinoDoc document, IReadOnlyCollection<string> requestedAssemblyNames,
        StoneBakeOutputMode outputMode, double surfaceHeightInches)
    {
        double tolerance = Math.Max(document.ModelAbsoluteTolerance, 1e-7);
        var failures = new List<StoneWireframeBakeFailureResponse>();
        var failureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requested = new HashSet<string>(requestedAssemblyNames, StringComparer.OrdinalIgnoreCase);

        double surfaceHeight = 0;
        if (outputMode == StoneBakeOutputMode.WorldZSurface)
        {
            try
            {
                surfaceHeight = StoneSurfaceBakeGeometry.HeightInModelUnits(surfaceHeightInches, document.ModelUnitSystem);
                if (surfaceHeight <= tolerance) throw new ArgumentException("Surface height must exceed document tolerance.");
            }
            catch (ArgumentException ex)
            {
                AddFailure(failures, failureKeys, "INVALID_SURFACE_HEIGHT", string.Empty, string.Empty, ex.Message);
                return new PreparedBake(Array.Empty<InstanceObject>(), Array.Empty<StoneOccurrence>(),
                    Array.Empty<PreparedDefinition>(), Array.Empty<PreparedPlacement>(), failures,
                    outputMode, surfaceHeightInches, surfaceHeight, document.ModelUnitSystem.ToString());
            }
        }

        List<InstanceObject> assemblies = document.Objects.GetObjectList(CreateInstanceEnumerationSettings())
            .OfType<InstanceObject>()
            .Where(instance => !instance.IsDeleted && !instance.Attributes.IsInstanceDefinitionObject)
            .Where(instance => instance.InstanceDefinition is not null)
            .Where(instance => StoneWireframeBakeNaming.IsAssemblyDefinitionName(instance.InstanceDefinition.Name))
            .Where(instance => requested.Count == 0 || requested.Contains(instance.InstanceDefinition.Name))
            .OrderBy(instance => instance.InstanceDefinition.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(instance => instance.Id)
            .ToList();

        if (requested.Count > 0)
        {
            HashSet<string> matchedNames = assemblies
                .Select(instance => instance.InstanceDefinition.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string missing in requested.Where(name => !matchedNames.Contains(name)))
            {
                AddFailure(failures, failureKeys, "ASSEMBLY_NOT_FOUND", missing, string.Empty,
                    $"No top-level live assembly instances were found for {missing}.");
            }
        }

        if (assemblies.Count == 0 && failures.Count == 0)
        {
            AddFailure(failures, failureKeys, "NO_ASSEMBLIES", string.Empty, string.Empty,
                "No top-level STN-MET-GA-...-... assembly instances were found in the live document.");
        }

        var occurrences = new List<StoneOccurrence>();
        foreach (InstanceObject assembly in assemblies)
        {
            InstanceDefinition definition = assembly.InstanceDefinition;
            var stack = new HashSet<Guid> { definition.Id };
            TraverseDefinition(
                document,
                definition,
                assembly.InstanceXform,
                assembly.Id,
                definition.Name,
                assembly.Attributes.LayerIndex,
                0,
                stack,
                occurrences,
                failures,
                failureKeys);
        }

        var definitions = new List<PreparedDefinition>();
        foreach (IGrouping<Guid, StoneOccurrence> group in occurrences.GroupBy(occurrence => occurrence.StoneDefinition.Id))
        {
            StoneOccurrence first = group.First();
            OperationResponse<SelectedLine> selectionResult = SelectBakeLine(first.StoneDefinition, tolerance);
            if (!selectionResult.Success || selectionResult.Data is null)
            {
                AddFailure(
                    failures,
                    failureKeys,
                    "LINE_SELECTION_FAILED",
                    first.AssemblyDefinitionName,
                    first.StoneDefinition.Name,
                    selectionResult.Message);
                continue;
            }

            string outputName = StoneWireframeBakeNaming.OutputDefinitionName(first.StoneDefinition.Name);
            InstanceDefinition? existingDefinition = document.InstanceDefinitions.Find(outputName);
            if (existingDefinition is not null && !IsCompatibleOutputDefinition(existingDefinition, selectionResult.Data.Line.Length, surfaceHeight, outputMode, tolerance))
            {
                AddFailure(
                    failures,
                    failureKeys,
                    "OUTPUT_DEFINITION_CONFLICT",
                    first.AssemblyDefinitionName,
                    first.StoneDefinition.Name,
                    $"Existing output definition {outputName} is not an equivalent {outputMode} definition for the requested edge and height.");
            }

            GeometryBase outputGeometry = outputMode == StoneBakeOutputMode.Curve
                ? new LineCurve(selectionResult.Data.Line)
                : StoneSurfaceBakeGeometry.Create(selectionResult.Data.Line.Length, surfaceHeight);
            if (!outputGeometry.IsValid)
            {
                outputGeometry.Dispose();
                AddFailure(failures, failureKeys, "INVALID_OUTPUT_GEOMETRY", first.AssemblyDefinitionName,
                    first.StoneDefinition.Name, $"Unable to construct valid geometry for {outputName}.");
                continue;
            }
            definitions.Add(new PreparedDefinition(
                first.StoneDefinition,
                outputName,
                selectionResult.Data,
                existingDefinition,
                group.Count(),
                outputGeometry));
        }

        var definitionBySourceId = definitions.ToDictionary(item => item.SourceDefinition.Id);
        var existingByKey = new Dictionary<string, List<ExistingPlacement>>(StringComparer.OrdinalIgnoreCase);
        foreach (InstanceObject instance in document.Objects.GetObjectList(CreateInstanceEnumerationSettings()).OfType<InstanceObject>()
            .Where(instance => !instance.IsDeleted && !instance.Attributes.IsInstanceDefinitionObject && instance.InstanceDefinition is not null))
        {
            string outputName = instance.InstanceDefinition.Name;
            if (!outputName.StartsWith(StoneWireframeBakeNaming.OutputPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string assemblyName = instance.Attributes.GetUserString(StoneWireframeBakeNaming.AssemblyAttributeKey) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                continue;
            }

            string key = PlacementKey(outputName, assemblyName);
            if (!existingByKey.TryGetValue(key, out List<ExistingPlacement>? existing))
            {
                existing = new List<ExistingPlacement>();
                existingByKey[key] = existing;
            }

            existing.Add(new ExistingPlacement(instance.Id, instance.InstanceXform));
        }

        var placements = new List<PreparedPlacement>();
        foreach (StoneOccurrence occurrence in occurrences)
        {
            if (!definitionBySourceId.TryGetValue(occurrence.StoneDefinition.Id, out PreparedDefinition? definition))
            {
                continue;
            }

            Transform placementTransform = occurrence.Transform * Transform.Translation(
                definition.Selection.Line.From.X,
                definition.Selection.Line.From.Y,
                definition.Selection.Line.From.Z);

            if (outputMode == StoneBakeOutputMode.WorldZSurface
                && !StoneSurfaceBakeGeometry.TryPlacement(definition.Selection.Line, occurrence.Transform,
                    tolerance, out placementTransform))
            {
                AddFailure(failures, failureKeys, "DEGENERATE_SURFACE_PLACEMENT", occurrence.AssemblyDefinitionName,
                    definition.SourceDefinition.Name, "A placed edge is degenerate or parallel to world Z; cannot create its surface.");
                continue;
            }

            Guid existingObjectId = Guid.Empty;
            string key = PlacementKey(definition.OutputName, occurrence.AssemblyDefinitionName);
            bool alreadyExists = false;
            if (existingByKey.TryGetValue(key, out List<ExistingPlacement>? existingPlacements))
            {
                ExistingPlacement? match = existingPlacements.FirstOrDefault(
                    item => !item.Consumed && TransformsEquivalent(item.Transform, placementTransform, tolerance));
                if (match is not null)
                {
                    match.Consumed = true;
                    alreadyExists = true;
                    existingObjectId = match.ObjectId;
                }
            }

            placements.Add(new PreparedPlacement(definition, occurrence, placementTransform, alreadyExists)
            { OutputObjectId = existingObjectId });
        }

        return new PreparedBake(assemblies, occurrences, definitions, placements, failures, outputMode,
            outputMode == StoneBakeOutputMode.WorldZSurface ? surfaceHeightInches : 0, surfaceHeight, document.ModelUnitSystem.ToString());
    }

    internal static ObjectEnumeratorSettings CreateInstanceEnumerationSettings() => new()
    {
        NormalObjects = true,
        LockedObjects = true,
        HiddenObjects = true,
        IdefObjects = false,
        DeletedObjects = false,
        ActiveObjects = true,
        ReferenceObjects = false,
        ObjectTypeFilter = ObjectType.InstanceReference
    };

    private static void TraverseDefinition(
        RhinoDoc document,
        InstanceDefinition definition,
        Transform currentTransform,
        Guid assemblyInstanceId,
        string assemblyDefinitionName,
        int layerIndex,
        int depth,
        HashSet<Guid> stack,
        List<StoneOccurrence> occurrences,
        List<StoneWireframeBakeFailureResponse> failures,
        HashSet<string> failureKeys)
    {
        if (depth >= MaxNestedDepth)
        {
            AddFailure(failures, failureKeys, "NESTED_DEPTH_EXCEEDED", assemblyDefinitionName, string.Empty,
                $"Nested definition depth exceeded {MaxNestedDepth} in {assemblyDefinitionName}.");
            return;
        }

        foreach (RhinoObject member in definition.GetObjects())
        {
            if (member.Geometry is not InstanceReferenceGeometry reference)
            {
                continue;
            }

            InstanceDefinition? nested = document.InstanceDefinitions.Find(reference.ParentIdefId, true);
            if (nested is null || nested.IsDeleted || string.IsNullOrWhiteSpace(nested.Name))
            {
                AddFailure(failures, failureKeys, "NESTED_DEFINITION_MISSING", assemblyDefinitionName, string.Empty,
                    $"A nested definition referenced by {definition.Name} could not be resolved.");
                continue;
            }

            Transform nestedTransform = currentTransform * reference.Xform;
            if (StoneWireframeBakeNaming.IsStoneDefinitionName(nested.Name))
            {
                occurrences.Add(new StoneOccurrence(
                    assemblyInstanceId,
                    assemblyDefinitionName,
                    nested,
                    nestedTransform,
                    layerIndex));
                continue;
            }

            if (!stack.Add(nested.Id))
            {
                AddFailure(failures, failureKeys, "NESTED_DEFINITION_CYCLE", assemblyDefinitionName, nested.Name,
                    $"Nested definition cycle detected at {nested.Name}.");
                continue;
            }

            TraverseDefinition(
                document,
                nested,
                nestedTransform,
                assemblyInstanceId,
                assemblyDefinitionName,
                layerIndex,
                depth + 1,
                stack,
                occurrences,
                failures,
                failureKeys);
            stack.Remove(nested.Id);
        }
    }

    private static OperationResponse<SelectedLine> SelectBakeLine(InstanceDefinition definition, double tolerance)
    {
        var candidates = new List<LineCandidate>();
        foreach (RhinoObject member in definition.GetObjects())
        {
            if (member.Geometry is not Curve curve)
            {
                continue;
            }

            foreach (Line line in ExtractLines(curve, tolerance))
            {
                Point3d from = line.From;
                Point3d to = line.To;
                if (line.Length <= tolerance
                    || Math.Abs(from.Y - to.Y) > tolerance
                    || Math.Abs(from.Z - to.Z) > tolerance
                    || Math.Abs(from.X - to.X) <= tolerance)
                {
                    continue;
                }

                Line canonical = from.X <= to.X ? line : new Line(to, from);
                candidates.Add(new LineCandidate(member.Id, member.Attributes.Duplicate(), canonical));
            }
        }

        if (candidates.Count == 0)
        {
            return OperationResponse<SelectedLine>.Fail(
                $"No nonzero definition-local horizontal line segment was found in {definition.Name}.");
        }

        double topY = candidates.Max(candidate => candidate.MidY);
        List<LineCandidate> top = candidates.Where(candidate => Math.Abs(candidate.MidY - topY) <= tolerance).ToList();
        double longest = top.Max(candidate => candidate.Line.Length);
        List<LineCandidate> finalists = top
            .Where(candidate => Math.Abs(candidate.Line.Length - longest) <= tolerance)
            .OrderBy(candidate => candidate.Line.From.X)
            .ThenBy(candidate => candidate.Line.From.Z)
            .ThenBy(candidate => candidate.SourceObjectId)
            .ToList();

        LineCandidate selected = finalists[0];
        LineCandidate? distinct = finalists.Skip(1).FirstOrDefault(
            candidate => !PointsEquivalent(candidate.Line.From, selected.Line.From, tolerance)
                || !PointsEquivalent(candidate.Line.To, selected.Line.To, tolerance));
        if (distinct is not null)
        {
            return OperationResponse<SelectedLine>.Fail(
                $"Ambiguous upper horizontal lines were found in {definition.Name}; tied candidates are geometrically distinct.");
        }

        return OperationResponse<SelectedLine>.Ok(
            new SelectedLine(selected.SourceObjectId, selected.SourceAttributes, selected.Line));
    }

    private static IEnumerable<Line> ExtractLines(Curve curve, double tolerance)
    {
        if (curve.IsLinear(tolerance))
        {
            yield return new Line(curve.PointAtStart, curve.PointAtEnd);
            yield break;
        }

        Curve[] segments = curve.DuplicateSegments();
        foreach (Curve segment in segments)
        {
            if (segment.IsLinear(tolerance))
            {
                yield return new Line(segment.PointAtStart, segment.PointAtEnd);
            }
        }
    }

    private static bool IsCompatibleOutputDefinition(InstanceDefinition definition, double expectedLength,
        double height, StoneBakeOutputMode outputMode, double tolerance)
    {
        RhinoObject[] members = definition.GetObjects();
        if (members.Length != 1) return false;
        if (outputMode == StoneBakeOutputMode.WorldZSurface)
            return StoneSurfaceBakeGeometry.IsCompatible(members[0].Geometry, expectedLength, height, tolerance);
        if (members[0].Geometry is not Curve curve || !curve.IsLinear(tolerance))
        {
            return false;
        }

        var line = new Line(curve.PointAtStart, curve.PointAtEnd);
        Point3d from = line.From.X <= line.To.X ? line.From : line.To;
        Point3d to = line.From.X <= line.To.X ? line.To : line.From;
        return PointsEquivalent(from, Point3d.Origin, tolerance)
            && Math.Abs(to.X - expectedLength) <= tolerance
            && Math.Abs(to.Y) <= tolerance
            && Math.Abs(to.Z) <= tolerance;
    }

    private static bool TransformsEquivalent(Transform left, Transform right, double documentTolerance)
    {
        const double linearTolerance = 1e-9;
        return Math.Abs(left.M00 - right.M00) <= linearTolerance
            && Math.Abs(left.M01 - right.M01) <= linearTolerance
            && Math.Abs(left.M02 - right.M02) <= linearTolerance
            && Math.Abs(left.M10 - right.M10) <= linearTolerance
            && Math.Abs(left.M11 - right.M11) <= linearTolerance
            && Math.Abs(left.M12 - right.M12) <= linearTolerance
            && Math.Abs(left.M20 - right.M20) <= linearTolerance
            && Math.Abs(left.M21 - right.M21) <= linearTolerance
            && Math.Abs(left.M22 - right.M22) <= linearTolerance
            && Math.Abs(left.M30 - right.M30) <= linearTolerance
            && Math.Abs(left.M31 - right.M31) <= linearTolerance
            && Math.Abs(left.M32 - right.M32) <= linearTolerance
            && Math.Abs(left.M33 - right.M33) <= linearTolerance
            && Math.Abs(left.M03 - right.M03) <= documentTolerance
            && Math.Abs(left.M13 - right.M13) <= documentTolerance
            && Math.Abs(left.M23 - right.M23) <= documentTolerance;
    }

    private static bool PointsEquivalent(Point3d left, Point3d right, double tolerance)
    {
        return left.DistanceTo(right) <= tolerance;
    }

    private static string PlacementKey(string outputDefinitionName, string assemblyDefinitionName)
    {
        return outputDefinitionName + "\n" + assemblyDefinitionName;
    }

    private static void AddFailure(
        List<StoneWireframeBakeFailureResponse> failures,
        HashSet<string> failureKeys,
        string code,
        string assemblyDefinitionName,
        string stoneDefinitionName,
        string message)
    {
        string key = string.Join("\n", code, assemblyDefinitionName, stoneDefinitionName, message);
        if (!failureKeys.Add(key))
        {
            return;
        }

        failures.Add(new StoneWireframeBakeFailureResponse
        {
            Code = code,
            AssemblyDefinitionName = assemblyDefinitionName,
            StoneDefinitionName = stoneDefinitionName,
            Message = message
        });
    }

    private static StoneWireframeBakeResponse BuildResponse(
        string filePath,
        string mode,
        PreparedBake prepared,
        int createdDefinitionCount,
        int createdInstanceCount,
        IReadOnlyList<Guid> createdObjectIds)
    {
        var definitionResponses = prepared.Definitions
            .OrderBy(item => item.SourceDefinition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                List<PreparedPlacement> placements = prepared.Placements
                    .Where(item => item.Definition.SourceDefinition.Id == definition.SourceDefinition.Id)
                    .ToList();
                return new StoneWireframeDefinitionResponse
                {
                    SourceDefinitionId = definition.SourceDefinition.Id,
                    SourceDefinitionName = definition.SourceDefinition.Name,
                    StoneOrientation = StoneWireframeBakeNaming.IsFlatStoneDefinitionName(definition.SourceDefinition.Name)
                        ? "Flat"
                        : "Vertical",
                    OutputDefinitionName = definition.OutputName,
                    OutputDefinitionAction = mode == "Apply"
                        ? definition.ExistingDefinition is null ? "Created" : "Reused"
                        : definition.ExistingDefinition is null ? "Create" : "Reuse",
                    OutputGeometryType = prepared.OutputMode == StoneBakeOutputMode.WorldZSurface ? "Surface" : "Curve",
                    SourceObjectId = definition.Selection.SourceObjectId,
                    StartPoint = ToPoint(definition.Selection.Line.From),
                    EndPoint = ToPoint(definition.Selection.Line.To),
                    BasePoint = ToPoint(definition.Selection.Line.From),
                    Length = definition.Selection.Line.Length,
                    OccurrenceCount = definition.OccurrenceCount,
                    WouldCreateInstanceCount = placements.Count(item => !item.AlreadyExists),
                    ExistingInstanceCount = placements.Count(item => item.AlreadyExists)
                };
            })
            .ToList();

        var assemblyResponses = prepared.Occurrences
            .GroupBy(item => item.AssemblyDefinitionName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StoneWireframeAssemblyResponse
            {
                AssemblyDefinitionName = group.Key,
                AssemblyInstanceCount = group.Select(item => item.AssemblyInstanceId).Distinct().Count(),
                StoneOccurrenceCount = group.Count(),
                StoneDefinitionNames = group.Select(item => item.StoneDefinition.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();

        var warnings = new List<ObjectEditWarning>();
        if (createdObjectIds.Count > MaxReturnedObjectIds)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "CREATED_OBJECT_IDS_TRUNCATED",
                Message = $"Returned the first {MaxReturnedObjectIds} of {createdObjectIds.Count} created object ids."
            });
        }

        if (prepared.Placements.Count > MaxReturnedObjectIds)
            warnings.Add(new ObjectEditWarning { Code = "PLACEMENTS_TRUNCATED",
                Message = $"Returned {MaxReturnedObjectIds} of {prepared.Placements.Count} placements." });

        return new StoneWireframeBakeResponse
        {
            FilePath = filePath,
            Mode = mode,
            OutputMode = prepared.OutputMode.ToString(),
            DocumentUnits = prepared.DocumentUnits,
            SurfaceHeightInches = prepared.SurfaceHeightInches,
            SurfaceHeightModelUnits = prepared.SurfaceHeight,
            AssemblyDefinitionCount = prepared.Assemblies.Select(item => item.InstanceDefinition.Id).Distinct().Count(),
            AssemblyInstanceCount = prepared.Assemblies.Count,
            StoneDefinitionCount = prepared.Occurrences.Select(item => item.StoneDefinition.Id).Distinct().Count(),
            StoneOccurrenceCount = prepared.Occurrences.Count,
            WouldCreateDefinitionCount = prepared.Definitions.Count(item => item.ExistingDefinition is null),
            WouldReuseDefinitionCount = prepared.Definitions.Count(item => item.ExistingDefinition is not null),
            WouldCreateInstanceCount = prepared.Placements.Count(item => !item.AlreadyExists),
            ExistingInstanceCount = prepared.Placements.Count(item => item.AlreadyExists),
            CreatedDefinitionCount = createdDefinitionCount,
            CreatedInstanceCount = createdInstanceCount,
            FailedCount = prepared.Failures.Count,
            Definitions = definitionResponses,
            Assemblies = assemblyResponses,
            Placements = prepared.Placements.Take(MaxReturnedObjectIds).Select(placement =>
            {
                Point3d start = placement.Definition.Selection.Line.From;
                Point3d end = placement.Definition.Selection.Line.To;
                start.Transform(placement.Occurrence.Transform);
                end.Transform(placement.Occurrence.Transform);
                return new StoneBakePlacementResponse
                {
                    AssemblyInstanceId = placement.Occurrence.AssemblyInstanceId,
                    AssemblyDefinitionName = placement.Occurrence.AssemblyDefinitionName,
                    OutputDefinitionName = placement.Definition.OutputName,
                    OutputObjectId = placement.OutputObjectId,
                    AlreadyExists = placement.AlreadyExists,
                    WorldStartPoint = ToPoint(start), WorldEndPoint = ToPoint(end),
                    WorldTopStartPoint = ToPoint(new Point3d(start.X, start.Y, start.Z + prepared.SurfaceHeight)),
                    WorldTopEndPoint = ToPoint(new Point3d(end.X, end.Y, end.Z + prepared.SurfaceHeight))
                };
            }).ToList(),
            Failures = prepared.Failures,
            CreatedObjectIds = createdObjectIds.Take(MaxReturnedObjectIds).ToList(),
            Warnings = warnings
        };
    }

    private static StoneWireframePointResponse ToPoint(Point3d point)
    {
        return new StoneWireframePointResponse { X = point.X, Y = point.Y, Z = point.Z };
    }

    private sealed record StoneOccurrence(
        Guid AssemblyInstanceId,
        string AssemblyDefinitionName,
        InstanceDefinition StoneDefinition,
        Transform Transform,
        int LayerIndex);

    private sealed record SelectedLine(Guid SourceObjectId, ObjectAttributes SourceAttributes, Line Line);

    private sealed record LineCandidate(Guid SourceObjectId, ObjectAttributes SourceAttributes, Line Line)
    {
        public double MidY => (Line.From.Y + Line.To.Y) * 0.5d;
    }

    private sealed record PreparedDefinition(
        InstanceDefinition SourceDefinition,
        string OutputName,
        SelectedLine Selection,
        InstanceDefinition? ExistingDefinition,
        int OccurrenceCount,
        GeometryBase OutputGeometry);

    private sealed record PreparedPlacement(
        PreparedDefinition Definition,
        StoneOccurrence Occurrence,
        Transform Transform,
        bool AlreadyExists)
    {
        public Guid OutputObjectId { get; set; }
    }

    private sealed class ExistingPlacement
    {
        public ExistingPlacement(Guid objectId, Transform transform)
        {
            ObjectId = objectId;
            Transform = transform;
        }

        public Guid ObjectId { get; }
        public Transform Transform { get; }
        public bool Consumed { get; set; }
    }

    private sealed record PreparedBake(
        IReadOnlyList<InstanceObject> Assemblies,
        IReadOnlyList<StoneOccurrence> Occurrences,
        IReadOnlyList<PreparedDefinition> Definitions,
        IReadOnlyList<PreparedPlacement> Placements,
        IReadOnlyList<StoneWireframeBakeFailureResponse> Failures,
        StoneBakeOutputMode OutputMode,
        double SurfaceHeightInches,
        double SurfaceHeight,
        string DocumentUnits) : IDisposable
    {
        public void Dispose()
        {
            foreach (PreparedDefinition definition in Definitions) definition.OutputGeometry.Dispose();
        }
    }
}
