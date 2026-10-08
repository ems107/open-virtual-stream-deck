using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using OVSD.Core;
using OVSD.Core.Platform;
using OVSD.Core.Storage;
using OVSD.Host;
using OVSD.Host.Api;
using OVSD.Host.Realtime;
using OVSD.Host.Security;
using OVSD.Host.SystemSetup;
using OVSD.Integrations;
using OVSD.Platform.Windows;

if (args.Length >= 2 && args[0] == "--export-schema")
{
    SchemaExport.Run(args[1]);
    return;
}

// Short-lived elevated helper (firewall, sensor service) started by the installer or the settings page.
if (args.Contains("--setup"))
{
    Environment.ExitCode = await ElevatedSetup.RunAsync(args);
    return;
}

// The CPU sensor Windows service (a copy of this executable, see SensorService).
if (args.Contains("--sensor-service"))
{
    await SensorServiceHost.RunAsync(args);
    return;
}

var noTray = args.Contains("--no-tray");
var restarted = args.Contains("--restart");
// Value-less flags must not reach the configuration parser, which would take the next argument as their value.
var configArgs = args.Where(a => a is not ("--no-tray" or "--restart")).ToArray();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = configArgs,
    // Autostart launches us with System32 as working directory; config files live next to the executable.
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.Configure<OvsdOptions>(builder.Configuration.GetSection("Ovsd"));
var port = builder.Configuration.GetValue("Ovsd:Port", 7341);
var dataDir = builder.Configuration["Ovsd:DataDir"] is { Length: > 0 } custom ? custom : DataPaths.DefaultRoot;

// One server per user session: a second launch just opens the editor of the running one.
using var instance = new Mutex(initiallyOwned: true, $@"Local\OVSD-{port}", out var isFirstInstance);
if (!isFirstInstance && restarted)
{
    // Restarting ourselves (e.g. after allowing LAN access): wait for the previous process to exit.
    try
    {
        isFirstInstance = instance.WaitOne(TimeSpan.FromSeconds(20));
    }
    catch (AbandonedMutexException)
    {
        isFirstInstance = true;
    }
}
if (!isFirstInstance)
{
    if (noTray) Console.Error.WriteLine($"OVSD is already running on port {port}.");
    else Process.Start(new ProcessStartInfo($"http://localhost:{port}/editor") { UseShellExecute = true });
    Environment.ExitCode = 1;
    return;
}

// The profiles folder is created (with a sample profile) on the very first start.
var firstRun = !Directory.Exists(Path.Combine(dataDir, "profiles"));
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(dataDir, "logs")));
var lan = new LanAccess(builder.Configuration["Ovsd:Network"] ?? "auto", Environment.ProcessPath!);
builder.WebHost.ConfigureKestrel(k =>
{
    if (lan.Listening) k.ListenAnyIP(port);
    else k.ListenLocalhost(port);
});

builder.Services.AddSingleton(TimeProvider.System);
if (builder.Configuration.GetValue("Ovsd:DryRun", false))
{
    // Tests and demos: record actions instead of touching the machine.
    builder.Services.AddSingleton<DryRunPlatform>();
    builder.Services.AddSingleton<IKeyboard>(sp => sp.GetRequiredService<DryRunPlatform>());
    builder.Services.AddSingleton<IMediaController>(sp => sp.GetRequiredService<DryRunPlatform>());
    builder.Services.AddSingleton<IAudioController>(sp => sp.GetRequiredService<DryRunPlatform>());
    builder.Services.AddSingleton<IForegroundWatcher>(sp => sp.GetRequiredService<DryRunPlatform>());
    builder.Services.AddSingleton<IProcessLauncher>(sp => sp.GetRequiredService<DryRunPlatform>());
}
else
{
    builder.Services.AddWindowsPlatform();
}
builder.Services.AddOvsdCore(dataDir);
builder.Services.AddIntegrations();
builder.Services.AddSingleton<DeviceAuth>();
builder.Services.AddSingleton<PairingService>();
builder.Services.AddSingleton<DeckSocketHandler>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    var protocol = OVSD.Core.Protocol.ProtocolJson.Options;
    o.SerializerOptions.DefaultIgnoreCondition = protocol.DefaultIgnoreCondition;
    o.SerializerOptions.AllowOutOfOrderMetadataProperties = true;
    o.SerializerOptions.NumberHandling = protocol.NumberHandling;
    foreach (var converter in protocol.Converters) o.SerializerOptions.Converters.Add(converter);
});

var app = builder.Build();

// The web client is embedded in the executable; a physical wwwroot next to it (dev builds) wins.
var physicalWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
app.Environment.WebRootFileProvider = Directory.Exists(physicalWebRoot)
    ? new PhysicalFileProvider(physicalWebRoot)
    : new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl =
        ctx.Context.Request.Path.StartsWithSegments("/assets") ? "public, max-age=31536000, immutable" : "no-cache",
});

app.Map("/ws", (HttpContext ctx, DeckSocketHandler handler) => handler.HandleAsync(ctx));
app.MapOvsdApi(port, lan.Listening);
app.MapSystemApi(port, lan, restart: () =>
{
    // Start a new instance (it waits for this one to exit) and shut down.
    var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    foreach (var arg in args.Where(a => a != "--restart").Append("--restart")) info.ArgumentList.Add(arg);
    _ = Task.Delay(500).ContinueWith(_ =>
    {
        Process.Start(info);
        app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
    });
});
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});

if (noTray)
{
    await app.RunAsync();
    return;
}

try
{
    await app.StartAsync();
}
catch (Exception e)
{
    app.Logger.LogCritical(e, "OVSD could not start");
    TrayApp.ShowStartupError(port, e);
    Environment.ExitCode = 1;
    return;
}
app.Logger.LogInformation("OVSD {Version} listening on {Url} (data in {DataDir})", AppInfo.Version, NetworkInfo.GetPrimaryUrl(port), dataDir);

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var trayThread = new Thread(() => new TrayApp(port, dataDir, firstRun, lan.Listening, lifetime.ApplicationStopping, lifetime.StopApplication).Run()) { IsBackground = true };
trayThread.SetApartmentState(ApartmentState.STA);
trayThread.Start();

await app.WaitForShutdownAsync();
