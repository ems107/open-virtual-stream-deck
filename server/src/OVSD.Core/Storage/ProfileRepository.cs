using System.Globalization;
using Microsoft.Extensions.Logging;
using OVSD.Core.Model;

namespace OVSD.Core.Storage;

public sealed record BackupInfo(string Name, DateTimeOffset Created);
public sealed record DeletedProfile(string Id, string Name, string Backup, DateTimeOffset Deleted);

/// <summary>
/// Profiles live in profiles/&lt;id&gt;.json and are cached in memory.
/// Every save keeps the previous version in backups/&lt;id&gt;/ so it can be restored.
/// </summary>
public sealed class ProfileRepository
{
    private const int MaxBackups = 50;
    private const string BackupTimeFormat = "yyyyMMdd-HHmmss-fff";

    private readonly DataPaths _paths;
    private readonly ILogger<ProfileRepository> _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Profile> _profiles = new();

    /// <summary>Raised with the profile id after a save, delete or restore.</summary>
    public event Action<string>? Changed;

    public ProfileRepository(DataPaths paths, ILogger<ProfileRepository> logger)
    {
        _paths = paths;
        _logger = logger;
        Load();
    }

    public IReadOnlyList<Profile> All
    {
        get { lock (_lock) return _profiles.Values.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList(); }
    }

    public Profile? Get(string id)
    {
        lock (_lock) return _profiles.GetValueOrDefault(id);
    }

    /// <summary>Normalizes and stores a profile, bumping its revision. Returns the stored version.</summary>
    public Profile Save(Profile profile)
    {
        Profile saved;
        lock (_lock)
        {
            var previous = _profiles.GetValueOrDefault(profile.Id);
            saved = ProfileNormalizer.Normalize(profile) with { Revision = (previous?.Revision ?? 0) + 1 };
            var file = FileFor(saved.Id);
            if (previous is not null && File.Exists(file)) Backup(saved.Id, file, "");
            JsonFile.WriteAtomic(file, saved);
            _profiles[saved.Id] = saved;
        }
        Changed?.Invoke(saved.Id);
        return saved;
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            if (!_profiles.Remove(id)) return false;
            var file = FileFor(id);
            if (File.Exists(file))
            {
                Backup(id, file, "-deleted");
                File.Delete(file);
            }
        }
        Changed?.Invoke(id);
        return true;
    }

    public IReadOnlyList<BackupInfo> GetBackups(string id)
    {
        var dir = BackupDir(id);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(name => new BackupInfo(name, ParseBackupTime(name)))
            .OrderByDescending(b => b.Created)
            .ToList();
    }

    /// <summary>Deleted profiles that can still be restored (their last backup is the "-deleted" one).</summary>
    public IReadOnlyList<DeletedProfile> GetDeleted()
    {
        var root = _paths.Backups;
        if (!Directory.Exists(root)) return [];
        var result = new List<DeletedProfile>();
        foreach (var dir in Directory.GetDirectories(root))
        {
            var id = Path.GetFileName(dir);
            if (Get(id) is not null) continue;
            var last = GetBackups(id).FirstOrDefault();
            if (last is null || !last.Name.EndsWith("-deleted", StringComparison.Ordinal)) continue;
            var name = JsonFile.Read<Profile>(Path.Combine(dir, last.Name + ".json"))?.Name ?? id;
            result.Add(new DeletedProfile(id, name, last.Name, last.Created));
        }
        return result.OrderByDescending(d => d.Deleted).ToList();
    }

    public Profile? Restore(string id, string backupName)
    {
        if (backupName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        var file = Path.Combine(BackupDir(id), backupName + ".json");
        var backup = JsonFile.Read<Profile>(file);
        return backup is null ? null : Save(backup with { Id = id });
    }

    private void Load()
    {
        foreach (var file in Directory.GetFiles(_paths.Profiles, "*.json"))
        {
            try
            {
                var profile = JsonFile.Read<Profile>(file);
                if (profile is not null) _profiles[profile.Id] = ProfileNormalizer.Normalize(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not load profile {File}", file);
            }
        }

        if (_profiles.Count == 0)
        {
            var sample = SampleProfile.Create();
            JsonFile.WriteAtomic(FileFor(sample.Id), sample);
            _profiles[sample.Id] = sample;
            _logger.LogInformation("Created sample profile {Name}", sample.Name);
        }
    }

    private void Backup(string id, string file, string suffix)
    {
        var dir = BackupDir(id);
        Directory.CreateDirectory(dir);
        var name = DateTimeOffset.Now.ToString(BackupTimeFormat, CultureInfo.InvariantCulture) + suffix;
        File.Copy(file, Path.Combine(dir, name + ".json"), overwrite: true);

        foreach (var old in Directory.GetFiles(dir, "*.json").OrderByDescending(f => f, StringComparer.Ordinal).Skip(MaxBackups))
            File.Delete(old);
    }

    private static DateTimeOffset ParseBackupTime(string name) =>
        DateTime.TryParseExact(name.Length >= BackupTimeFormat.Length ? name[..BackupTimeFormat.Length] : name,
            BackupTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)
            ? new DateTimeOffset(dt)
            : DateTimeOffset.MinValue;

    private string FileFor(string id) => Path.Combine(_paths.Profiles, SafeId(id) + ".json");
    private string BackupDir(string id) => Path.Combine(_paths.Backups, SafeId(id));

    private static string SafeId(string id) =>
        id.Length is > 0 and <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? id
            : throw new ArgumentException($"Invalid profile id '{id}'");
}
