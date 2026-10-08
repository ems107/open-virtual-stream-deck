using System.Management;
using System.Net;
using System.Net.NetworkInformation;

namespace OVSD.Host.SystemSetup;

/// <summary>Network the PC is on and whether Windows treats it as public or private.</summary>
public sealed record NetworkProfile(string Name, string InterfaceAlias, int InterfaceIndex, string Category);

/// <summary>
/// Windows "network profiles" (MSFT_NetConnectionProfile). OVSD is only reachable on private networks,
/// and Windows often marks a new WiFi as public: the settings page detects that and offers to change it.
/// </summary>
public static class NetworkProfiles
{
    private const string Scope = @"root\StandardCimv2";

    /// <summary>Profile of the network adapter that has <paramref name="address"/>.</summary>
    public static NetworkProfile? ForAddress(IPAddress address)
    {
        var index = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(address)))
            .Select(n => n.GetIPProperties().GetIPv4Properties()?.Index)
            .FirstOrDefault();
        return index is { } i ? All().FirstOrDefault(p => p.InterfaceIndex == i) : null;
    }

    public static List<NetworkProfile> All()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT Name, InterfaceAlias, InterfaceIndex, NetworkCategory FROM MSFT_NetConnectionProfile");
            return searcher.Get().Cast<ManagementObject>()
                .Select(o => new NetworkProfile(
                    o["Name"] as string ?? "",
                    o["InterfaceAlias"] as string ?? "",
                    Convert.ToInt32(o["InterfaceIndex"]),
                    Convert.ToInt32(o["NetworkCategory"]) switch { 0 => "public", 1 => "private", 2 => "domain", _ => "unknown" }))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Marks the network of an adapter as private (needs administrator).</summary>
    public static void MakePrivate(int interfaceIndex)
    {
        using var searcher = new ManagementObjectSearcher(Scope, $"SELECT * FROM MSFT_NetConnectionProfile WHERE InterfaceIndex = {interfaceIndex}");
        foreach (var profile in searcher.Get().Cast<ManagementObject>())
        {
            profile["NetworkCategory"] = 1;
            profile.Put();
        }
    }
}
