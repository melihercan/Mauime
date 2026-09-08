using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Me.Toolkit.Maui.WebHostPatch;

/// <summary>
/// One network interface, reduced to the three things address selection needs.
/// </summary>
/// <remarks>
/// This exists so the selection rules can be tested. <see cref="NetworkInterface"/> is abstract with
/// no constructible implementation - <c>GetIPProperties()</c> returns another abstract type whose
/// address collections have internal constructors - so a fake interface cannot be built, and the
/// only alternative would be to test against whatever the build machine happens to have.
/// </remarks>
internal readonly record struct InterfaceCandidate(
    NetworkInterfaceType Type,
    OperationalStatus Status,
    IReadOnlyList<IPAddress> Addresses);

/// <summary>Finds the address another device on the network can reach this one at.</summary>
public static class NetworkAddress
{
    /// <summary>
    /// The first usable IPv4 address on an operational interface, or <see langword="null"/> when
    /// there is none.
    /// </summary>
    /// <remarks>
    /// Loopback and link-local (169.254.x.x) addresses are skipped: nothing else on the network can
    /// reach them, and a link-local address means DHCP failed.
    ///
    /// Ported from the Xamarinme demo's <c>NetworkHelper</c>, with its bug fixed. That version
    /// picked the first qualifying interface and only then filtered its addresses, so a machine
    /// whose first interface held nothing but a link-local address reported no address at all, even
    /// with a working connection on the next one. Every interface is now searched.
    /// </remarks>
    public static IPAddress? GetLocalAddress() =>
        SelectAddress(NetworkInterface.GetAllNetworkInterfaces().Select(ToCandidate));

    /// <summary>
    /// Picks the address to report from a set of candidate interfaces.
    /// </summary>
    /// <remarks>
    /// The interface *type* is deliberately not used to decide whether a candidate qualifies, only
    /// to order the ones that do.
    ///
    /// An earlier version required <see cref="NetworkInterfaceType.Ethernet"/> or
    /// <see cref="NetworkInterfaceType.Wireless80211"/>, which returned null on Android with a
    /// perfectly good Wi-Fi connection: .NET classifies an interface on Linux by reading
    /// <c>/sys/class/net/&lt;name&gt;/type</c>, and Android's SELinux policy denies that file - to
    /// an app, and even to the more privileged <c>shell</c> user. Every interface therefore reports
    /// <see cref="NetworkInterfaceType.Unknown"/>, and the filter rejected the one interface that
    /// worked. The demo app showed <c>http://0.0.0.0:5001/</c> under a label telling the user to
    /// open it from another device.
    ///
    /// Loopback is excluded by address rather than by type, in <see cref="IsRoutableIPv4"/>. On the
    /// device this was verified against, Android does classify <c>lo</c> correctly, so the type
    /// would have been enough - but the whole point here is that the classification cannot be
    /// relied on, and an address check does not depend on it. Observed there:
    /// <c>wlan0|Unknown|Up|[192.168.1.16]</c>, <c>dummy0|Unknown|Up|[]</c>,
    /// <c>lo|Loopback|Up|[127.0.0.1]</c>.
    /// </remarks>
    internal static IPAddress? SelectAddress(IEnumerable<InterfaceCandidate> candidates) =>
        candidates
            .Where(IsUsable)
            .OrderBy(Preference)
            .SelectMany(candidate => candidate.Addresses)
            .FirstOrDefault(IsRoutableIPv4);

    private static InterfaceCandidate ToCandidate(NetworkInterface network) =>
        new(network.NetworkInterfaceType,
            network.OperationalStatus,
            network.GetIPProperties().UnicastAddresses.Select(address => address.Address).ToArray());

    /// <remarks>
    /// <see cref="OperationalStatus.Unknown"/> counts as usable for the same reason the type is not
    /// filtered on: a platform that will not say is not a platform saying no.
    /// </remarks>
    private static bool IsUsable(InterfaceCandidate candidate) =>
        candidate.Status is OperationalStatus.Up or OperationalStatus.Unknown;

    /// <summary>
    /// Orders qualifying interfaces so a platform that *does* report types still prefers the
    /// obvious one. Ties keep enumeration order, which is what the previous version relied on.
    /// </summary>
    private static int Preference(InterfaceCandidate candidate) => candidate.Type switch
    {
        NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet => 0,
        NetworkInterfaceType.Unknown => 1,
        _ => 2,
    };

    private static bool IsRoutableIPv4(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork
        && !IPAddress.IsLoopback(address)
        && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal);
}
