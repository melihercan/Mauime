using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mauime.WebHostPatch;

/// <summary>Finds the address another device on the network can reach this one at.</summary>
public static class NetworkAddress
{
    /// <summary>
    /// The first usable IPv4 address on an operational Ethernet or Wi-Fi interface, or
    /// <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// Link-local addresses (169.254.x.x) are skipped: they mean DHCP failed, and nothing else can
    /// route to them.
    ///
    /// Ported from the Xamarinme demo's <c>NetworkHelper</c>, with its bug fixed. That version
    /// picked the first qualifying interface and only then filtered its addresses, so a machine
    /// whose first interface held nothing but a link-local address reported no address at all, even
    /// with a working connection on the next one. Every interface is now searched.
    /// </remarks>
    public static IPAddress? GetLocalAddress() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsUsable)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .FirstOrDefault(IsRoutableIPv4);

    private static bool IsUsable(NetworkInterface network) =>
        network.OperationalStatus == OperationalStatus.Up
        && network.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211;

    private static bool IsRoutableIPv4(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork
        && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal);
}
