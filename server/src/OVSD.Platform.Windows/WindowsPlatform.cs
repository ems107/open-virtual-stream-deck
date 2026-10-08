using Microsoft.Extensions.DependencyInjection;
using OVSD.Core.Platform;
using OVSD.Platform.Windows.Sensors;

namespace OVSD.Platform.Windows;

public static class WindowsPlatform
{
    /// <summary>Registers the native Windows implementations. Call before AddOvsdCore.</summary>
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IKeyboard, WindowsKeyboard>();

        services.AddSingleton<MediaSessionService>();
        services.AddSingleton<IMediaController>(sp => sp.GetRequiredService<MediaSessionService>());
        services.AddHostedService(sp => sp.GetRequiredService<MediaSessionService>());

        services.AddSingleton<IAudioController, WindowsAudio>();
        services.AddHostedService<AudioVariables>();

        services.AddSingleton<ForegroundWatcher>();
        services.AddSingleton<IForegroundWatcher>(sp => sp.GetRequiredService<ForegroundWatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<ForegroundWatcher>());

        services.AddSingleton<SensorPipeClient>();
        services.AddHostedService(sp => sp.GetRequiredService<SensorPipeClient>());
        services.AddHostedService<SystemMetrics>();
        return services;
    }
}
