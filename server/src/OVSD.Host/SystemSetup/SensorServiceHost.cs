using OVSD.Platform.Windows.Sensors;

namespace OVSD.Host.SystemSetup;

/// <summary>"OVSD.exe --sensor-service": the Windows service process. No web server, no tray, no actions.</summary>
public static class SensorServiceHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Services.AddWindowsService(o => o.ServiceName = SensorPipe.ServiceName);
        builder.Logging.AddProvider(new FileLoggerProvider(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OVSD Sensors", "logs")));
        builder.Services.AddHostedService(sp => new SensorPipeServer(AppInfo.Version, sp.GetRequiredService<ILogger<SensorPipeServer>>()));
        await builder.Build().RunAsync();
    }
}
