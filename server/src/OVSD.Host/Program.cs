using Microsoft.Extensions.Options;
using OVSD.Host;
using OVSD.Host.Realtime;
using QRCoder;

if (args.Length >= 2 && args[0] == "--export-schema")
{
    SchemaExport.Run(args[1]);
    return;
}

var noTray = args.Contains("--no-tray");

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<OvsdOptions>(builder.Configuration.GetSection("Ovsd"));
var port = builder.Configuration.GetValue("Ovsd:Port", 7341);
builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(port));
builder.Services.AddSingleton<DeckSocketHandler>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
app.UseDefaultFiles();
app.UseStaticFiles();

app.Map("/ws", (HttpContext ctx, DeckSocketHandler handler) => handler.HandleAsync(ctx));

app.MapGet("/api/server", (IOptions<OvsdOptions> o) => new
{
    name = o.Value.ServerName,
    url = NetworkInfo.GetPrimaryUrl(port),
    addresses = NetworkInfo.GetLanAddresses().Select(a => a.ToString()),
});

// Temporary until F1 pairing: QR with the LAN URL of the deck.
app.MapGet("/api/pair/qr.png", () =>
{
    using var generator = new QRCodeGenerator();
    using var data = generator.CreateQrCode(NetworkInfo.GetPrimaryUrl(port), QRCodeGenerator.ECCLevel.M);
    return Results.File(new PngByteQRCode(data).GetGraphic(10), "image/png");
});

app.MapFallbackToFile("index.html");

if (noTray)
{
    await app.RunAsync();
    return;
}

await app.StartAsync();
app.Logger.LogInformation("OVSD listening on {Url}", NetworkInfo.GetPrimaryUrl(port));

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var trayThread = new Thread(() => new TrayApp(port, lifetime.StopApplication).Run()) { IsBackground = true };
trayThread.SetApartmentState(ApartmentState.STA);
trayThread.Start();

await app.WaitForShutdownAsync();
