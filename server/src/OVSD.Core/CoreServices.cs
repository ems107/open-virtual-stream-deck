using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OVSD.Core.Actions;
using OVSD.Core.Actions.BuiltIn;
using OVSD.Core.Engine;
using OVSD.Core.Platform;
using OVSD.Core.Runtime;
using OVSD.Core.Storage;
using OVSD.Core.Variables;

namespace OVSD.Core;

public static class CoreServices
{
    /// <summary>
    /// Registers storage, variables, the macro engine, built-in actions and the deck runtime.
    /// Platform services not registered beforehand fall back to <see cref="UnsupportedPlatform"/>.
    /// </summary>
    public static IServiceCollection AddOvsdCore(this IServiceCollection services, string dataDirectory)
    {
        services.AddSingleton(new DataPaths(dataDirectory));
        services.AddSingleton<ProfileRepository>();
        services.AddSingleton<ConfigRepository>();
        services.AddSingleton<MediaStore>();
        services.AddSingleton<VariableStore>();
        services.AddHostedService<VariablePersistence>();
        services.AddHostedService<ClockVariables>();

        services.TryAddSingleton<UnsupportedPlatform>();
        services.TryAddSingleton<IKeyboard>(sp => sp.GetRequiredService<UnsupportedPlatform>());
        services.TryAddSingleton<IMediaController>(sp => sp.GetRequiredService<UnsupportedPlatform>());
        services.TryAddSingleton<IAudioController>(sp => sp.GetRequiredService<UnsupportedPlatform>());
        services.TryAddSingleton<IForegroundWatcher>(sp => sp.GetRequiredService<UnsupportedPlatform>());
        services.TryAddSingleton<IProcessLauncher, ProcessLauncher>();

        services.AddSingleton<IActionProvider, DeckActions>();
        services.AddSingleton<IActionProvider, KeyboardActions>();
        services.AddSingleton<IActionProvider, SystemActions>();
        services.AddSingleton<IActionProvider, MediaActions>();
        services.AddSingleton<IActionProvider, AudioActions>();
        foreach (var name in new[] { "audio.apps", "audio.devices" })
            services.AddSingleton<IOptionsProvider>(sp =>
                AudioActions.OptionProviders(sp.GetRequiredService<IAudioController>()).Single(o => o.Id == name));

        services.AddSingleton<ActionRegistry>();
        services.AddSingleton<MacroExecutor>();
        services.AddSingleton<BindingRunner>();
        services.AddSingleton<DeckRuntime>();
        services.AddHostedService(sp => sp.GetRequiredService<DeckRuntime>());
        return services;
    }
}
