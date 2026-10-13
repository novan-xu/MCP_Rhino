extern alias rhinocommon;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Rhino.Live.Scripting;
using rhinocommon::Rhino;
using rhinocommon::Rhino.Geometry;
using rhinocommon::Rhino.FileIO;
using rhinocommon::Rhino.Runtime.InProcess;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        try { RhinoInside.Resolver.Initialize(); Run(args); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static void Run(string[] args)
    {
        using var core = new RhinoCore(["/NOSPLASH", "/NOTEMPLATE"], WindowStyle.NoWindow);
        string root = Path.GetFullPath("Runtime_Test/local/live-custom-scripts/" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string aPath = Path.Combine(root, "a.3dm"), bPath = Path.Combine(root, "b.3dm");
        using (var file = new File3dm()) { file.Write(aPath, 8); file.Write(bPath, 8); }
        using var a = RhinoDoc.OpenHeadless(aPath);
        using var b = RhinoDoc.OpenHeadless(bPath);
        a.UndoRecordingEnabled = true; b.UndoRecordingEnabled = true;
        if (args.Length == 1)
        {
            CheckPackage(Path.GetFullPath(args[0]), a);
            return;
        }
        var accessor = new PumpAccessor(a);
        int assertions = 0;
        void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); assertions++; Console.WriteLine("PASS: " + message); }
        int Count(RhinoDoc d) => d.Objects.Count(o => !o.IsDeleted && !o.IsInstanceDefinitionGeometry);
        RhinoScriptPreviewResponse Preview(RhinoScriptService s, string code, int timeout = 60)
        {
            var response = s.Preview(new() { FilePath = aPath, Script = code, ParametersJson = "{\"x\":12.5}", TimeoutSeconds = timeout });
            if (!response.Success) throw new InvalidOperationException(response.Message);
            return response.Data!;
        }
        RhinoScriptStatusResponse Wait(RhinoScriptService service, string id)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                accessor.Pump();
                var s = service.GetStatus(aPath, id).Data!;
                if (s.IsTerminal) return s;
                Thread.Sleep(5);
            }
            throw new TimeoutException("Synthetic script execution did not complete.");
        }
        RhinoScriptStatusResponse RunCode(string code, int timeout = 60)
        {
            using var e = new LiveRhinoScriptExecutor(accessor);
            var s = new RhinoScriptService(e);
            var p = Preview(s, code, timeout);
            return Wait(s, s.Apply(aPath, p.PreviewToken).Data!.ExecutionId);
        }

        using (var e = new LiveRhinoScriptExecutor(accessor))
        {
            var s = new RhinoScriptService(e);
            uint undoBefore = a.NextUndoRecordSerialNumber;
            var p = Preview(s, "Log(\"started\"); var id = doc.Objects.AddPoint(Parameters.GetProperty(\"x\").GetDouble(), 2, 3); Result = new { id, serial = doc.RuntimeSerialNumber };");
            Check(Count(a) == 0 && a.NextUndoRecordSerialNumber == undoBefore, "Preview compiles without executing or creating Undo");
            Check(p.RuntimeSerialNumber == a.RuntimeSerialNumber && p.PreviewKind == "CompilationOnly", "Preview names exact target");
            Check(!s.Apply(bPath, p.PreviewToken).Success, "Mistargeted apply rejected");
            var start = s.Apply(aPath, p.PreviewToken).Data!;
            Check(s.Apply(aPath, p.PreviewToken).Data!.ExecutionId == start.ExecutionId, "Repeated apply before completion uses same execution");
            var done = Wait(s, start.ExecutionId);
            Check(done.State == "Succeeded", "C# execution succeeds: " + done.Error);
            Check(Count(a) == 1 && Count(b) == 0 && done.Result!.Value.GetProperty("serial").GetUInt32() == a.RuntimeSerialNumber, "Only explicitly bound document is edited");
            Check(done.Logs.SequenceEqual(["started"]), "Structured logs survive completion");
            Check(s.Apply(aPath, p.PreviewToken).Data!.ExecutionId == start.ExecutionId && Count(a) == 1, "Completed retry does not repeat mutation");
            uint inverseRecord = a.NextUndoRecordSerialNumber;
            Check(a.Undo() && Count(a) == 0, "Successful script edits form one Rhino Undo record");
            if (a.CurrentUndoRecordSerialNumber == inverseRecord) a.EndUndoRecord(inverseRecord);
            foreach (string bad in new[] { "this is invalid C#", "#r \"untrusted.dll\"\nResult = 1;", "#load \"other.csx\"", "await System.Threading.Tasks.Task.Delay(1);", "async void F() {} F();" })
                Check(!s.Preview(new() { FilePath = aPath, Script = bad }).Success, "Invalid/directive/async source rejected without execution");
        }

        uint previous = a.BeginUndoRecord("Earlier user edit");
        Guid previousId = a.Objects.AddPoint(100, 200, 300); bool previousEnded = a.EndUndoRecord(previous);
        Console.WriteLine($"Undo fixture: previous={previous}, ended={previousEnded}, enabled={a.UndoRecordingEnabled}, active={a.UndoRecordingIsActive}, serial={a.CurrentUndoRecordSerialNumber}");
        var failed = RunCode("doc.Objects.AddPoint(1, 2, 3); throw new Exception(\"expected failure\");");
        Check(failed.State == "Failed" && failed.Rollback == "Applied" && Count(a) == 1 && a.Objects.FindId(previousId) is not null,
            "Failed mutation rolls back only its own record: " + failed.Error + " / " + failed.Rollback);
        var early = RunCode("throw new Exception(\"before first edit\");");
        Check(early.State == "Failed" && early.Rollback == "Applied" && Count(a) == 1, "Failure before any edit does not undo preceding user work");
        var logLimit = RunCode("for (int i=0; i<101; i++) Log(\"line\");");
        Check(logLimit.State == "Failed" && logLimit.Logs.Count == 100 && Count(a) == 1, "Log limit rejects runaway output");
        var resultLimit = RunCode("Result = new string('x', 300000);");
        Check(resultLimit.State == "Failed" && resultLimit.Error.Contains("SCRIPT_RESULT_LIMIT") && Count(a) == 1, "Result size limit enforced");
        var timed = RunCode("doc.Objects.AddPoint(9,9,9); while (true) { Checkpoint(); System.Threading.Thread.Sleep(5); }", 1);
        Check(timed.State == "Cancelled" && timed.Rollback == "Applied" && Count(a) == 1, "Cooperative deadline cancels and rolls back");
        var callback = RunCode("CancellationToken.Register(() => throw new Exception(\"callback\")); while (true) { Checkpoint(); System.Threading.Thread.Sleep(5); }", 1);
        Check(callback.State == "Cancelled" && callback.Rollback == "Applied", "Throwing cancellation callbacks do not escape the timer thread");
        using (var e = new LiveRhinoScriptExecutor(accessor))
        {
            var s = new RhinoScriptService(e);
            var p = Preview(s, "doc.Objects.AddPoint(8,8,8); while (true) { Checkpoint(); System.Threading.Thread.Sleep(5); }");
            var id = s.Apply(aPath, p.PreviewToken).Data!.ExecutionId;
            var monitor = Task.Run(() =>
            {
                while (s.GetStatus(aPath, id).Data!.State == "Queued") Thread.Sleep(5);
                var running = s.GetStatus(aPath, id).Data!;
                if (running.State != "Running" || running.IsTerminal) throw new Exception("Running status unavailable while Rhino is busy.");
                return s.Cancel(aPath, id);
            });
            var done = Wait(s, id);
            Check(monitor.GetAwaiter().GetResult().Success && done.State == "Cancelled" && done.Rollback == "Applied" && Count(a) == 1,
                "Status and cooperative cancellation remain responsive during execution");
        }

        using (var e = new LiveRhinoScriptExecutor(accessor))
        {
            var s = new RhinoScriptService(e);
            var p = Preview(s, "doc.Objects.AddPoint(8,8,8);");
            var id = s.Apply(aPath, p.PreviewToken).Data!.ExecutionId;
            s.Cancel(aPath, id);
            Check(Wait(s, id).State == "Cancelled" && Count(a) == 1, "Queued cancellation never executes code");
        }
        using (var e = new LiveRhinoScriptExecutor(accessor))
        {
            var s = new RhinoScriptService(e);
            var p = Preview(s, "doc.Objects.AddPoint(8,8,8);");
            using var other = new LiveRhinoScriptExecutor(accessor);
            Check(!new RhinoScriptService(other).Apply(aPath, p.PreviewToken).Success, "Preview cannot cross backend connections");
            typeof(RhinoScriptPreviewResponse).GetProperty(nameof(RhinoScriptPreviewResponse.ExpiresAt))!.SetValue(p, DateTimeOffset.UtcNow.AddMinutes(-1));
            Check(!s.Apply(aPath, p.PreviewToken).Success && Count(a) == 1, "Expired preview cannot execute");
        }
        using (var e = new LiveRhinoScriptExecutor(accessor))
        {
            var s = new RhinoScriptService(e);
            var p = Preview(s, "doc.Objects.AddPoint(7,7,7);");
            accessor.Document = b;
            var id = s.Apply(aPath, p.PreviewToken).Data!.ExecutionId;
            Check(Wait(s, id).State == "Failed" && Count(b) == 0, "Changed document identity rejected immediately before execution");
            accessor.Document = a;
        }
        using (var e = new LiveRhinoScriptExecutor(accessor))
        {
            var s = new RhinoScriptService(e);
            var p = Preview(s, "doc.Objects.AddPoint(6,6,6);");
            accessor.Closed = true;
            var id = s.Apply(aPath, p.PreviewToken).Data!.ExecutionId;
            Check(Wait(s, id).Error.Contains("DOCUMENT_CLOSED") && Count(a) == 1, "Closed target is not replaced by another document or disk read");
            accessor.Closed = false;
        }
        using (var e = new LiveRhinoScriptExecutor(accessor))
        {
            var s = new RhinoScriptService(e);
            var p = Preview(s, "doc.Objects.AddPoint(6,6,6);");
            var id = s.Apply(aPath, p.PreviewToken).Data!.ExecutionId;
            e.Dispose();
            Check(Wait(s, id).State == "Cancelled" && Count(a) == 1, "Connection disposal cancels queued execution without replay");
        }
        a.UndoRecordingEnabled = false;
        Check(RunCode("doc.Objects.AddPoint(5,5,5);").State == "Failed" && Count(a) == 1, "Disabled Undo refuses execution");
        a.UndoRecordingEnabled = true;
        uint outer = a.BeginUndoRecord("Outer user command");
        Check(RunCode("doc.Objects.AddPoint(4,4,4);").State == "Failed" && Count(a) == 1, "An existing user Undo record is never borrowed");
        a.EndUndoRecord(outer);
        Check(Count(b) == 0, "Unrelated document stays empty through all tests");
        Console.WriteLine($"PASS: {assertions} native script assertions.");
    }

    private static void CheckPackage(string package, RhinoDoc doc)
    {
        string runtimePath = Path.Combine(package, "Plugin", "MCP_Rhino.Server.Runtime.dll");
        var context = new MCP_Rhino.Server.Infrastructure.Plugin.PluginLoadContext(runtimePath);
        var assembly = context.LoadFromAssemblyPath(runtimePath);
        var compiler = assembly.GetType("MCP_Rhino.Server.Infrastructure.Rhino.Live.Scripting.LiveRhinoScriptCompiler", true)!;
        var compiled = (ITuple)compiler.GetMethod("Compile", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, ["Log(\"isolated package\"); Result = new { serial = doc.RuntimeSerialNumber, count = doc.Objects.Count, x = Parameters.GetProperty(\"x\").GetInt32() };"])!;
        var globalsType = assembly.GetType("MCP_Rhino.Server.Infrastructure.Rhino.Live.Scripting.LiveRhinoScriptGlobals", true)!;
        var jsonAssembly = globalsType.GetProperty("Parameters")!.PropertyType.Assembly;
        var jsonDocType = jsonAssembly.GetType("System.Text.Json.JsonDocument", true)!;
        var jsonOptions = jsonAssembly.GetType("System.Text.Json.JsonDocumentOptions", true)!;
        using var parameters = (IDisposable)jsonDocType.GetMethod("Parse", [typeof(string), jsonOptions])!
            .Invoke(null, ["{\"x\":42}", Activator.CreateInstance(jsonOptions)])!;
        var globals = Activator.CreateInstance(globalsType, doc, jsonDocType.GetProperty("RootElement")!.GetValue(parameters)!, CancellationToken.None)!;
        ((Task)((Delegate)compiled[0]!).DynamicInvoke(globals, CancellationToken.None)!).GetAwaiter().GetResult();
        var result = globalsType.GetProperty("Result")!.GetValue(globals)!;
        if (!Equals(result.GetType().GetProperty("serial")!.GetValue(result), doc.RuntimeSerialNumber)
            || !Equals(result.GetType().GetProperty("x")!.GetValue(result), 42))
            throw new InvalidOperationException("Packaged script did not share the routed RhinoDoc/JSON types.");
        Console.WriteLine("PASS: Packaged isolated runtime compiles and executes with shared RhinoCommon and isolated System.Text.Json " + jsonAssembly.GetName().Version);
    }

    private sealed class PumpAccessor(RhinoDoc doc) : ILiveRhinoDocumentAccessor
    {
        private readonly int _owner = Environment.CurrentManagedThreadId;
        private readonly ConcurrentQueue<Action> _queue = new();
        public RhinoDoc Document { get; set; } = doc;
        public bool Closed { get; set; }
        public void Pump() { while (_queue.TryDequeue(out var action)) action(); }
        public OperationResponse<T> Execute<T>(string path, Func<RhinoDoc, OperationResponse<T>> work, TimeSpan? timeout = null)
        {
            OperationResponse<T> Invoke() => Closed ? OperationResponse<T>.Fail("DOCUMENT_CLOSED") : work(Document);
            if (Environment.CurrentManagedThreadId == _owner) return Invoke();
            using var signal = new ManualResetEventSlim();
            OperationResponse<T>? result = null;
            _queue.Enqueue(() => { try { result = Invoke(); } catch (Exception e) { result = OperationResponse<T>.Fail(e.ToString()); } finally { signal.Set(); } });
            signal.Wait(); return result!;
        }
        public OperationResponse<T> ExecuteWithUndo<T>(string path, string description, Func<RhinoDoc, OperationResponse<(bool Mutated, T Result)>> work, TimeSpan? timeout = null)
            => throw new InvalidOperationException("Custom scripts must own their Undo boundary.");
        public bool TryGetActiveDocumentState(string path, out bool modified) { modified = false; return !Closed; }
    }
}
