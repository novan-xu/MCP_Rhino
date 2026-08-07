extern alias rhinocommon;

using System.Drawing;
using System.Runtime.CompilerServices;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Undo;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoPlugIn = rhinocommon::Rhino.PlugIns.PlugIn;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live.Grasshopper;

public sealed class LiveGrasshopperOperator : ILiveGrasshopperOperator
{
    private static readonly Guid GrasshopperPluginId = new("B45A29B1-4343-4035-989E-044E8580D9CF");
    private readonly ILiveRhinoDocumentAccessor _accessor;
    private readonly GrasshopperDefinitionRegistry _registry;
    private readonly GrasshopperComponentCatalog _catalog;
    private readonly GrasshopperGraphProjector _projector;

    public LiveGrasshopperOperator(
        ILiveRhinoDocumentAccessor accessor,
        GrasshopperDefinitionRegistry registry,
        GrasshopperComponentCatalog catalog,
        GrasshopperGraphProjector projector)
    {
        _accessor = accessor;
        _registry = registry;
        _catalog = catalog;
        _projector = projector;
    }

    public OperationResponse<GrasshopperDefinitionListResponse> Start(GrasshopperEngineRequest request) =>
        _accessor.Execute(request.FilePath, rhinoDocument =>
        {
            OperationResponse runtime = EnsureRuntime();
            if (!runtime.Success)
            {
                return OperationResponse<GrasshopperDefinitionListResponse>.Fail(runtime.Message);
            }

            if (!RhinoApp.RunScript(rhinoDocument.RuntimeSerialNumber, "_Grasshopper", false))
            {
                return OperationResponse<GrasshopperDefinitionListResponse>.Fail("GRASSHOPPER_RUNTIME_UNAVAILABLE: the Grasshopper command did not start.");
            }

            return BuildDefinitionListLoaded(rhinoDocument, request.Engine);
        });

    public OperationResponse<GrasshopperDefinitionListResponse> ListDefinitions(GrasshopperEngineRequest request) =>
        _accessor.Execute(request.FilePath, rhinoDocument =>
        {
            OperationResponse runtime = EnsureRuntime();
            return runtime.Success
                ? BuildDefinitionListLoaded(rhinoDocument, request.Engine)
                : OperationResponse<GrasshopperDefinitionListResponse>.Fail(runtime.Message);
        });

    public OperationResponse<GrasshopperComponentSearchResponse> SearchComponents(SearchGrasshopperComponentsRequest request) =>
        _accessor.Execute(request.FilePath, _ =>
        {
            OperationResponse runtime = EnsureRuntime();
            if (!runtime.Success)
            {
                return OperationResponse<GrasshopperComponentSearchResponse>.Fail(runtime.Message);
            }

            return SearchComponentsLoaded(request);
        });

    public OperationResponse<GrasshopperComponentDescriptionResponse> DescribeComponent(DescribeGrasshopperComponentRequest request) =>
        _accessor.Execute(request.FilePath, _ =>
        {
            OperationResponse runtime = EnsureRuntime();
            if (!runtime.Success)
            {
                return OperationResponse<GrasshopperComponentDescriptionResponse>.Fail(runtime.Message);
            }

            return DescribeComponentLoaded(request);
        });

    public OperationResponse<GrasshopperGraphResponse> GetGraph(GetGrasshopperGraphRequest request) =>
        ExecuteAfterRuntime(request.FilePath, rhinoDocument => GetGraphLoaded(request, rhinoDocument));

    public OperationResponse<GrasshopperGraphPreviewResponse> PreviewGraph(PreviewApplyGrasshopperGraphRequest request) =>
        ExecuteAfterRuntime(request.FilePath, rhinoDocument => PreviewGraphLoaded(request, rhinoDocument));

    public OperationResponse<GrasshopperGraphApplyResponse> ApplyGraph(
        ApplyGrasshopperGraphRequest request,
        GrasshopperGraphPreviewResponse preview) =>
        ExecuteAfterRuntime(request.FilePath, rhinoDocument => ApplyGraphLoaded(request, preview, rhinoDocument));

    public OperationResponse<GrasshopperSolveResponse> Solve(SolveGrasshopperDefinitionRequest request) =>
        ExecuteAfterRuntime(request.FilePath, rhinoDocument => SolveLoaded(request, rhinoDocument));

    public OperationResponse<GrasshopperClearPreviewResponse> PreviewClear(GrasshopperDefinitionRequest request) =>
        ExecuteAfterRuntime(request.FilePath, rhinoDocument => PreviewClearLoaded(request, rhinoDocument));

    public OperationResponse<GrasshopperClearApplyResponse> ApplyClear(
        ApplyClearGrasshopperDefinitionRequest request,
        GrasshopperClearPreviewResponse preview) =>
        ExecuteAfterRuntime(request.FilePath, rhinoDocument => ApplyClearLoaded(request, preview, rhinoDocument));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperComponentSearchResponse> SearchComponentsLoaded(SearchGrasshopperComponentsRequest request)
    {
        IReadOnlyList<IGH_ObjectProxy> all = _catalog.Find(request.Query);
        List<GrasshopperComponentSummary> results = all
            .Take(request.MaxResults)
            .Select(_catalog.ToSummary)
            .ToList();
        return OperationResponse<GrasshopperComponentSearchResponse>.Ok(new GrasshopperComponentSearchResponse
        {
            Query = request.Query,
            TotalMatches = all.Count,
            Truncated = all.Count > results.Count,
            Components = results
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperComponentDescriptionResponse> DescribeComponentLoaded(DescribeGrasshopperComponentRequest request)
    {
        GrasshopperComponentCatalog.OperationResult resolved = _catalog.Resolve(request.ComponentGuid, null);
        if (resolved.Proxy is null)
        {
            return OperationResponse<GrasshopperComponentDescriptionResponse>.Fail("GRASSHOPPER_COMPONENT_NOT_FOUND");
        }

        IGH_DocumentObject instance;
        try
        {
            instance = resolved.Proxy.CreateInstance();
        }
        catch (Exception ex)
        {
            return OperationResponse<GrasshopperComponentDescriptionResponse>.Fail($"GRASSHOPPER_COMPONENT_NOT_FOUND: component construction failed: {ex.Message}");
        }

        var description = new GrasshopperComponentDescriptionResponse
        {
            Component = _catalog.ToSummary(resolved.Proxy),
            Inputs = GrasshopperGraphProjector.GetInputs(instance).Select(ToParameterDescription).ToList(),
            Outputs = GrasshopperGraphProjector.GetOutputs(instance).Select(ToParameterDescription).ToList()
        };
        if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
        return OperationResponse<GrasshopperComponentDescriptionResponse>.Ok(description);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperGraphResponse> GetGraphLoaded(GetGrasshopperGraphRequest request, RhinoDoc rhinoDocument) =>
        WithDefinitionLoaded(request, rhinoDocument, document =>
            OperationResponse<GrasshopperGraphResponse>.Ok(
                _projector.Project(document, request.DefinitionSessionId, request.DataSampleSize)));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperGraphPreviewResponse> PreviewGraphLoaded(PreviewApplyGrasshopperGraphRequest request, RhinoDoc rhinoDocument) =>
        WithDefinitionLoaded(request, rhinoDocument, document => PreviewGraphCore(document, request));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperGraphApplyResponse> ApplyGraphLoaded(
        ApplyGrasshopperGraphRequest request,
        GrasshopperGraphPreviewResponse preview,
        RhinoDoc rhinoDocument) =>
        WithDefinitionLoaded(request, rhinoDocument, document => ApplyGraphCore(document, request, preview));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperSolveResponse> SolveLoaded(SolveGrasshopperDefinitionRequest request, RhinoDoc rhinoDocument) =>
        WithDefinitionLoaded(request, rhinoDocument, document =>
        {
            try
            {
                document.NewSolution(request.ExpireAllObjects);
                return OperationResponse<GrasshopperSolveResponse>.Ok(BuildSolveResponse(document, request));
            }
            catch (Exception ex)
            {
                return OperationResponse<GrasshopperSolveResponse>.Fail($"GRASSHOPPER_SOLVE_FAILED: {ex.Message}");
            }
        });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperClearPreviewResponse> PreviewClearLoaded(GrasshopperDefinitionRequest request, RhinoDoc rhinoDocument) =>
        WithDefinitionLoaded(request, rhinoDocument, document => OperationResponse<GrasshopperClearPreviewResponse>.Ok(new GrasshopperClearPreviewResponse
        {
            DefinitionRevision = GrasshopperGraphProjector.ComputeRevision(document),
            ObjectIds = document.Objects.Select(obj => obj.InstanceGuid).ToList(),
            WireCount = GrasshopperGraphProjector.CountWires(document)
        }));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperClearApplyResponse> ApplyClearLoaded(
        ApplyClearGrasshopperDefinitionRequest request,
        GrasshopperClearPreviewResponse preview,
        RhinoDoc rhinoDocument) =>
        WithDefinitionLoaded(request, rhinoDocument, document =>
        {
            if (!string.Equals(GrasshopperGraphProjector.ComputeRevision(document), preview.DefinitionRevision, StringComparison.Ordinal))
            {
                return OperationResponse<GrasshopperClearApplyResponse>.Fail("GRASSHOPPER_GRAPH_CHANGED");
            }

            List<IGH_DocumentObject> objects = document.Objects.ToList();
            int wires = GrasshopperGraphProjector.CountWires(document);
            if (objects.Count == 0)
            {
                return OperationResponse<GrasshopperClearApplyResponse>.Ok(new GrasshopperClearApplyResponse
                {
                    DefinitionRevision = preview.DefinitionRevision
                });
            }

            bool priorSolutions = GH_Document.EnableSolutions;
            try
            {
                GH_Document.EnableSolutions = false;
                document.UndoUtil.RecordRemoveObjectEvent("MCP Clear Grasshopper Definition", objects);
                document.RemoveObjects(objects, false);
                document.Modified();
                return OperationResponse<GrasshopperClearApplyResponse>.Ok(new GrasshopperClearApplyResponse
                {
                    DefinitionRevision = GrasshopperGraphProjector.ComputeRevision(document),
                    RemovedObjectCount = objects.Count,
                    RemovedWireCount = wires
                });
            }
            catch (Exception ex)
            {
                return OperationResponse<GrasshopperClearApplyResponse>.Fail($"GRASSHOPPER_APPLY_FAILED: {ex.Message}");
            }
            finally
            {
                GH_Document.EnableSolutions = priorSolutions;
            }
        });

    private OperationResponse<GrasshopperGraphPreviewResponse> PreviewGraphCore(
        GH_Document document,
        PreviewApplyGrasshopperGraphRequest request)
    {
        var working = new Dictionary<string, IGH_DocumentObject>(StringComparer.Ordinal);
        var resolvedNodes = new List<ResolvedGrasshopperNode>();
        var resolvedWires = new List<ResolvedGrasshopperWire>();
        var warnings = new List<string>();
        var temporaryObjects = new List<IGH_DocumentObject>();

        try
        {
            foreach (GrasshopperNodeSpec node in request.Graph.Nodes)
            {
                if (node.ExistingObjectId is Guid existingId)
                {
                    IGH_DocumentObject? existing = FindObject(document, existingId);
                    if (existing is null)
                    {
                        return OperationResponse<GrasshopperGraphPreviewResponse>.Fail("GRASSHOPPER_PARAMETER_NOT_FOUND: an existing graph object was not found.");
                    }
                    working[node.ClientKey] = existing;
                    resolvedNodes.Add(new ResolvedGrasshopperNode
                    {
                        ClientKey = node.ClientKey,
                        Kind = node.Kind,
                        ExistingObjectId = existingId,
                        ComponentGuid = existing.ComponentGuid,
                        IsExecutableCodeComponent = GrasshopperComponentCatalog.IsExecutableCodeObject(existing)
                    });
                    continue;
                }

                if (node.Kind == GrasshopperNodeKind.NumberSlider)
                {
                    GH_NumberSlider slider = CreateSlider(node);
                    temporaryObjects.Add(slider);
                    working[node.ClientKey] = slider;
                    resolvedNodes.Add(new ResolvedGrasshopperNode
                    {
                        ClientKey = node.ClientKey,
                        Kind = node.Kind,
                        ComponentGuid = slider.ComponentGuid
                    });
                    continue;
                }

                GrasshopperComponentCatalog.OperationResult component = _catalog.Resolve(node.ComponentGuid, node.ComponentName);
                if (component.IsAmbiguous)
                {
                    string candidates = string.Join(", ", component.Candidates.Take(10).Select(item => $"{item.Desc.Name} ({item.Guid})"));
                    return OperationResponse<GrasshopperGraphPreviewResponse>.Fail($"GRASSHOPPER_COMPONENT_AMBIGUOUS: {candidates}");
                }
                if (component.Proxy is null)
                {
                    return OperationResponse<GrasshopperGraphPreviewResponse>.Fail("GRASSHOPPER_COMPONENT_NOT_FOUND");
                }

                IGH_DocumentObject instance;
                try
                {
                    instance = component.Proxy.CreateInstance();
                }
                catch (Exception ex)
                {
                    return OperationResponse<GrasshopperGraphPreviewResponse>.Fail($"GRASSHOPPER_COMPONENT_NOT_FOUND: component construction failed: {ex.Message}");
                }
                working[node.ClientKey] = instance;
                temporaryObjects.Add(instance);
                bool executable = GrasshopperComponentCatalog.ClassifyExecutableCode(component.Proxy).IsExecutable;
                if (executable)
                {
                    warnings.Add($"Executable code component '{component.Proxy.Desc.Name}' ({component.Proxy.Guid}) will be placed and may access external state when solved.");
                }
                resolvedNodes.Add(new ResolvedGrasshopperNode
                {
                    ClientKey = node.ClientKey,
                    Kind = node.Kind,
                    ComponentGuid = component.Proxy.Guid,
                    IsExecutableCodeComponent = executable
                });
            }

            foreach (GrasshopperWireSpec wire in request.Graph.Wires)
            {
                OperationResponse<(IGH_DocumentObject Source, IGH_DocumentObject Destination)> endpoints =
                    ResolveEndpoints(document, working, wire);
                if (!endpoints.Success)
                {
                    return OperationResponse<GrasshopperGraphPreviewResponse>.Fail(endpoints.Message);
                }

                (IGH_DocumentObject sourceObject, IGH_DocumentObject destinationObject) = endpoints.Data;
                OperationResponse<int> sourceIndex = ResolveParameterIndex(
                    GrasshopperGraphProjector.GetOutputs(sourceObject), wire.SourceParameter);
                if (!sourceIndex.Success)
                {
                    return OperationResponse<GrasshopperGraphPreviewResponse>.Fail(sourceIndex.Message);
                }
                OperationResponse<int> destinationIndex = ResolveParameterIndex(
                    GrasshopperGraphProjector.GetInputs(destinationObject), wire.DestinationParameter);
                if (!destinationIndex.Success)
                {
                    return OperationResponse<GrasshopperGraphPreviewResponse>.Fail(destinationIndex.Message);
                }

                resolvedWires.Add(new ResolvedGrasshopperWire
                {
                    SourceNodeKey = wire.SourceNodeKey,
                    SourceObjectId = wire.SourceObjectId,
                    SourceParameterIndex = sourceIndex.Data,
                    DestinationNodeKey = wire.DestinationNodeKey,
                    DestinationObjectId = wire.DestinationObjectId,
                    DestinationParameterIndex = destinationIndex.Data
                });
            }

            return OperationResponse<GrasshopperGraphPreviewResponse>.Ok(new GrasshopperGraphPreviewResponse
            {
                DefinitionRevision = GrasshopperGraphProjector.ComputeRevision(document),
                PredictedNodeAdditions = request.Graph.Nodes.Count(node => node.ExistingObjectId is null),
                PredictedWireAdditions = request.Graph.Wires.Count,
                ResolvedNodes = resolvedNodes,
                ResolvedWires = resolvedWires,
                Warnings = warnings
            });
        }
        finally
        {
            foreach (IDisposable disposable in temporaryObjects.OfType<IDisposable>())
            {
                disposable.Dispose();
            }
        }
    }

    private OperationResponse<GrasshopperGraphApplyResponse> ApplyGraphCore(
        GH_Document document,
        ApplyGrasshopperGraphRequest request,
        GrasshopperGraphPreviewResponse preview)
    {
        if (!string.Equals(GrasshopperGraphProjector.ComputeRevision(document), preview.DefinitionRevision, StringComparison.Ordinal))
        {
            return OperationResponse<GrasshopperGraphApplyResponse>.Fail("GRASSHOPPER_GRAPH_CHANGED");
        }

        var objects = new Dictionary<string, IGH_DocumentObject>(StringComparer.Ordinal);
        var created = new List<IGH_DocumentObject>();
        var createdIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var connectedDestinations = new List<(IGH_Param Destination, IGH_Param Source)>();
        GH_UndoRecord? wireUndoRecord = null;
        Guid pushedUndoGuid = Guid.Empty;
        bool priorSolutions = GH_Document.EnableSolutions;

        try
        {
            GH_Document.EnableSolutions = false;
            for (int i = 0; i < request.Graph.Nodes.Count; i++)
            {
                GrasshopperNodeSpec node = request.Graph.Nodes[i];
                ResolvedGrasshopperNode resolved = preview.ResolvedNodes[i];
                if (node.ExistingObjectId is Guid existingId)
                {
                    IGH_DocumentObject? existing = FindObject(document, existingId);
                    if (existing is null)
                    {
                        throw new InvalidOperationException("GRASSHOPPER_GRAPH_CHANGED: an existing object disappeared.");
                    }
                    objects[node.ClientKey] = existing;
                    continue;
                }

                IGH_DocumentObject instance = node.Kind == GrasshopperNodeKind.NumberSlider
                    ? CreateSlider(node)
                    : Instances.ComponentServer.EmitObject(resolved.ComponentGuid!.Value)
                        ?? throw new InvalidOperationException("GRASSHOPPER_COMPONENT_NOT_FOUND");
                ConfigureInstance(instance, node);
                if (!document.AddObject(instance, false))
                {
                    throw new InvalidOperationException("Grasshopper rejected a graph object.");
                }
                objects[node.ClientKey] = instance;
                created.Add(instance);
                createdIds[node.ClientKey] = instance.InstanceGuid;
            }

            List<IGH_Param> existingDestinations = [];
            foreach (ResolvedGrasshopperWire wire in preview.ResolvedWires)
            {
                IGH_DocumentObject sourceObject = ResolveAppliedObject(document, objects, wire.SourceNodeKey, wire.SourceObjectId);
                IGH_DocumentObject destinationObject = ResolveAppliedObject(document, objects, wire.DestinationNodeKey, wire.DestinationObjectId);
                IGH_Param source = GrasshopperGraphProjector.GetOutputs(sourceObject)[wire.SourceParameterIndex];
                IGH_Param destination = GrasshopperGraphProjector.GetInputs(destinationObject)[wire.DestinationParameterIndex];
                if (!created.Contains(destinationObject))
                {
                    existingDestinations.Add(destination);
                }
            }

            if (existingDestinations.Count > 0)
            {
                wireUndoRecord = document.UndoUtil.CreateWireEvent(
                    "MCP Apply Grasshopper Graph Wires",
                    existingDestinations.Distinct());
            }

            foreach (ResolvedGrasshopperWire wire in preview.ResolvedWires)
            {
                IGH_DocumentObject sourceObject = ResolveAppliedObject(document, objects, wire.SourceNodeKey, wire.SourceObjectId);
                IGH_DocumentObject destinationObject = ResolveAppliedObject(document, objects, wire.DestinationNodeKey, wire.DestinationObjectId);
                IGH_Param source = GrasshopperGraphProjector.GetOutputs(sourceObject)[wire.SourceParameterIndex];
                IGH_Param destination = GrasshopperGraphProjector.GetInputs(destinationObject)[wire.DestinationParameterIndex];
                destination.AddSource(source);
                connectedDestinations.Add((destination, source));
            }

            var batchUndoRecord = new GH_UndoRecord("MCP Apply Grasshopper Graph");
            if (created.Count > 0)
            {
                GH_UndoRecord addRecord = document.UndoUtil.CreateAddObjectEvent("MCP Apply Grasshopper Graph Objects", created);
                foreach (IGH_UndoAction action in addRecord.Actions)
                {
                    batchUndoRecord.AddAction(action);
                }
            }
            if (wireUndoRecord is not null)
            {
                foreach (IGH_UndoAction action in wireUndoRecord.Actions)
                {
                    batchUndoRecord.AddAction(action);
                }
            }
            if (batchUndoRecord.ActionCount > 0)
            {
                document.UndoServer.PushUndoRecord(batchUndoRecord);
                pushedUndoGuid = batchUndoRecord.Guid;
            }

            document.Modified();
            GH_Document.EnableSolutions = priorSolutions;
            GrasshopperSolveResponse? solve = null;
            if (request.Graph.SolveAfterApply)
            {
                document.NewSolution(false);
                solve = BuildSolveResponse(document, new SolveGrasshopperDefinitionRequest
                {
                    FilePath = request.FilePath,
                    Engine = request.Engine,
                    DefinitionSessionId = request.DefinitionSessionId,
                    ExpireAllObjects = false
                });
            }

            return OperationResponse<GrasshopperGraphApplyResponse>.Ok(new GrasshopperGraphApplyResponse
            {
                DefinitionRevision = GrasshopperGraphProjector.ComputeRevision(document),
                CreatedObjectIds = createdIds,
                AddedNodeCount = created.Count,
                AddedWireCount = connectedDestinations.Count,
                RollbackCompleted = true,
                Solve = solve
            });
        }
        catch (Exception ex)
        {
            bool rollback = true;
            try
            {
                foreach ((IGH_Param destination, IGH_Param source) in connectedDestinations.AsEnumerable().Reverse())
                {
                    destination.RemoveSource(source);
                }
                if (created.Count > 0)
                {
                    document.RemoveObjects(created, false);
                }
                if (pushedUndoGuid != Guid.Empty)
                {
                    document.UndoServer.RemoveRecord(pushedUndoGuid);
                }
            }
            catch
            {
                rollback = false;
            }
            return OperationResponse<GrasshopperGraphApplyResponse>.Fail(
                rollback
                    ? $"GRASSHOPPER_APPLY_FAILED: {ex.Message} ROLLBACK_APPLIED"
                    : $"GRASSHOPPER_ROLLBACK_FAILED: {ex.Message}");
        }
        finally
        {
            GH_Document.EnableSolutions = priorSolutions;
        }
    }

    private OperationResponse<T> ExecuteAfterRuntime<T>(
        string filePath,
        Func<RhinoDoc, OperationResponse<T>> loadedWork) =>
        _accessor.Execute(filePath, rhinoDocument =>
        {
            OperationResponse runtime = EnsureRuntime();
            return runtime.Success
                ? loadedWork(rhinoDocument)
                : OperationResponse<T>.Fail(runtime.Message);
        });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<T> WithDefinitionLoaded<T>(
        GrasshopperDefinitionRequest request,
        RhinoDoc rhinoDocument,
        Func<GH_Document, OperationResponse<T>> work)
    {
        if (!_registry.TryResolve(request.DefinitionSessionId, rhinoDocument.RuntimeSerialNumber, out GH_Document? definition)
            || definition is null)
        {
            return OperationResponse<T>.Fail("GRASSHOPPER_DEFINITION_NOT_FOUND");
        }
        if (definition.RhinoDocument is null
            || definition.RhinoDocument.RuntimeSerialNumber != rhinoDocument.RuntimeSerialNumber)
        {
            return OperationResponse<T>.Fail("GRASSHOPPER_TARGET_UNVERIFIED");
        }
        return work(definition);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private OperationResponse<GrasshopperDefinitionListResponse> BuildDefinitionListLoaded(
        RhinoDoc rhinoDocument,
        GrasshopperEngine engine)
    {
        List<GrasshopperDefinitionSummary> definitions = Instances.DocumentServer
            .Where(document => document.RhinoDocument is not null
                && document.RhinoDocument.RuntimeSerialNumber == rhinoDocument.RuntimeSerialNumber)
            .Select(document => new GrasshopperDefinitionSummary
            {
                DefinitionSessionId = _registry.GetOrCreate(document, rhinoDocument.RuntimeSerialNumber),
                Engine = engine,
                DisplayName = document.DisplayName ?? string.Empty,
                NativeDocumentId = document.DocumentID,
                IsDefinitionSaved = document.IsFilePathDefined,
                RhinoRuntimeSerialNumber = rhinoDocument.RuntimeSerialNumber,
                TargetBindingVerified = true,
                ObjectCount = document.Objects.Count,
                WireCount = GrasshopperGraphProjector.CountWires(document),
                SolutionState = document.SolutionState.ToString(),
                DefinitionRevision = GrasshopperGraphProjector.ComputeRevision(document)
            })
            .ToList();
        return OperationResponse<GrasshopperDefinitionListResponse>.Ok(new GrasshopperDefinitionListResponse
        {
            Engine = engine,
            RuntimeLoaded = true,
            Definitions = definitions
        });
    }

    private static OperationResponse EnsureRuntime()
    {
        try
        {
            if (RhinoPlugIn.Find(GrasshopperPluginId) is null
                && !RhinoPlugIn.LoadPlugIn(GrasshopperPluginId, true, false))
            {
                return OperationResponse.Fail("GRASSHOPPER_RUNTIME_UNAVAILABLE: Rhino could not load the Grasshopper plug-in.");
            }
            return VerifyRuntimeLoaded();
        }
        catch (Exception ex)
        {
            return OperationResponse.Fail($"GRASSHOPPER_RUNTIME_UNAVAILABLE: {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OperationResponse VerifyRuntimeLoaded()
    {
        _ = Instances.DocumentServer;
        _ = Instances.ComponentServer;
        return OperationResponse.Ok();
    }

    private static GrasshopperParameterDescription ToParameterDescription(IGH_Param parameter, int index) => new()
    {
        Index = index,
        Name = parameter.Name ?? string.Empty,
        NickName = parameter.NickName ?? string.Empty,
        Description = parameter.Description ?? string.Empty,
        Access = parameter.Access.ToString(),
        Optional = parameter.Optional,
        TypeName = parameter.TypeName ?? parameter.GetType().Name
    };

    private static GH_NumberSlider CreateSlider(GrasshopperNodeSpec node)
    {
        var slider = new GH_NumberSlider();
        slider.CreateAttributes();
        slider.Slider.Minimum = node.SliderMinimum!.Value;
        slider.Slider.Maximum = node.SliderMaximum!.Value;
        slider.Slider.DecimalPlaces = node.SliderDecimalPlaces ?? 2;
        slider.Slider.Value = node.SliderValue!.Value;
        ConfigureInstance(slider, node);
        return slider;
    }

    private static void ConfigureInstance(IGH_DocumentObject instance, GrasshopperNodeSpec node)
    {
        if (instance.Attributes is null)
        {
            instance.CreateAttributes();
        }
        IGH_Attributes attributes = instance.Attributes
            ?? throw new InvalidOperationException("Grasshopper did not create attributes for the graph object.");
        attributes.Pivot = new PointF(node.CanvasX, node.CanvasY);
        if (!string.IsNullOrWhiteSpace(node.NickName))
        {
            instance.NickName = node.NickName;
        }
    }

    private static IGH_DocumentObject? FindObject(GH_Document document, Guid id) =>
        document.Objects.FirstOrDefault(obj => obj.InstanceGuid == id);

    private static OperationResponse<(IGH_DocumentObject Source, IGH_DocumentObject Destination)> ResolveEndpoints(
        GH_Document document,
        IReadOnlyDictionary<string, IGH_DocumentObject> working,
        GrasshopperWireSpec wire)
    {
        IGH_DocumentObject? source = ResolveEndpoint(document, working, wire.SourceNodeKey, wire.SourceObjectId);
        IGH_DocumentObject? destination = ResolveEndpoint(document, working, wire.DestinationNodeKey, wire.DestinationObjectId);
        return source is null || destination is null
            ? OperationResponse<(IGH_DocumentObject, IGH_DocumentObject)>.Fail("GRASSHOPPER_PARAMETER_NOT_FOUND: wire endpoint was not found.")
            : OperationResponse<(IGH_DocumentObject, IGH_DocumentObject)>.Ok((source, destination));
    }

    private static IGH_DocumentObject? ResolveEndpoint(
        GH_Document document,
        IReadOnlyDictionary<string, IGH_DocumentObject> working,
        string? nodeKey,
        Guid? objectId)
    {
        if (!string.IsNullOrWhiteSpace(nodeKey) && working.TryGetValue(nodeKey, out IGH_DocumentObject? byKey))
        {
            return byKey;
        }
        return objectId is Guid id ? FindObject(document, id) : null;
    }

    private static IGH_DocumentObject ResolveAppliedObject(
        GH_Document document,
        IReadOnlyDictionary<string, IGH_DocumentObject> objects,
        string? nodeKey,
        Guid? objectId) =>
        ResolveEndpoint(document, objects, nodeKey, objectId)
        ?? throw new InvalidOperationException("GRASSHOPPER_GRAPH_CHANGED: wire endpoint disappeared.");

    private static OperationResponse<int> ResolveParameterIndex(
        IReadOnlyList<IGH_Param> parameters,
        GrasshopperParameterSelector selector)
    {
        if (selector.Index is int index)
        {
            return index >= 0 && index < parameters.Count
                ? OperationResponse<int>.Ok(index)
                : OperationResponse<int>.Fail("GRASSHOPPER_PARAMETER_NOT_FOUND: parameter index is out of range.");
        }

        if (!string.IsNullOrWhiteSpace(selector.Name))
        {
            List<int> matches = parameters.Select((param, i) => (param, i))
                .Where(item => string.Equals(item.param.Name, selector.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(item => item.i)
                .ToList();
            return MatchesToIndex(matches);
        }

        if (!string.IsNullOrWhiteSpace(selector.NickName))
        {
            List<int> matches = parameters.Select((param, i) => (param, i))
                .Where(item => string.Equals(item.param.NickName, selector.NickName.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(item => item.i)
                .ToList();
            return MatchesToIndex(matches);
        }

        return parameters.Count == 1
            ? OperationResponse<int>.Ok(0)
            : OperationResponse<int>.Fail("GRASSHOPPER_PARAMETER_NOT_FOUND: selector is required when an object has multiple parameters.");
    }

    private static OperationResponse<int> MatchesToIndex(IReadOnlyList<int> matches) => matches.Count switch
    {
        0 => OperationResponse<int>.Fail("GRASSHOPPER_PARAMETER_NOT_FOUND"),
        1 => OperationResponse<int>.Ok(matches[0]),
        _ => OperationResponse<int>.Fail("GRASSHOPPER_PARAMETER_AMBIGUOUS")
    };

    private GrasshopperSolveResponse BuildSolveResponse(GH_Document document, SolveGrasshopperDefinitionRequest request)
    {
        List<GrasshopperObjectDiagnostic> diagnostics = GrasshopperGraphProjector.CollectDiagnostics(document);
        int warnings = diagnostics.Count(item => item.Severity == GrasshopperDiagnosticSeverity.Warning);
        int errors = diagnostics.Count(item => item.Severity == GrasshopperDiagnosticSeverity.Error);
        int faults = document.Objects.OfType<IGH_ActiveObject>().Count(item => item.Phase == GH_SolutionPhase.Failed);
        return new GrasshopperSolveResponse
        {
            CompletionPhase = document.SolutionState.ToString(),
            Solved = errors == 0 && faults == 0,
            WarningCount = warnings,
            ErrorCount = errors,
            FaultCount = faults,
            DefinitionRevision = GrasshopperGraphProjector.ComputeRevision(document),
            Diagnostics = diagnostics,
            Graph = request.DataSampleSize > 0
                ? _projector.Project(document, request.DefinitionSessionId, request.DataSampleSize)
                : null
        };
    }
}
