using Microsoft.Extensions.DependencyInjection;
using OVSD.Core.Actions;
using OVSD.Core.Integrations;
using OVSD.Integrations.Discord;
using OVSD.Integrations.Http;
using OVSD.Integrations.Mqtt;
using OVSD.Integrations.Obs;

namespace OVSD.Integrations;

public static class IntegrationServices
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IActionProvider, HttpActions>();

        services.AddSingleton<ObsClient>();
        services.AddHostedService(sp => sp.GetRequiredService<ObsClient>());
        services.AddSingleton<IIntegration>(sp => sp.GetRequiredService<ObsClient>());
        services.AddSingleton<IActionProvider, ObsActions>();
        foreach (var id in new[] { "obs.scenes", "obs.inputs", "obs.sources" })
            services.AddSingleton<IOptionsProvider>(sp => ObsActions.OptionProviders(sp.GetRequiredService<ObsClient>()).Single(o => o.Id == id));

        services.AddSingleton<MqttService>();
        services.AddHostedService(sp => sp.GetRequiredService<MqttService>());
        services.AddSingleton<IIntegration>(sp => sp.GetRequiredService<MqttService>());
        services.AddSingleton<IActionProvider>(sp => sp.GetRequiredService<MqttService>());

        services.AddSingleton<DiscordRpcClient>();
        services.AddHostedService(sp => sp.GetRequiredService<DiscordRpcClient>());
        services.AddSingleton<IIntegration>(sp => sp.GetRequiredService<DiscordRpcClient>());
        services.AddSingleton<IActionProvider>(sp => sp.GetRequiredService<DiscordRpcClient>());
        return services;
    }
}
