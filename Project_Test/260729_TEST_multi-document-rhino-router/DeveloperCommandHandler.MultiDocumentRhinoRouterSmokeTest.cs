using System.Diagnostics;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string MultiDocumentRhinoRouterSlug = "multi-document-rhino-router-smoke-test";

    partial void RegisterMultiDocumentRhinoRouterHandlers()
    {
        _extensionHandlers[MultiDocumentRhinoRouterSlug] = HandleMultiDocumentRhinoRouterSmokeTest;
    }

    private static bool HandleMultiDocumentRhinoRouterSmokeTest(string[] args)
    {
        try
        {
            string root = FindMultiDocumentRouterRepositoryRoot();
            string project = Path.Combine(
                root,
                "Project_Test",
                "260729_TEST_multi-document-rhino-router",
                "RouterProtocolSmoke",
                "RouterProtocolSmoke.csproj");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = root
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(project);
            startInfo.ArgumentList.Add("--configuration");
#if DEBUG
            startInfo.ArgumentList.Add("Debug");
#else
            startInfo.ArgumentList.Add("Release");
#endif

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start Router protocol smoke test.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Console.Write(output);
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.Error.Write(error);
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Router protocol smoke exited with code {process.ExitCode}.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Multi-document Rhino Router smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static string FindMultiDocumentRouterRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MCP_Rhino.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find MCP_Rhino.sln from " + AppContext.BaseDirectory);
    }
}
