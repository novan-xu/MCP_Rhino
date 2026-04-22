using System.Globalization;
using System.Text;

namespace MCP_Rhino.Server.Infrastructure.CLI;

// Aggregates checkpoints produced during the live-document smoke run and
// writes a Markdown diagnostic report. The report is shaped so an LLM
// receiving only the .md file (no repo access) can still answer:
//   1. Which feature failed?
//   2. Which request/response shape was exercised?
//   3. Which code paths should be inspected first?
internal sealed class LiveSmokeReport
{
    public string Mode { get; set; } = "Unknown";
    public string DocumentPath { get; set; } = string.Empty;
    public string RhinoVersion { get; set; } = "n/a";
    public DateTime StartedAt { get; } = DateTime.Now;
    public DateTime FinishedAt { get; private set; } = DateTime.MinValue;

    public int InitialObjectCount { get; set; }
    public int FinalObjectCount { get; set; }
    public int InitialLayerCount { get; set; }
    public int FinalLayerCount { get; set; }
    public uint InitialUndoSerial { get; set; }
    public uint FinalUndoSerial { get; set; }

    public List<LiveSmokeCheckpoint> Checkpoints { get; } = new();

    public int PassCount => Checkpoints.Count(c => c.Status == LiveSmokeStatus.Pass);
    public int FailCount => Checkpoints.Count(c => c.Status == LiveSmokeStatus.Fail);
    public int SkipCount => Checkpoints.Count(c => c.Status == LiveSmokeStatus.Skip);
    public bool HasFailures => FailCount > 0;

    // Runs one checkpoint. Any exception from the body is captured as a
    // FAIL entry (or SKIP when LiveSmokeSkipException) so a single broken
    // checkpoint does not abort the remaining stages.
    public void Run(
        string stage,
        string feature,
        IReadOnlyList<string> codeLocations,
        string input,
        string expected,
        IReadOnlyList<string> suspects,
        Action<LiveSmokeCheckpoint> body)
    {
        var checkpoint = new LiveSmokeCheckpoint
        {
            Stage = stage,
            Feature = feature,
            CodeLocations = codeLocations,
            InputSummary = input,
            Expectation = expected,
            Suspects = suspects
        };
        Checkpoints.Add(checkpoint);

        try
        {
            body(checkpoint);
            if (checkpoint.Status == LiveSmokeStatus.Pending)
            {
                checkpoint.Status = LiveSmokeStatus.Pass;
            }
        }
        catch (LiveSmokeSkipException skip)
        {
            checkpoint.Status = LiveSmokeStatus.Skip;
            checkpoint.FailureReason = skip.Message;
        }
        catch (Exception ex)
        {
            checkpoint.Status = LiveSmokeStatus.Fail;
            checkpoint.FailureReason = ex.Message;
        }
    }

    public string WriteMarkdown(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        FinishedAt = DateTime.Now;

        string stamp = StartedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string fileName = $"report_{stamp}_{Mode.ToLowerInvariant()}.md";
        string filePath = Path.Combine(outputDirectory, fileName);

        var sb = new StringBuilder();
        AppendHeader(sb);
        AppendSummaryTable(sb);
        AppendFailures(sb);
        AppendSkips(sb);
        AppendPasses(sb);

        File.WriteAllText(filePath, sb.ToString());
        return filePath;
    }

    private void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("# Live-Document Smoke Report");
        sb.AppendLine();
        sb.AppendLine($"- **Mode**: `{Mode}`");
        sb.AppendLine($"- **Document**: `{DocumentPath}`");
        sb.AppendLine($"- **Rhino version**: {RhinoVersion}");
        sb.AppendLine($"- **Started**: {StartedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **Finished**: {FinishedAt:yyyy-MM-dd HH:mm:ss}  (duration: {(FinishedAt - StartedAt).TotalSeconds:F1}s)");
        sb.AppendLine($"- **Objects**: {InitialObjectCount} → {FinalObjectCount}");
        sb.AppendLine($"- **Layers**: {InitialLayerCount} → {FinalLayerCount}");
        sb.AppendLine($"- **Undo serial**: {InitialUndoSerial} → {FinalUndoSerial}  (delta = {(long)FinalUndoSerial - InitialUndoSerial})");
        sb.AppendLine($"- **Totals**: {Checkpoints.Count} checkpoints — **{PassCount} PASS**, **{FailCount} FAIL**, **{SkipCount} SKIP**");
        sb.AppendLine();

        if (HasFailures)
        {
            sb.AppendLine("> **Overall**: FAIL. See the Failures section below for diagnostic details.");
        }
        else if (SkipCount > 0)
        {
            sb.AppendLine("> **Overall**: PASS (with skipped checkpoints due to missing preconditions).");
        }
        else
        {
            sb.AppendLine("> **Overall**: PASS.");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private void AppendSummaryTable(StringBuilder sb)
    {
        sb.AppendLine("## Summary Table");
        sb.AppendLine();
        sb.AppendLine("| # | Stage | Feature | Status | Objects Δ | Layers Δ | Undo Δ | Observed Success | Observed Message |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        for (int i = 0; i < Checkpoints.Count; i++)
        {
            LiveSmokeCheckpoint cp = Checkpoints[i];
            string statusCell = cp.Status switch
            {
                LiveSmokeStatus.Pass => "PASS",
                LiveSmokeStatus.Fail => "**FAIL**",
                LiveSmokeStatus.Skip => "SKIP",
                _ => "PENDING"
            };
            string message = cp.ObservedMessage is null ? string.Empty : EscapeCell(cp.ObservedMessage);
            string observedSuccess = cp.ObservedSuccess is null ? "-" : cp.ObservedSuccess.Value.ToString();
            sb.AppendLine($"| {i + 1} | {EscapeCell(cp.Stage)} | {EscapeCell(cp.Feature)} | {statusCell} | {cp.ObjectsDelta} | {cp.LayersDelta} | {cp.UndoDelta} | {observedSuccess} | {message} |");
        }
        sb.AppendLine();
    }

    private void AppendFailures(StringBuilder sb)
    {
        if (FailCount == 0)
        {
            return;
        }

        sb.AppendLine($"## Failures ({FailCount})");
        sb.AppendLine();

        int index = 0;
        foreach (LiveSmokeCheckpoint cp in Checkpoints.Where(c => c.Status == LiveSmokeStatus.Fail))
        {
            index++;
            sb.AppendLine($"### [FAIL {index}] {cp.Stage} / {cp.Feature}");
            sb.AppendLine();

            sb.AppendLine($"**Failure reason**: {cp.FailureReason}");
            sb.AppendLine();

            sb.AppendLine("**Code locations**:");
            foreach (string location in cp.CodeLocations)
            {
                sb.AppendLine($"- `{location}`");
            }
            sb.AppendLine();

            sb.AppendLine($"**Input**: `{cp.InputSummary}`");
            sb.AppendLine();
            sb.AppendLine($"**Expected**: {cp.Expectation}");
            sb.AppendLine();

            sb.AppendLine("**Observed**:");
            sb.AppendLine($"- Success: {FormatNullable(cp.ObservedSuccess)}");
            sb.AppendLine($"- Message: {FormatOrEmpty(cp.ObservedMessage)}");
            sb.AppendLine($"- Data: {FormatOrEmpty(cp.ObservedDataSummary)}");
            sb.AppendLine($"- Objects delta: {cp.ObjectsDelta}");
            sb.AppendLine($"- Layers delta: {cp.LayersDelta}");
            sb.AppendLine($"- Undo delta (informational only — may be 0 under Rhino command wrapper): {cp.UndoDelta}");
            sb.AppendLine();

            if (cp.Suspects.Count > 0)
            {
                sb.AppendLine("**Suspects to inspect**:");
                foreach (string suspect in cp.Suspects)
                {
                    sb.AppendLine($"- {suspect}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    private void AppendSkips(StringBuilder sb)
    {
        if (SkipCount == 0)
        {
            return;
        }

        sb.AppendLine($"## Skipped ({SkipCount})");
        sb.AppendLine();
        foreach (LiveSmokeCheckpoint cp in Checkpoints.Where(c => c.Status == LiveSmokeStatus.Skip))
        {
            sb.AppendLine($"- **{cp.Stage} / {cp.Feature}** — {cp.FailureReason}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private void AppendPasses(StringBuilder sb)
    {
        if (PassCount == 0)
        {
            return;
        }

        sb.AppendLine($"## Passes ({PassCount})");
        sb.AppendLine();
        foreach (LiveSmokeCheckpoint cp in Checkpoints.Where(c => c.Status == LiveSmokeStatus.Pass))
        {
            sb.AppendLine($"- **{cp.Stage} / {cp.Feature}** — {FormatOrEmpty(cp.Evidence)}  (Undo Δ = {cp.UndoDelta})");
        }
        sb.AppendLine();
    }

    private static string FormatNullable(bool? value) => value is null ? "-" : value.Value.ToString();
    private static string FormatOrEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    private static string EscapeCell(string value) => value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", string.Empty);
}
