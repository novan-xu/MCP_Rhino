using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live.Grasshopper;

public sealed class GrasshopperGraphProjector
{
    public GrasshopperGraphResponse Project(GH_Document document, string sessionId, int dataSampleSize)
    {
        int remainingSamples = GrasshopperLimits.MaxTotalSampledItems;
        bool truncated = false;
        var nodes = new List<GrasshopperGraphNode>();
        var wires = new List<GrasshopperGraphWire>();

        foreach (IGH_DocumentObject obj in document.Objects)
        {
            IReadOnlyList<IGH_Param> inputs = GetInputs(obj);
            IReadOnlyList<IGH_Param> outputs = GetOutputs(obj);
            nodes.Add(new GrasshopperGraphNode
            {
                ObjectId = obj.InstanceGuid,
                ComponentGuid = obj.ComponentGuid,
                Name = obj.Name ?? string.Empty,
                NickName = obj.NickName ?? string.Empty,
                ObjectKind = obj.GetType().Name,
                CanvasX = obj.Attributes?.Pivot.X ?? 0,
                CanvasY = obj.Attributes?.Pivot.Y ?? 0,
                IsExecutableCodeComponent = GrasshopperComponentCatalog.IsExecutableCodeObject(obj),
                Inputs = inputs.Select((param, index) => ProjectParameter(param, index, dataSampleSize, ref remainingSamples, ref truncated)).ToList(),
                Outputs = outputs.Select((param, index) => ProjectParameter(param, index, dataSampleSize, ref remainingSamples, ref truncated)).ToList()
            });

            for (int destinationIndex = 0; destinationIndex < inputs.Count; destinationIndex++)
            {
                IGH_Param destination = inputs[destinationIndex];
                foreach (IGH_Param source in destination.Sources)
                {
                    IGH_DocumentObject sourceOwner = TopLevelOwner(source);
                    IReadOnlyList<IGH_Param> sourceOutputs = GetOutputs(sourceOwner);
                    int sourceIndex = IndexOfReference(sourceOutputs, source);
                    wires.Add(new GrasshopperGraphWire
                    {
                        SourceObjectId = sourceOwner.InstanceGuid,
                        SourceParameterIndex = Math.Max(0, sourceIndex),
                        DestinationObjectId = obj.InstanceGuid,
                        DestinationParameterIndex = destinationIndex
                    });
                }
            }
        }

        return new GrasshopperGraphResponse
        {
            DefinitionSessionId = sessionId,
            DefinitionRevision = ComputeRevision(document),
            Nodes = nodes,
            Wires = wires,
            Diagnostics = CollectDiagnostics(document),
            DataTruncated = truncated
        };
    }

    public static string ComputeRevision(GH_Document document)
    {
        var builder = new StringBuilder();
        foreach (IGH_DocumentObject obj in document.Objects.OrderBy(item => item.InstanceGuid))
        {
            builder.Append(obj.InstanceGuid).Append('|')
                .Append(obj.ComponentGuid).Append('|')
                .Append(obj.Name).Append('|')
                .Append(obj.NickName).Append('|')
                .Append(obj.Attributes?.Pivot.X).Append(',')
                .Append(obj.Attributes?.Pivot.Y).Append('|');
            if (obj is IGH_ActiveObject active)
            {
                builder.Append(active.Locked).Append('|');
            }
            if (obj is GH_NumberSlider slider)
            {
                builder.Append(slider.Slider.Minimum).Append('|')
                    .Append(slider.Slider.Maximum).Append('|')
                    .Append(slider.Slider.Value).Append('|')
                    .Append(slider.Slider.DecimalPlaces).Append('|');
            }
            foreach (IGH_Param destination in GetInputs(obj))
            {
                builder.Append('[').Append(destination.InstanceGuid).Append(':')
                    .Append(destination.Name).Append(':')
                    .Append(destination.NickName).Append(':')
                    .Append(destination.Access).Append(':')
                    .Append(destination.Optional).Append(':');
                foreach (IGH_Param source in destination.Sources)
                {
                    builder.Append(source.InstanceGuid).Append(',');
                }
                builder.Append(']');
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static int CountWires(GH_Document document) =>
        document.Objects.Sum(obj => GetInputs(obj).Sum(param => param.SourceCount));

    public static IReadOnlyList<IGH_Param> GetInputs(IGH_DocumentObject obj) => obj switch
    {
        IGH_Component component => component.Params.Input,
        IGH_Param param => [param],
        _ => []
    };

    public static IReadOnlyList<IGH_Param> GetOutputs(IGH_DocumentObject obj) => obj switch
    {
        IGH_Component component => component.Params.Output,
        IGH_Param param => [param],
        _ => []
    };

    public static List<GrasshopperObjectDiagnostic> CollectDiagnostics(GH_Document document)
    {
        var results = new List<GrasshopperObjectDiagnostic>();
        foreach (IGH_ActiveObject active in document.Objects.OfType<IGH_ActiveObject>())
        {
            AddMessages(results, active, GH_RuntimeMessageLevel.Remark, GrasshopperDiagnosticSeverity.Remark);
            AddMessages(results, active, GH_RuntimeMessageLevel.Warning, GrasshopperDiagnosticSeverity.Warning);
            AddMessages(results, active, GH_RuntimeMessageLevel.Error, GrasshopperDiagnosticSeverity.Error);
        }
        return results;
    }

    private static GrasshopperParameterDataSummary ProjectParameter(
        IGH_Param parameter,
        int index,
        int requested,
        ref int remaining,
        ref bool graphTruncated)
    {
        int take = Math.Min(requested, remaining);
        List<string> samples = take == 0
            ? []
            : parameter.VolatileData.AllData(true)
                .Take(take)
                .Select(item => item?.ToString() ?? "null")
                .ToList();
        remaining -= samples.Count;
        bool truncated = requested > 0 && parameter.VolatileDataCount > samples.Count;
        graphTruncated |= truncated || (requested > 0 && remaining == 0);
        return new GrasshopperParameterDataSummary
        {
            Index = index,
            Name = parameter.Name ?? string.Empty,
            NickName = parameter.NickName ?? string.Empty,
            VolatileDataCount = parameter.VolatileDataCount,
            Truncated = truncated,
            Samples = samples
        };
    }

    private static IGH_DocumentObject TopLevelOwner(IGH_Param parameter) =>
        parameter.Attributes?.GetTopLevel.DocObject ?? parameter;

    private static int IndexOfReference(IReadOnlyList<IGH_Param> parameters, IGH_Param target)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (ReferenceEquals(parameters[i], target))
            {
                return i;
            }
        }
        return -1;
    }

    private static void AddMessages(
        ICollection<GrasshopperObjectDiagnostic> destination,
        IGH_ActiveObject active,
        GH_RuntimeMessageLevel level,
        GrasshopperDiagnosticSeverity severity)
    {
        foreach (string message in active.RuntimeMessages(level))
        {
            destination.Add(new GrasshopperObjectDiagnostic
            {
                ObjectId = active.InstanceGuid,
                Name = active.Name ?? string.Empty,
                NickName = active.NickName ?? string.Empty,
                Severity = severity,
                Message = message
            });
        }
    }
}
