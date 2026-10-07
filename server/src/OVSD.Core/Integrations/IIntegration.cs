namespace OVSD.Core.Integrations;

public enum IntegrationState { Disabled, Connecting, Connected, Error }

public sealed record IntegrationStatus(string Id, string Name, IntegrationState State, string? Message);

/// <summary>Something the settings page can show a connection status for (OBS, MQTT, Discord...).</summary>
public interface IIntegration
{
    IntegrationStatus Status { get; }
}
