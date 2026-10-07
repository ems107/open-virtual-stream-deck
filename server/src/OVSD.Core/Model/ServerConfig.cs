namespace OVSD.Core.Model;

/// <summary>Everything persisted in config.json: paired devices and settings.</summary>
public sealed record ServerConfig
{
    public List<Device> Devices { get; init; } = [];
    public AppSettings Settings { get; init; } = new();
}

public sealed record Device
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset PairedAt { get; init; }
    public DateTimeOffset? LastSeen { get; init; }
    /// <summary>Profile shown by default (and when no automatic rule matches).</summary>
    public string? ProfileId { get; init; }
    /// <summary>Switch profile automatically following the foreground app on the PC.</summary>
    public bool AutoProfile { get; init; }
}

public sealed record AppSettings
{
    public string? DefaultProfileId { get; init; }
    public DeckSettings Deck { get; init; } = new();
    public MetricsSettings Metrics { get; init; } = new();
    public ObsSettings Obs { get; init; } = new();
    public MqttSettings Mqtt { get; init; } = new();
    public DiscordSettings Discord { get; init; } = new();
    /// <summary>Secret for inbound webhooks (/api/hook/{name}?key=...).</summary>
    public string WebhookKey { get; init; } = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));
}

public sealed record DeckSettings
{
    public int LongPressMs { get; init; } = 500;
    public int DoubleTapMs { get; init; } = 250;
    public bool Haptics { get; init; } = true;
}

public sealed record MetricsSettings
{
    /// <summary>GPU load/temperature through LibreHardwareMonitor (no admin needed for most GPUs).</summary>
    public bool GpuSensors { get; init; } = true;
    /// <summary>CPU temperature needs a kernel driver and running as administrator.</summary>
    public bool CpuSensors { get; init; }
}

public sealed record ObsSettings
{
    public bool Enabled { get; init; }
    public string Url { get; init; } = "ws://127.0.0.1:4455";
    public string? Password { get; init; }
}

public sealed record MqttSettings
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
    public string? Username { get; init; }
    public string? Password { get; init; }
    /// <summary>Topic filters; each message sets the variable "mqtt.&lt;topic&gt;".</summary>
    public List<string> Subscriptions { get; init; } = [];
}

public sealed record DiscordSettings
{
    public bool Enabled { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? RedirectUri { get; init; } = "http://localhost";
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTimeOffset? TokenExpires { get; init; }
}
