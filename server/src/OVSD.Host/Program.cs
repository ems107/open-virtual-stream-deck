using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using OVSD.Core;
using OVSD.Core.Storage;
using OVSD.Host;
using OVSD.Host.Api;
using OVSD.Host.Realtime;
using OVSD.Host.Security;
using OVSD.Platform.Windows;

if (args.Length >= 2 && args[0] == "--export-schema")
{
    SchemaExport.Run(args[1]);
    return;
}

var noTray = args.Contains("--no-tray");
// Value-less flags must not reach the configuration parser, which would take the next argument as their value.
var configArgs = args.Where(a => a != "--no-tray").ToArray();

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
if (!isFirstInstance)
{
    if (noTray) Console.Error.WriteLine($"OVSD is already running on port {port}.");
    else Process.Start(new ProcessStartInfo($"http://localhost:{port}/editor") { UseShellExecute = true });
    Environment.ExitCode = 1;
    return;
}

builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(dataDir, "logs")));
builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(port));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddWindowsPlatform();
builder.Services.AddOvsdCore(dataDir);
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
app.MapOvsdApi(port);
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});

if (noTray)
{
    await app.RunAsync();
    return;
}

await app.StartAsync();
app.Logger.LogInformation("OVSD listening on {Url} (data in {DataDir})", NetworkInfo.GetPrimaryUrl(port), dataDir);

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var trayThread = new Thread(() => new TrayApp(port, dataDir, lifetime.StopApplication).Run()) { IsBackground = true };
trayThread.SetApartmentState(ApartmentState.STA);
trayThread.Start();

await app.WaitForShutdownAsync();
