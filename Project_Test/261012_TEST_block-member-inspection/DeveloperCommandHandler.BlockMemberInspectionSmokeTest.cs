using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterBlockMemberInspectionHandlers()
        => _extensionHandlers["block-member-inspection-smoke-test"] = HandleBlockMemberInspectionSmokeTest;

    private bool HandleBlockMemberInspectionSmokeTest(string[] args)
    {
        try
        {
            var recording = new RecordingBlockMemberInspector();
            var service = new RhinoBlockMemberInspectionService(recording);
            Guid id = Guid.NewGuid();
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            foreach (var ids in new[] { new List<Guid>(), new List<Guid> { Guid.Empty }, Enumerable.Repeat(id, 1001).ToList() })
                Require(!service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = ids }).Success, "Invalid root list accepted.");
            Require(!service.Inspect(new() { FilePath = "", ObjectIds = new() { id } }).Success, "Empty path accepted.");
            foreach (int limit in new[] { 0, -1, 50001 })
                Require(!service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = new() { id }, MaxLeaves = limit }).Success, "Invalid leaf limit accepted.");
            Require(recording.Request is null, "Invalid requests reached adapter.");
            Require(service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = new() { id, id } }).Success
                && recording.Request?.ObjectIds.Count == 1, "Duplicate roots not deduplicated.");
            var live = new RhinoBlockMemberInspectionService(new LiveBlockMemberInspector(_liveRhinoDocumentAccessor));
            if (McpRhinoPlugin.Instance is null)
            {
                var response = live.Inspect(new() { FilePath = "C:/fixture.3dm", ObjectIds = new() { id } });
                Require(!response.Success && response.Message.Contains("LIVE_RHINO_REQUIRED"), "CLI must require live Rhino.");
            }
            else
            {
                Require(args.Length > 1, "Live smoke requires saved document path.");
                var instances = _rhinoBlockInspectionService.ListInstances(new() { FilePath = args[1], IncludeHidden = true });
                Require(instances.Success && instances.Data is not null, "Cannot list live instances.");
                var ids = instances.Data!.Instances.Take(25).Select(i => i.ObjectId).ToList();
                Require(ids.Count > 0, "Live smoke requires at least one block instance.");
                var response = live.Inspect(new() { FilePath = args[1], ObjectIds = ids });
                Require(response.Success && response.Data?.RootInstanceCount == ids.Count, response.Message);
                Console.WriteLine($"[OK] Read {response.Data!.LeafCount} live members.");
            }
            Console.WriteLine("[OK] block-member-inspection: validation, root deduplication, and live-only routing.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        return true;
    }

    private sealed class RecordingBlockMemberInspector : ILiveBlockMemberInspector
    {
        public BlockMemberInspectionRequest? Request { get; private set; }
        public OperationResponse<BlockMemberInspectionResponse> Inspect(BlockMemberInspectionRequest request)
        {
            Request = request;
            return OperationResponse<BlockMemberInspectionResponse>.Ok(new());
        }
    }
}
