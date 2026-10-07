using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OVSD.Core.Protocol;
using OVSD.Core.Variables;

namespace OVSD.Core.Storage;

/// <summary>
/// Album art and replaced uploads accumulate in the media folder. Once a day, deletes media older than
/// a day that no profile, backup or current variable references.
/// </summary>
public sealed class MediaJanitor(DataPaths paths, ProfileRepository profiles, VariableStore variables, ILogger<MediaJanitor> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan MinAge = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var removed = Clean();
                    if (removed > 0) logger.LogInformation("Removed {Count} unused media files", removed);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Media cleanup failed");
                }
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    public int Clean()
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Collect(string text)
        {
            var index = 0;
            while ((index = text.IndexOf(MediaStore.UrlPrefix, index, StringComparison.Ordinal)) >= 0)
            {
                index += MediaStore.UrlPrefix.Length;
                var end = index;
                while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '.')) end++;
                referenced.Add(text[index..end].TrimEnd('.'));
            }
        }

        foreach (var profile in profiles.All) Collect(JsonSerializer.Serialize(profile, ProtocolJson.Options));
        foreach (var backup in Directory.EnumerateFiles(paths.Backups, "*.json", SearchOption.AllDirectories)) Collect(File.ReadAllText(backup));
        foreach (var value in variables.Snapshot().Values) if (value is string s) Collect(s);

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(paths.Media))
        {
            if (referenced.Contains(Path.GetFileName(file))) continue;
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < MinAge) continue;
            File.Delete(file);
            removed++;
        }
        return removed;
    }
}
