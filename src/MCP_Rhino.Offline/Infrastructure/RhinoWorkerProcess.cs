using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MCP_Rhino.Offline.Application.Interfaces;
using MCP_Rhino.Offline.Contracts;

namespace MCP_Rhino.Offline.Infrastructure;

public sealed class RhinoWorkerProcess(string? workerPath = null) : IRhinoJobExecutor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<RhinoJobOutput> ExecuteAsync(byte[] source, string extension, string script,
        string parametersJson, string? librarySource, string? libraryType, string? modelUnits,
        int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new OfflineEditException("RHINO_REQUIRES_WINDOWS", "Headless Rhino worker requires Windows and licensed Rhino 8.");
        string executable = workerPath ?? Path.Combine(AppContext.BaseDirectory, "RhinoWorker", "MCP_Rhino.RhinoWorker.exe");
        if (!File.Exists(executable)) throw new OfflineEditException("RHINO_WORKER_MISSING", "Install/stage the RhinoWorker folder beside the Router.");
        string job = Path.Combine(Path.GetTempPath(), "MCP_Rhino", "offline-rhino", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(job);
        string input = Path.Combine(job, "input" + extension), output = Path.Combine(job, "output.3dm");
        string requestPath = Path.Combine(job, "request.json"), resultPath = Path.Combine(job, "result.json");
        try
        {
            await using (var stream = new FileStream(input, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await stream.WriteAsync(source, cancellationToken);
            using var parameters = JsonDocument.Parse(parametersJson);
            var request = new RhinoWorkerRequest(input, output, script, parameters.RootElement.Clone(), librarySource, libraryType, modelUnits);
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, Json), cancellationToken);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = job };
            start.ArgumentList.Add(requestPath); start.ArgumentList.Add(resultPath);
            using var process = Process.Start(start) ?? throw new OfflineEditException("RHINO_START_FAILED", "Could not start the headless Rhino worker.");
            Task<string> stdout = Drain(process.StandardOutput), stderr = Drain(process.StandardError);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(stdout, stderr);
                cancellationToken.ThrowIfCancellationRequested();
                throw new OfflineEditException("RHINO_TIMEOUT", "Headless Rhino exceeded its deadline; no preview/output was published.");
            }
            string diagnostic = await stderr; string console = await stdout;
            if (!File.Exists(resultPath)) throw new OfflineEditException("RHINO_START_FAILED", "Worker exited without a result. " + diagnostic + console);
            if (new FileInfo(resultPath).Length > 1048576) throw new OfflineEditException("RESULT_SIZE_LIMIT", "Worker result exceeds 1 MiB.");
            var report = JsonSerializer.Deserialize<RhinoWorkerResult>(await File.ReadAllTextAsync(resultPath, cancellationToken), Json)
                ?? throw new OfflineEditException("INVALID_WORKER_RESULT", "Empty worker result.");
            if (process.ExitCode != 0 || !report.Success) throw new OfflineEditException("RHINO_SCRIPT_FAILED", report.Error ?? diagnostic);
            if (!File.Exists(output) || new FileInfo(output).Length is <= 0 or > 268435456)
                throw new OfflineEditException("INVALID_OUTPUT", "Missing or oversized worker output.");
            byte[] bytes = await File.ReadAllBytesAsync(output, cancellationToken);
            return new(bytes, report);
        }
        finally
        {
            // Remove only our known job files; never recursively follow content created by full-trust code.
            foreach (string path in new[] { input, output, requestPath, resultPath })
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { Directory.Delete(job, recursive: false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static async Task<string> Drain(StreamReader reader)
    {
        var text = new StringBuilder(); var buffer = new char[4096]; int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
            if (text.Length < 65536) text.Append(buffer, 0, Math.Min(read, 65536 - text.Length));
        return text.ToString();
    }
}
