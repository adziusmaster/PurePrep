using System.Net;
using System.Net.Sockets;

namespace PurePrep.Ai;

/// <summary>
/// Guards the server against SSRF: the AI parser fetches a user-supplied URL from inside our VPS,
/// so we must reject anything that isn't a public http(s) address. Blocks non-http schemes and any
/// URL that resolves to a loopback, private, link-local (incl. cloud metadata 169.254.169.254),
/// or otherwise non-public IP address.
/// </summary>
public interface IUrlGuard
{
    Task<bool> IsPublicHttpAsync(Uri url, CancellationToken ct = default);
}

public sealed class UrlGuard : IUrlGuard
{
    public async Task<bool> IsPublicHttpAsync(Uri url, CancellationToken ct = default)
    {
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            return false;

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(url.Host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(url.Host, ct);
        }
        catch (SocketException)
        {
            return false;
        }

        return addresses.Length > 0 && addresses.All(IsPublic);
    }

    private static bool IsPublic(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return false;

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 0.0.0.0/8, 10/8, 100.64/10 (CGNAT), 127/8, 169.254/16 (link-local + metadata),
            // 172.16/12, 192.0.0/24, 192.168/16, 198.18/15, 240/4 (reserved).
            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                100 when bytes[1] >= 64 && bytes[1] <= 127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] >= 16 && bytes[1] <= 31 => false,
                192 when bytes[1] == 168 => false,
                192 when bytes[1] == 0 && bytes[2] == 0 => false,
                198 when bytes[1] == 18 || bytes[1] == 19 => false,
                >= 240 => false,
                _ => true,
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
                return false;
            if (ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.IPv6Loopback))
                return false;
            // Unique local addresses fc00::/7.
            if ((bytes[0] & 0xFE) == 0xFC)
                return false;
            return true;
        }

        return false;
    }
}
