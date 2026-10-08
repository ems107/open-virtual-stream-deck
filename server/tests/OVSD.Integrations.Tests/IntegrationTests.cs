using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Server;
using OVSD.Core.Actions;
using OVSD.Core.Engine;
using OVSD.Core.Integrations;
using OVSD.Core.Model;
using OVSD.Core.Storage;
using OVSD.Core.Tests;
using OVSD.Core.Variables;
using OVSD.Integrations.Mqtt;
using OVSD.Integrations.Obs;

namespace OVSD.Integrations.Tests;

/// <summary>Spins up the core services plus integrations, with background services started.</summary>
public sealed class IntegrationHost : IAsyncDisposable
{
    private readonly TestHost _core;
    private readonly List<IHostedService> _started = [];

    private IntegrationHost(TestHost core) => _core = core;

    public static async Task<IntegrationHost> StartAsync(Func<AppSettings, AppSettings> settings)
    {
        var core = new TestHost(services => services.AddIntegrations());
        core.Get<ConfigRepository>().UpdateSettings(settings);
        var host = new IntegrationHost(core);
        foreach (var service in core.Services.GetServices<IHostedService>().Where(s => s is IIntegration))
        {
            await service.StartAsync(CancellationToken.None);
            host._started.Add(service);
        }
        return host;
    }

    public T Get<T>() where T : notnull => _core.Get<T>();

    public Task RunAsync(params Step[] steps) =>
        Get<MacroExecutor>().RunAsync(steps, new ActionContext(Get<VariableStore>(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        foreach (var service in _started) await service.StopAsync(CancellationToken.None);
        _core.Dispose();
    }
}

public class ObsTests
{
    [Fact]
    public async Task ConnectsWithPasswordPublishesStateAndRunsActions()
    {
        await using var obs = new FakeObsServer("secret");
        await using var host = await IntegrationHost.StartAsync(s => s with { Obs = new ObsSettings { Enabled = true, Url = obs.Url, Password = "secret" } });
        var vars = host.Get<VariableStore>();

        await TestHost.Eventually(() => Equals(vars.Get("obs.scene"), "Main"), 5000);
        Assert.Equal(true, vars.Get("obs.connected"));
        Assert.Equal(true, vars.Get("obs.streaming"));
        Assert.Equal(IntegrationState.Connected, host.Get<ObsClient>().Status.State);

        await host.RunAsync(new ActionStep { Action = "obs.setScene", Params = new() { ["scene"] = "BRB" } });
        await TestHost.Eventually(() => Equals(vars.Get("obs.scene"), "BRB"));
        Assert.Contains(obs.Requests, r => r["requestType"]!.GetValue<string>() == "SetCurrentProgramScene");

        var scenes = await host.Get<ActionRegistry>().GetOptions("obs.scenes")!.GetOptionsAsync(CancellationToken.None);
        Assert.Equal(["BRB", "Main"], scenes.Select(s => s.Value));
    }

    [Fact]
    public async Task UnsupportedRequestSurfacesObsError()
    {
        await using var obs = new FakeObsServer("pw");
        await using var host = await IntegrationHost.StartAsync(s => s with { Obs = new ObsSettings { Enabled = true, Url = obs.Url, Password = "pw" } });
        await TestHost.Eventually(() => host.Get<ObsClient>().IsConnected, 5000);
        var ex = await Assert.ThrowsAsync<ActionException>(() => host.RunAsync(new ActionStep { Action = "obs.stream", Params = new() { ["mode"] = "toggle" } }));
        Assert.Contains("unsupported in fake", ex.Message);
    }

    [Fact]
    public async Task WrongPasswordReportsError()
    {
        await using var obs = new FakeObsServer("right");
        await using var host = await IntegrationHost.StartAsync(s => s with { Obs = new ObsSettings { Enabled = true, Url = obs.Url, Password = "wrong" } });
        await TestHost.Eventually(() => host.Get<ObsClient>().Status.State == IntegrationState.Error, 5000);
        Assert.Contains("password", host.Get<ObsClient>().Status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActionsFailClearlyWhenDisabled()
    {
        await using var host = await IntegrationHost.StartAsync(s => s);
        var ex = await Assert.ThrowsAsync<ActionException>(() => host.RunAsync(new ActionStep { Action = "obs.setScene", Params = new() { ["scene"] = "x" } }));
        Assert.Contains("not connected", ex.Message);
        Assert.Equal(IntegrationState.Disabled, host.Get<ObsClient>().Status.State);
    }

    [Fact]
    public void AuthMatchesProtocolDocumentation()
    {
        // Same algorithm as obs-websocket docs: base64(sha256(base64(sha256(password + salt)) + challenge)).
        var secret = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("pwsalt")));
        var expected = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(secret + "chal")));
        Assert.Equal(expected, ObsClient.AuthResponse("pw", "salt", "chal"));
    }
}

public class MqttTests
{
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    [Fact]
    public async Task SubscribesToVariablesAndPublishes()
    {
        var port = FreePort();
        using var broker = new MqttServerFactory().CreateMqttServer(
            // Loopback only: listening on every interface makes Windows ask for a firewall exception.
            new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Loopback)
                .WithDefaultEndpointBoundIPV6Address(System.Net.IPAddress.None).WithDefaultEndpointPort(port).Build());
        var published = new List<(string Topic, string Payload)>();
        broker.InterceptingPublishAsync += e =>
        {
            lock (published) published.Add((e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString()));
            return Task.CompletedTask;
        };
        await broker.StartAsync();

        await using var host = await IntegrationHost.StartAsync(s => s with
        {
            Mqtt = new MqttSettings { Enabled = true, Host = "127.0.0.1", Port = port, Subscriptions = ["home/#"] },
        });
        await TestHost.Eventually(() => host.Get<MqttService>().Status.State == IntegrationState.Connected, 5000);

        await broker.InjectApplicationMessage(new InjectedMqttApplicationMessage(
            new MqttApplicationMessageBuilder().WithTopic("home/temp").WithPayload("21.5").Build()) { SenderClientId = "sensor" });
        var vars = host.Get<VariableStore>();
        await TestHost.Eventually(() => Equals(vars.Get("mqtt.home/temp"), "21.5"));
        Assert.Equal(22.5, OVSD.Core.Expressions.Expression.Evaluate("[mqtt.home/temp] + 1", vars.Get));

        await host.RunAsync(new ActionStep { Action = "mqtt.publish", Params = new() { ["topic"] = "home/light/set", ["payload"] = "ON {{1+1}}" } });
        await TestHost.Eventually(() => { lock (published) return published.Contains(("home/light/set", "ON 2")); });
        await broker.StopAsync();
    }
}

public class HttpTests
{
    [Fact]
    public async Task StoresResponseAndStatus()
    {
        var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        var number = ((IPEndPoint)port.LocalEndpoint).Port;
        port.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{number}/");
        listener.Start();
        string? receivedBody = null, receivedHeader = null;
        _ = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            receivedBody = await new StreamReader(ctx.Request.InputStream).ReadToEndAsync();
            receivedHeader = ctx.Request.Headers["X-Token"];
            var bytes = Encoding.UTF8.GetBytes("""{"temp":19}""");
            ctx.Response.StatusCode = 201;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        });

        await using var host = await IntegrationHost.StartAsync(s => s);
        await host.RunAsync(new ActionStep
        {
            Action = "http.request",
            Params = new()
            {
                ["method"] = "POST",
                ["url"] = $"http://127.0.0.1:{number}/hook",
                ["headers"] = "X-Token: abc",
                ["body"] = """{"n": {{2*3}}}""",
                ["resultVariable"] = "user.weather",
            },
        });

        var vars = host.Get<VariableStore>();
        Assert.Equal("""{"n": 6}""", receivedBody);
        Assert.Equal("abc", receivedHeader);
        Assert.Equal(201.0, vars.Get("user.weather.status"));
        Assert.Equal(19.0, OVSD.Core.Expressions.Expression.Evaluate("json(user.weather, 'temp')", vars.Get));
    }
}
