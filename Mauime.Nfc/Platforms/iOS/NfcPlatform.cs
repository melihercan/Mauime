using Microsoft.Maui.Hosting;

namespace Mauime.Nfc;

internal static class NfcPlatform
{
    internal static INfc Create() => new IosNfc();

    internal static void Configure(MauiAppBuilder builder)
    {
        // CoreNFC needs no application lifecycle hooks: a reader session is started and invalidated
        // explicitly, and iOS presents its own system sheet while one is open.
    }
}
