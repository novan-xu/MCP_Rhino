namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string CompanionImageAttachmentsSlug = "companion-image-attachments-smoke-test";

    partial void RegisterCompanionImageAttachmentsHandlers()
    {
        _extensionHandlers[CompanionImageAttachmentsSlug] = HandleCompanionImageAttachmentsSmokeTest;
    }

    private bool HandleCompanionImageAttachmentsSmokeTest(string[] args)
    {
        try
        {
            string root = FindCompanionImageAttachmentsRepoRoot();
            string appJs = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "wwwroot", "app.js");
            string index = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "wwwroot", "index.html");
            string mainWindow = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "MainWindow.xaml.cs");
            string sessionContract = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "IAgentSession.cs");
            string attachment = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "CompanionAttachment.cs");
            string userMessage = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "CompanionUserMessage.cs");
            string validator = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "CompanionAttachmentValidator.cs");
            string fileSet = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "CompanionAttachmentFileSet.cs");
            string streamWriter = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "StreamJsonWriter.cs");
            string claudeSession = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "ClaudeCodeSession.cs");
            string codexSession = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Companion", "CodexCliSession.cs");
            string runtimePolicy = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Server", "Prompts", "Runtime", "McpRhinoRuntimePolicyBundle.md");
            string referenceAgentTool = ReadCompanionImageAttachmentsFile(root, "src", "MCP_Rhino.Server", "Tools", "Modeling", "RunReferenceImageObjectModelingAgentTool.cs");

            RequireCompanionImageAttachments(index.Contains("id=\"attachmentInput\"", StringComparison.Ordinal)
                && index.Contains("accept=\"image/png,image/jpeg,image/webp\"", StringComparison.Ordinal),
                "Companion index exposes a bounded image file picker.");
            RequireCompanionImageAttachments(appJs.Contains("FileReader", StringComparison.Ordinal)
                && appJs.Contains("base64Data", StringComparison.Ordinal)
                && appJs.Contains("post(\"send\", { text, attachments: outgoingAttachments() })", StringComparison.Ordinal),
                "Companion UI sends real image attachment payloads over WebView2 IPC.");
            RequireCompanionImageAttachments(appJs.Contains("pendingSend", StringComparison.Ordinal)
                && appJs.Contains("clearAcceptedComposer()", StringComparison.Ordinal),
                "Companion UI waits for host acceptance before clearing attachment chips.");
            RequireCompanionImageAttachments(mainWindow.Contains("CompanionUserMessage.TryReadFromJson", StringComparison.Ordinal)
                && mainWindow.Contains("CompanionUiEvent.Input(true)", StringComparison.Ordinal),
                "Companion host validates structured send payloads and resets input on rejection.");
            RequireCompanionImageAttachments(sessionContract.Contains("SendUserMessageAsync(CompanionUserMessage message", StringComparison.Ordinal),
                "IAgentSession is no longer text-only.");
            RequireCompanionImageAttachments(attachment.Contains("CompanionAttachmentSummary", StringComparison.Ordinal)
                && userMessage.Contains("AttachmentSummaries", StringComparison.Ordinal),
                "Companion attachments have a full payload type and a safe UI summary type.");
            RequireCompanionImageAttachments(validator.Contains("MaxAttachmentCount = 4", StringComparison.Ordinal)
                && validator.Contains("MaxAttachmentBytes = 10L * 1024L * 1024L", StringComparison.Ordinal)
                && validator.Contains("image/png", StringComparison.Ordinal)
                && validator.Contains("Convert.FromBase64String", StringComparison.Ordinal),
                "Companion host validates count, size, MIME type, and base64 data.");
            RequireCompanionImageAttachments(streamWriter.Contains("type = \"image\"", StringComparison.Ordinal)
                && streamWriter.Contains("media_type = attachment.MediaType", StringComparison.Ordinal),
                "Claude stream-json writer emits image content blocks.");
            RequireCompanionImageAttachments(codexSession.Contains("CompanionAttachmentFileSet.WriteImages", StringComparison.Ordinal)
                && codexSession.Contains("arguments.Add(\"-i\")", StringComparison.Ordinal),
                "Codex backend passes selected images through codex exec --image files.");
            RequireCompanionImageAttachments(fileSet.Contains("attachments", StringComparison.Ordinal)
                && fileSet.Contains("Directory.Delete", StringComparison.Ordinal),
                "Codex image temp files are scoped under the Companion workspace and cleaned up.");
            RequireCompanionImageAttachments(claudeSession.Contains("Read,Write", StringComparison.Ordinal)
                && claudeSession.Contains("WriteUserMessageAsync(_process.StandardInput, message", StringComparison.Ordinal),
                "Claude backend keeps broad local file tools disabled while accepting structured messages.");
            RequireCompanionImageAttachments(runtimePolicy.Contains("Companion Image Attachments", StringComparison.Ordinal)
                && runtimePolicy.Contains("convert visible observations from attached images into `briefRequest`", StringComparison.Ordinal),
                "Runtime policy explains image attachment inspection and reference-image brief routing.");
            RequireCompanionImageAttachments(referenceAgentTool.Contains("IMAGE_BRIEF_REQUIRED", StringComparison.Ordinal)
                && referenceAgentTool.Contains("raw image-only requests return IMAGE_BRIEF_REQUIRED", StringComparison.Ordinal),
                "Rhino reference-image modeling still requires structured brief input.");

            Console.WriteLine("[OK] companion-image-attachments smoke passed.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Companion image attachments smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static string FindCompanionImageAttachmentsRepoRoot()
    {
        string? directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "MCP_Rhino.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Unable to locate MCP_Rhino.sln from current directory.");
    }

    private static string ReadCompanionImageAttachmentsFile(string root, params string[] parts)
    {
        string path = Path.Combine(new[] { root }.Concat(parts).ToArray());
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Expected file was not found.", path);
        }

        return File.ReadAllText(path);
    }

    private static void RequireCompanionImageAttachments(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine($"[OK] {message}");
    }
}
