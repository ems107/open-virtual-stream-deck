using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OVSD.Host;

public static class NetworkInfo
{
    /// <summary>IPv4 addresses of interfaces that are up, those with a gateway (the real LAN ones) first.</summary>
    public static IReadOnlyList<IPAddress> GetLanAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => n.GetIPProperties())
            .OrderByDescending(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .SelectMany(p => p.UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && !IsLinkLocal(a))
            .Distinct()
            .ToList();

    private static bool IsLinkLocal(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    public static string GetPrimaryUrl(int port)
    {
        var ip = GetLanAddresses().FirstOrDefault() ?? IPAddress.Loopback;
        return $"http://{ip}:{port}/";
    }

    public static List<string> GetAllUrls(int port) =>
        GetLanAddresses().Select(ip => $"http://{ip}:{port}/").ToList();
}
