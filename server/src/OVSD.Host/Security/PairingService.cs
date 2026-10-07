using System.Security.Cryptography;
using OVSD.Core;
using OVSD.Core.Model;
using OVSD.Core.Storage;

namespace OVSD.Host.Security;

public sealed record PairingCode(string Code, DateTimeOffset ExpiresAt);

public sealed record PairingResult(string DeviceId, string DeviceName, string Token);

/// <summary>
/// Short-lived 6-digit codes shown on the PC (as QR or PIN). Claiming one creates a device with its own token.
/// Failed attempts are rate limited so the PIN cannot be brute-forced from the LAN.
/// </summary>
public sealed class PairingService(ConfigRepository config, TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const int MaxFailuresPerMinute = 10;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTimeOffset> _codes = new();
    private readonly Queue<DateTimeOffset> _failures = new();

    public PairingCode CreateCode()
    {
        lock (_lock)
        {
            Prune();
            string code;
            do code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            while (_codes.ContainsKey(code));
            var expires = time.GetUtcNow() + Lifetime;
            _codes[code] = expires;
            return new PairingCode(code, expires);
        }
    }

    /// <summary>Returns null if the code is wrong, expired or too many attempts failed recently.</summary>
    public PairingResult? Claim(string code, string? deviceName)
    {
        lock (_lock)
        {
            Prune();
            var now = time.GetUtcNow();
            while (_failures.Count > 0 && now - _failures.Peek() > TimeSpan.FromMinutes(1)) _failures.Dequeue();
            if (_failures.Count >= MaxFailuresPerMinute) return null;

            if (!_codes.Remove(code.Trim()))
            {
                _failures.Enqueue(now);
                return null;
            }
        }

        var token = DeviceAuth.NewToken();
        var name = string.IsNullOrWhiteSpace(deviceName) ? "Device" : deviceName.Trim()[..Math.Min(deviceName.Trim().Length, 60)];
        var device = new Device
        {
            Id = Ids.New(),
            Name = name,
            TokenHash = DeviceAuth.Hash(token),
            PairedAt = time.GetUtcNow(),
        };
        config.Update(c => c with { Devices = [.. c.Devices, device] });
        return new PairingResult(device.Id, device.Name, token);
    }

    private void Prune()
    {
        var now = time.GetUtcNow();
        foreach (var expired in _codes.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList()) _codes.Remove(expired);
    }
}
