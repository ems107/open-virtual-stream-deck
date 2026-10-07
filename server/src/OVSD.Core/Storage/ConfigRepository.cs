using Microsoft.Extensions.Logging;
using OVSD.Core.Model;

namespace OVSD.Core.Storage;

/// <summary>config.json: paired devices and app settings.</summary>
public sealed class ConfigRepository
{
    private readonly DataPaths _paths;
    private readonly Lock _lock = new();
    private ServerConfig _current;

    /// <summary>Raised after every update with the old and the new config.</summary>
    public event Action<ServerConfig, ServerConfig>? Changed;

    public ConfigRepository(DataPaths paths, ILogger<ConfigRepository> logger)
    {
        _paths = paths;
        try
        {
            _current = JsonFile.Read<ServerConfig>(paths.ConfigFile) ?? new ServerConfig();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "config.json is corrupt; keeping a copy and starting fresh");
            File.Copy(paths.ConfigFile, paths.ConfigFile + ".corrupt", overwrite: true);
            _current = new ServerConfig();
        }
        JsonFile.WriteAtomic(paths.ConfigFile, _current);
    }

    public ServerConfig Current
    {
        get { lock (_lock) return _current; }
    }

    public AppSettings Settings => Current.Settings;

    public ServerConfig Update(Func<ServerConfig, ServerConfig> change)
    {
        ServerConfig old, updated;
        lock (_lock)
        {
            old = _current;
            updated = change(old);
            if (ReferenceEquals(old, updated)) return old;
            JsonFile.WriteAtomic(_paths.ConfigFile, updated);
            _current = updated;
        }
        Changed?.Invoke(old, updated);
        return updated;
    }

    public ServerConfig UpdateDevice(string deviceId, Func<Device, Device> change) =>
        Update(c => c with { Devices = c.Devices.Select(d => d.Id == deviceId ? change(d) : d).ToList() });

    public ServerConfig UpdateSettings(Func<AppSettings, AppSettings> change) =>
        Update(c => c with { Settings = change(c.Settings) });
}
