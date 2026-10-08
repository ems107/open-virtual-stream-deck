namespace OVSD.Host.SystemSetup;

/// <summary>
/// Decides at startup whether OVSD listens on the LAN or only on this PC ("Ovsd:Network"):
///   auto (default) — on the LAN once the firewall lets OVSD through; until then only on localhost,
///                    so Windows never shows its "allow access?" prompt (Settings has the button instead).
///   lan            — always on every interface.
///   local          — only on this PC.
/// </summary>
public sealed class LanAccess
{
    public LanAccess(string mode, string program)
    {
        Mode = mode.ToLowerInvariant();
        FirewallAllowed = FirewallRules.IsAllowed(program);
        Listening = Mode switch
        {
            "lan" => true,
            "local" => false,
            _ => FirewallAllowed,
        };
    }

    public string Mode { get; }

    /// <summary>Whether the server accepts connections from other devices (it binds every interface).</summary>
    public bool Listening { get; }

    public bool FirewallAllowed { get; }
}
