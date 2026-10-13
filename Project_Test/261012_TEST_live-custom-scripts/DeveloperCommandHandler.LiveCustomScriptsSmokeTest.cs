using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Rhino.Live.Scripting;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterLiveCustomScriptsHandlers()
        => _extensionHandlers["live-custom-scripts-smoke-test"] = HandleLiveCustomScriptsSmokeTest;

    private bool HandleLiveCustomScriptsSmokeTest(string[] args)
    {
        try
        {
            void Check(bool b) { if (!b) throw new InvalidOperationException("Custom script contract assertion failed."); }
            var fake = new RecordingRhinoScriptExecutor();
            var service = new RhinoScriptService(fake);
            string path = Path.GetFullPath("fixture.3dm");
            foreach (var request in new[] {
                new RhinoScriptRequest { FilePath = "relative.3dm", Script = "Result = 1;" },
                new RhinoScriptRequest { FilePath = path, Script = "" },
                new RhinoScriptRequest { FilePath = path, Script = new string(' ', 131073) },
                new RhinoScriptRequest { FilePath = path, Script = "Result = 1;", ParametersJson = "[]" },
                new RhinoScriptRequest { FilePath = path, Script = "Result = 1;", ParametersJson = "{" },
                new RhinoScriptRequest { FilePath = path, Script = "Result = 1;", TimeoutSeconds = 0 },
                new RhinoScriptRequest { FilePath = path, Script = "Result = 1;", TimeoutSeconds = 601 } })
                Check(!service.Preview(request).Success);
            Check(fake.Calls == 0);
            Check(service.Preview(new() { FilePath = path, Script = "Result = 1;" }).Success && fake.Calls == 1);
            Check(!service.Apply(path, "bad-token").Success && !service.GetStatus("relative", Guid.NewGuid().ToString("N")).Success);
            using var live = new LiveRhinoScriptExecutor(_liveRhinoDocumentAccessor);
            string livePath = args.Length > 1 ? args[1] : path;
            var preview = new RhinoScriptService(live).Preview(new() { FilePath = livePath, Script = "Result = doc.RuntimeSerialNumber;" });
            if (Infrastructure.Plugin.McpRhinoPlugin.Instance is null)
                Check(!preview.Success && preview.Message.Contains("LIVE_RHINO_REQUIRED"));
            else Check(preview.Success && preview.Data?.PreviewKind == "CompilationOnly");
            Console.WriteLine("[OK] live-custom-scripts: contract validation, opaque ids, and live-only compilation preview.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        return true;
    }

    private sealed class RecordingRhinoScriptExecutor : ILiveRhinoScriptExecutor
    {
        public int Calls { get; private set; }
        public OperationResponse<RhinoScriptPreviewResponse> Preview(RhinoScriptRequest request)
        { Calls++; return OperationResponse<RhinoScriptPreviewResponse>.Ok(new()); }
        public OperationResponse<RhinoScriptStatusResponse> Apply(string path, string token) => throw new NotImplementedException();
        public OperationResponse<RhinoScriptStatusResponse> GetStatus(string path, string id) => throw new NotImplementedException();
        public OperationResponse<RhinoScriptStatusResponse> Cancel(string path, string id) => throw new NotImplementedException();
    }
}
