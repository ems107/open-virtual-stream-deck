using Microsoft.Extensions.Logging;
using MQTTnet;
using OVSD.Core.Actions;
using OVSD.Core.Integrations;
using OVSD.Core.Model;
using OVSD.Core.Storage;
using OVSD.Core.Variables;

namespace OVSD.Integrations.Mqtt;

/// <summary>
/// MQTT client: every message on a subscribed topic sets the variable "mqtt.&lt;topic&gt;"
/// (use [mqtt.home/temp] in expressions because topics contain slashes).
/// </summary>
public sealed class MqttService(ConfigRepository config, VariableStore variables, ILogger<MqttService> logger)
    : ConnectionLoop<MqttSettings>(config, logger), IActionProvider
{
    private IMqttClient? _client;

    public override string Id => "mqtt";
    public override string Name => "MQTT";

    protected override MqttSettings Select(AppSettings settings) => settings.Mqtt;
    protected override bool IsEnabled(MqttSettings settings) => settings.Enabled && !string.IsNullOrWhiteSpace(settings.Host);
    protected override object ConnectionKey(MqttSettings s) =>
        $"{s.Enabled}|{s.Host}|{s.Port}|{s.Username}|{s.Password}|{string.Join(",", s.Subscriptions)}";

    protected override async Task RunConnectedAsync(MqttSettings settings, CancellationToken ct)
    {
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.DisconnectedAsync += e =>
        {
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };
        client.ApplicationMessageReceivedAsync += e =>
        {
            variables.Set("mqtt." + e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString());
            return Task.CompletedTask;
        };

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(settings.Host, settings.Port)
            .WithClientId("ovsd-" + Environment.MachineName.ToLowerInvariant())
            .WithCleanSession();
        if (!string.IsNullOrEmpty(settings.Username)) options = options.WithCredentials(settings.Username, settings.Password);
        await client.ConnectAsync(options.Build(), ct);

        var topics = settings.Subscriptions.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (topics.Count > 0)
        {
            var subscribe = factory.CreateSubscribeOptionsBuilder();
            foreach (var topic in topics) subscribe = subscribe.WithTopicFilter(topic.Trim());
            await client.SubscribeAsync(subscribe.Build(), ct);
        }

        _client = client;
        SetStatus(IntegrationState.Connected, $"{topics.Count} subscription(s)");
        variables.Set("mqtt.connected", true);
        try
        {
            await disconnected.Task.WaitAsync(ct);
        }
        finally
        {
            _client = null;
            if (client.IsConnected) await client.DisconnectAsync(cancellationToken: CancellationToken.None);
        }
    }

    protected override void OnDisconnected() => variables.Set("mqtt.connected", false);

    public IEnumerable<IActionHandler> GetActions() =>
    [
        new DelegateAction(new ActionDescriptor
        {
            Id = "mqtt.publish", Category = "mqtt", Name = "MQTT publish", Icon = "mdi:access-point-network",
            Params =
            [
                new ParamDescriptor { Name = "topic", Label = "Topic", Required = true, Placeholder = "home/livingroom/light/set" },
                new ParamDescriptor { Name = "payload", Label = "Payload", Type = ParamType.MultilineText, Placeholder = "ON" },
                new ParamDescriptor { Name = "retain", Label = "Retain", Type = ParamType.Bool, Default = "false" },
            ],
        }, async (_, args, ct) =>
        {
            var client = _client ?? throw new ActionException("MQTT is not connected (enable it in Settings → MQTT)");
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(args.Required("topic"))
                .WithPayload(args.String("payload") ?? "")
                .WithRetainFlag(args.Bool("retain"))
                .Build();
            await client.PublishAsync(message, ct);
        }),
    ];
}
