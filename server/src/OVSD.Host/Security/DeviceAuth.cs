using System.Net;
using System.Security.Cryptography;
using System.Text;
using OVSD.Core.Model;
using OVSD.Core.Storage;

namespace OVSD.Host.Security;

/// <summary>
/// Requests from this PC (loopback) are trusted. Everything else needs the bearer token a device got when pairing.
/// Only token hashes are stored.
/// </summary>
public sealed class DeviceAuth(ConfigRepository config)
{
    public static bool IsLocal(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public Device? FindByToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var hash = Encoding.ASCII.GetBytes(Hash(token));
        return config.Current.Devices.FirstOrDefault(d =>
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(d.TokenHash), hash));
    }

    public static string? TokenFrom(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return header["Bearer ".Length..].Trim();
        return context.Request.Query["token"].FirstOrDefault();
    }

    /// <summary>The paired device making the request, if any.</summary>
    public Device? DeviceOf(HttpContext context) => FindByToken(TokenFrom(context));

    public bool IsAuthorized(HttpContext context) => IsLocal(context) || DeviceOf(context) is not null;
}

public static class AuthFilters
{
    /// <summary>Requires a loopback request or a paired device token.</summary>
    public static TBuilder RequireDevice<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (ctx, next) =>
            ctx.HttpContext.RequestServices.GetRequiredService<DeviceAuth>().IsAuthorized(ctx.HttpContext)
                ? await next(ctx)
                : Results.Unauthorized());

    /// <summary>Only from this PC (pairing new devices, autostart...).</summary>
    public static TBuilder RequireLocal<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (ctx, next) =>
            DeviceAuth.IsLocal(ctx.HttpContext) ? await next(ctx) : Results.StatusCode(StatusCodes.Status403Forbidden));
}
