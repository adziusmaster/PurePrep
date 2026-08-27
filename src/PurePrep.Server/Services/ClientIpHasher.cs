using System.Security.Cryptography;
using System.Text;

namespace PurePrep.Server.Services;

/// <summary>
/// Turns a client IP into a stable, salted, non-reversible token used only to enforce the
/// free-credit seed cap. The raw address is never stored.
/// </summary>
public interface IClientIpHasher
{
    /// <summary>The origin token for <paramref name="ip"/>, or <c>null</c> when there is no address.</summary>
    string? Hash(string? ip);
}

public sealed class ClientIpHasher(string salt) : IClientIpHasher
{
    public string? Hash(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return null;

        // The IPv4 space is small enough to enumerate, so an unsalted digest would be trivially
        // reversible. The salt is what makes the stored token meaningless outside this deployment.
        var bytes = Encoding.UTF8.GetBytes($"{salt}|{ip.Trim()}");
        return Convert.ToHexStringLower(SHA256.HashData(bytes))[..32];
    }
}
