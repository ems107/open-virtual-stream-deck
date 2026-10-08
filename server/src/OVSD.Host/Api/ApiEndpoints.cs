using Microsoft.AspNetCore.Mvc;
using OVSD.Core;
using OVSD.Core.Actions;
using OVSD.Core.Expressions;
using OVSD.Core.Integrations;
using OVSD.Core.Model;
using OVSD.Core.Platform;
using OVSD.Core.Runtime;
using OVSD.Core.Storage;
using OVSD.Core.Variables;
using OVSD.Host.Security;

namespace OVSD.Host.Api;

public sealed record ProfileListItem(string Id, string Name, int Revision, int Rows, int Cols, int Pages, bool HasRules);
public sealed record NewProfileRequest(string Name, int Rows = 3, int Cols = 5);
public sealed record DeviceView(string Id, string Name, DateTimeOffset PairedAt, DateTimeOffset? LastSeen, string? ProfileId, bool AutoProfile, bool Online);
public sealed record DeviceUpdate(string? Name, string? ProfileId, bool? AutoProfile);
public sealed record ClaimRequest(string Code, string? Name);
public sealed record PairInfo(string Code, DateTimeOffset ExpiresAt, string Url, List<string> Urls);
public sealed record ServerInfo(string Name, string Version, string Url, List<string> Urls, bool IsLocal, bool Authorized);
public sealed record AutostartState(bool Enabled);
public sealed record UploadResult(string Url);

public static class ApiEndpoints
{
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    public static void MapOvsdApi(this WebApplication app, int port)
    {
        var api = app.MapGroup("/api");

        // ---------------------------------------------------------------- public
        api.MapGet("/server", (HttpContext ctx, DeviceAuth auth, Microsoft.Extensions.Options.IOptions<OvsdOptions> o) =>
            new ServerInfo(o.Value.ServerName, AppInfo.Version,
                NetworkInfo.GetPrimaryUrl(port), NetworkInfo.GetAllUrls(port), DeviceAuth.IsLocal(ctx), auth.IsAuthorized(ctx)));

        api.MapPost("/pair/claim", (ClaimRequest request, PairingService pairing) =>
            pairing.Claim(request.Code, request.Name) is { } result
                ? Results.Ok(result)
                : Results.Problem("Invalid or expired code", statusCode: StatusCodes.Status400BadRequest));

        api.MapMethods("/hook/{name}", ["GET", "POST"], async (string name, HttpContext ctx, ConfigRepository config, VariableStore variables) =>
        {
            if (ctx.Request.Query["key"] != config.Settings.WebhookKey) return Results.Unauthorized();
            string? value = ctx.Request.Query["value"];
            if (HttpMethods.IsPost(ctx.Request.Method))
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                if (body.Length > 0) value = body;
            }
            variables.Set($"webhook.{name}", value ?? "");
            variables.Set($"webhook.{name}.time", DateTimeOffset.Now.ToString("HH:mm:ss"));
            return Results.Ok();
        });

        app.MapGet("/media/{file}", (string file, MediaStore media, HttpContext ctx) =>
        {
            if (media.Find(file) is not { } found) return Results.NotFound();
            ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(found.Path, found.ContentType);
        });

        // ---------------------------------------------------------------- this PC only
        var local = api.MapGroup("").RequireLocal();

        local.MapPost("/pair/new", (PairingService pairing) =>
        {
            var code = pairing.CreateCode();
            var urls = NetworkInfo.GetAllUrls(port).Select(u => $"{u}?pair={code.Code}").ToList();
            return new PairInfo(code.Code, code.ExpiresAt, urls.FirstOrDefault() ?? $"http://localhost:{port}/?pair={code.Code}", urls);
        });

        local.MapGet("/qr.png", (string text) =>
        {
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.M);
            return Results.File(new QRCoder.PngByteQRCode(data).GetGraphic(10), "image/png");
        });

        local.MapGet("/autostart", () => new AutostartState(Autostart.IsEnabled));
        local.MapPut("/autostart", (AutostartState state) =>
        {
            Autostart.Set(state.Enabled);
            return new AutostartState(Autostart.IsEnabled);
        });

        // ---------------------------------------------------------------- paired devices and this PC
        var secured = api.MapGroup("").RequireDevice();

        secured.MapGet("/me", (HttpContext ctx, DeviceAuth auth) => auth.DeviceOf(ctx) is { } d
            ? Results.Ok(new { d.Id, d.Name, isLocal = DeviceAuth.IsLocal(ctx) })
            : Results.Ok(new { Id = (string?)null, Name = "localhost", isLocal = true }));

        MapDevices(secured);
        MapProfiles(secured);

        secured.MapPost("/media", async (IFormFile file, MediaStore media) =>
        {
            if (file.Length > MaxUploadBytes) return Results.Problem("File too large (max 20 MB)", statusCode: 413);
            var extension = Path.GetExtension(file.FileName);
            if (!MediaStore.IsAllowedExtension(extension)) extension = MediaStore.ExtensionFor(file.ContentType);
            if (!MediaStore.IsAllowedExtension(extension)) return Results.Problem("Only images are allowed", statusCode: 415);
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            return Results.Ok(new UploadResult(media.Save(buffer.ToArray(), extension)));
        }).DisableAntiforgery();

        secured.MapGet("/actions", (ActionRegistry registry) => registry.Descriptors);
        secured.MapGet("/options/{source}", async (string source, ActionRegistry registry, CancellationToken ct) =>
            registry.GetOptions(source) is { } provider
                ? Results.Ok(await SafeOptions(provider, ct))
                : Results.NotFound());
        secured.MapGet("/keys", () => OVSD.Core.Platform.Keys.All);
        secured.MapGet("/functions", () => Functions.Help);
        secured.MapGet("/variables", (VariableStore variables) => variables.Snapshot());

        // Live preview for the editor: renders controls with the current variables, exactly like a deck would.
        secured.MapPost("/preview", (List<Control> controls, VariableStore variables) =>
            controls.Select(c => TileRenderer.Render(c, variables.Get, null, null)).ToList());

        secured.MapGet("/settings", (ConfigRepository config) => config.Settings);
        secured.MapPut("/settings", (AppSettings settings, ConfigRepository config) =>
            config.UpdateSettings(_ => settings).Settings);

        secured.MapGet("/integrations", (IEnumerable<IIntegration> integrations) => integrations.Select(i => i.Status));
    }

    private static async Task<IReadOnlyList<OptionItem>> SafeOptions(IOptionsProvider provider, CancellationToken ct)
    {
        try
        {
            return await provider.GetOptionsAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static void MapDevices(RouteGroupBuilder api)
    {
        api.MapGet("/devices", (ConfigRepository config, DeckRuntime runtime) =>
        {
            var online = runtime.Sessions.Select(s => s.DeviceId).ToHashSet();
            return config.Current.Devices.Select(d =>
                new DeviceView(d.Id, d.Name, d.PairedAt, d.LastSeen, d.ProfileId, d.AutoProfile, online.Contains(d.Id)));
        });

        api.MapPut("/devices/{id}", (string id, DeviceUpdate update, ConfigRepository config) =>
        {
            if (config.Current.Devices.All(d => d.Id != id)) return Results.NotFound();
            config.UpdateDevice(id, d => d with
            {
                Name = string.IsNullOrWhiteSpace(update.Name) ? d.Name : update.Name.Trim(),
                ProfileId = update.ProfileId == "" ? null : update.ProfileId ?? d.ProfileId,
                AutoProfile = update.AutoProfile ?? d.AutoProfile,
            });
            return Results.NoContent();
        });

        api.MapDelete("/devices/{id}", (string id, ConfigRepository config) =>
        {
            config.Update(c => c with { Devices = c.Devices.Where(d => d.Id != id).ToList() });
            return Results.NoContent();
        });
    }

    private static void MapProfiles(RouteGroupBuilder api)
    {
        api.MapGet("/profiles", (ProfileRepository repo) => repo.All.Select(p =>
            new ProfileListItem(p.Id, p.Name, p.Revision, p.Grid.Rows, p.Grid.Cols, p.Pages.Count, p.MatchRules.Count > 0)));

        api.MapGet("/profiles/{id}", (string id, ProfileRepository repo) =>
            repo.Get(id) is { } p ? Results.Ok(p) : Results.NotFound());

        api.MapPost("/profiles", (NewProfileRequest request, ProfileRepository repo) =>
        {
            var home = new Page { Id = Ids.New(), Name = "Home" };
            var profile = repo.Save(new Profile
            {
                Id = Ids.New(),
                Name = request.Name,
                Grid = new GridSize { Rows = request.Rows, Cols = request.Cols },
                HomePageId = home.Id,
                Pages = [home],
            });
            return Results.Created($"/api/profiles/{profile.Id}", profile);
        });

        // Optimistic concurrency: the client sends the revision it edited; a mismatch means someone else saved.
        api.MapPut("/profiles/{id}", (string id, Profile profile, ProfileRepository repo, [FromQuery] bool force = false) =>
        {
            var current = repo.Get(id);
            if (current is not null && !force && current.Revision != profile.Revision)
                return Results.Conflict(current);
            return Results.Ok(repo.Save(profile with { Id = id }));
        });

        api.MapDelete("/profiles/{id}", (string id, ProfileRepository repo, ConfigRepository config) =>
        {
            if (!repo.Delete(id)) return Results.NotFound();
            config.Update(c => c with
            {
                Devices = c.Devices.Select(d => d.ProfileId == id ? d with { ProfileId = null } : d).ToList(),
                Settings = c.Settings.DefaultProfileId == id ? c.Settings with { DefaultProfileId = null } : c.Settings,
            });
            return Results.NoContent();
        });

        api.MapPost("/profiles/{id}/duplicate", (string id, ProfileRepository repo) =>
            repo.Get(id) is { } p
                ? Results.Ok(repo.Save(ProfileNormalizer.WithNewIds(p) with { Name = p.Name + " (copy)" }))
                : Results.NotFound());

        api.MapGet("/profiles/{id}/export", (string id, ProfileRepository repo, MediaStore media) =>
            repo.Get(id) is { } p
                ? Results.File(ProfileArchive.Export(p, media), "application/zip", SafeFileName(p.Name) + ".ovsd.zip")
                : Results.NotFound());

        api.MapPost("/profiles/import", (IFormFile file, ProfileRepository repo, MediaStore media) =>
        {
            if (file.Length > 200 * 1024 * 1024) return Results.Problem("File too large", statusCode: 413);
            try
            {
                using var stream = file.OpenReadStream();
                return Results.Ok(repo.Save(ProfileArchive.Import(stream, media)));
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
            {
                return Results.Problem("Not a valid profile archive: " + ex.Message, statusCode: 400);
            }
        }).DisableAntiforgery();

        api.MapGet("/profiles/{id}/backups", (string id, ProfileRepository repo) => repo.GetBackups(id));

        api.MapPost("/profiles/{id}/backups/{name}/restore", (string id, string name, ProfileRepository repo) =>
            repo.Restore(id, name) is { } p ? Results.Ok(p) : Results.NotFound());
    }

    private static string SafeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
