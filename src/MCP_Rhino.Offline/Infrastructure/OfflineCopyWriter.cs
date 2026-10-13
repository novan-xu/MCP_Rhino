namespace MCP_Rhino.Offline.Infrastructure;

internal static class OfflineCopyWriter
{
    public static string Publish(string sourcePath, byte[] bytes, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(sourcePath)!;
        string stem = System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(sourcePath),
            @"(?:(?:_offline-edit_\d{8}_\d{6}_[0-9a-fA-F]{32})|(?:_\d{8}_\d{6}))+$", "");
        // Repeated offline edits should not accumulate suffixes until the file name exceeds OS limits.
        if (stem.Length > 160) stem = stem[..160];
        // No caller/script supplied output path. File.Move never replaces an existing path,
        // including a symlink or hardlink. Each apply, even to a previous copy, creates a sibling.
        string staging = Path.Combine(directory, $".mcp-offline-{Guid.NewGuid():N}.tmp");
        bool ownsStaging = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsStaging = true;
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            var retryTime = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateTime now = DateTime.Now;
                string stamp = now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                string output = Path.Combine(directory, $"{stem}_{stamp}.3dm");
                try
                {
                    File.Move(staging, output, overwrite: false);
                    ownsStaging = false;
                    return output;
                }
                catch (IOException) when (File.Exists(output) || Directory.Exists(output))
                {
                    // Real local seconds only: concurrent writers retry next second without
                    // appending random IDs or replacing the source/another writer's result.
                    if (retryTime.Elapsed >= TimeSpan.FromSeconds(10))
                        throw new IOException("Timestamp names are occupied. Retry apply; no existing file was overwritten.");
                    cancellationToken.WaitHandle.WaitOne(1010 - now.Millisecond);
                }
            }
        }
        finally
        {
            if (ownsStaging) File.Delete(staging);
        }
    }
}
