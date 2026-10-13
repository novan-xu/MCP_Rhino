using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    // Extends the restored, existing stone-wireframe-bake-smoke-test entry point.
    private static void VerifyStoneAssemblyGaHyphenService()
    {
        var recording = new GaHyphenRecordingOperator();
        var service = new RhinoStoneWireframeBakeService(recording);
        const string path = "C:/mcp-rhino/naming-regression.3dm";
        const string parent = "STN-MET-GA-2010S-020";
        foreach (bool apply in new[] { false, true })
        {
            var names = new[] { " " + parent + " ", parent.ToLowerInvariant() };
            var result = apply ? service.Apply(path, names) : service.Preview(path, names);
            RequireStoneWireframe(result.Success && recording.Names.SequenceEqual(new[] { parent }),
                "Current assembly name must reach the live adapter with exact spelling and deduplicated scope.");
            int calls = recording.Calls;
            foreach (string invalid in new[] { "STN-MET-GA2010S-020", parent + "_EL", parent + "_V" })
            {
                result = apply ? service.Apply(path, new[] { invalid }) : service.Preview(path, new[] { invalid });
                RequireStoneWireframe(!result.Success && recording.Calls == calls,
                    "Excluded/legacy assemblies must be rejected before live access.");
            }
        }

        var settings = LiveStoneWireframeBakeOperator.CreateInstanceEnumerationSettings();
        RequireStoneWireframe(settings.NormalObjects && settings.LockedObjects && settings.HiddenObjects
            && settings.ActiveObjects && !settings.IdefObjects && !settings.DeletedObjects && !settings.ReferenceObjects,
            "Bake scope must include normal, locked, and hidden local instances without deleted or definition objects.");
        Console.WriteLine("[OK] GA- naming, exact parent scope, legacy/view exclusions, and complete instance enumeration.");
    }

    private sealed class GaHyphenRecordingOperator : ILiveStoneWireframeBakeOperator
    {
        public int Calls { get; private set; }
        public IReadOnlyCollection<string> Names { get; private set; } = Array.Empty<string>();
        public OperationResponse<StoneWireframeBakeResponse> Preview(string filePath, IReadOnlyCollection<string> names, StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve, double surfaceHeightInches = 5) => Record(names);
        public OperationResponse<StoneWireframeBakeResponse> Apply(string filePath, IReadOnlyCollection<string> names, StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve, double surfaceHeightInches = 5) => Record(names);
        private OperationResponse<StoneWireframeBakeResponse> Record(IReadOnlyCollection<string> names)
        {
            Calls++;
            Names = names;
            return OperationResponse<StoneWireframeBakeResponse>.Ok(new StoneWireframeBakeResponse());
        }
    }
}
