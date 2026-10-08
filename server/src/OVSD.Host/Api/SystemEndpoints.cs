using OVSD.Host.Security;
using OVSD.Host.SystemSetup;
using OVSD.Platform.Windows.Sensors;

namespace OVSD.Host.Api;

public sealed record LanStatus(bool Listening, bool FirewallAllowed, string Mode, NetworkProfile? Network);
public sealed record SensorStatus(string Service, bool? DriverInstalled, double? CpuTemperature, string? CpuName, string? Error);
public sealed record SystemStatus(LanStatus Lan, SensorStatus Sensors, bool Elevated);
public sealed record SetupResult(bool Ok, bool Cancelled, string? Error, bool Restarting);
public sealed record SensorsRequest(bool Enabled);

/// <summary>"This PC" settings: LAN access (firewall, network profile) and the CPU sensor service.</summary>
public static class SystemEndpoints
{
    public static void MapSystemApi(this WebApplication app, int port, LanAccess lan, Action restart)
    {
        var system = app.MapGroup("/api/system").RequireLocal();

        system.MapGet("", (HttpContext ctx) =>
        {
            var network = NetworkInfo.GetPrimaryAddress() is { } address ? NetworkProfiles.ForAddress(address) : null;
            var message = ctx.RequestServices.GetService<SensorPipeClient>()?.Latest; // absent in dry-run mode
            return new SystemStatus(
                new LanStatus(lan.Listening, lan.FirewallAllowed, lan.Mode, network),
                new SensorStatus(SensorService.State(), message?.DriverInstalled, message?.Cpu?.Temperature, message?.Cpu?.Name, message?.Error),
                Environment.IsPrivilegedProcess);
        });

        // One UAC prompt: firewall rules (+ make the network private if Windows marked it public), then a
        // restart so the server listens on the LAN.
        system.MapPost("/lan", async () =>
        {
            var tasks = new List<string> { "firewall" };
            if (NetworkInfo.GetPrimaryAddress() is { } address && NetworkProfiles.ForAddress(address) is { Category: "public" } network)
                tasks.Add($"private-network={network.InterfaceIndex}");
            var result = await ElevatedSetup.RunElevatedAsync([.. tasks]);
            var restarting = result.Ok && !lan.Listening && lan.Mode == "auto";
            if (restarting) restart();
            return new SetupResult(result.Ok, result.Cancelled, result.Error, restarting);
        });

        system.MapPost("/sensors", async (SensorsRequest request) =>
        {
            var result = await ElevatedSetup.RunElevatedAsync(request.Enabled ? "sensors" : "remove-sensors");
            return new SetupResult(result.Ok, result.Cancelled, result.Error, false);
        });
    }
}
