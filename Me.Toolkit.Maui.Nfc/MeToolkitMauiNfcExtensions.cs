using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Me.Toolkit.Maui.Nfc;

/// <summary>Registers NFC support on a <see cref="MauiAppBuilder"/>.</summary>
public static class MeToolkitMauiNfcExtensions
{
    /// <summary>
    /// Registers <see cref="INfc"/> as a singleton and wires up whatever platform plumbing the
    /// implementation needs.
    /// </summary>
    /// <remarks>
    /// On Android this includes the <c>OnNewIntent</c> hook that delivers tag discoveries. Xamarinme
    /// required the consumer to add that call to their own <c>MainActivity</c> by hand, with nothing
    /// to catch it if they forgot; here the library wires it through
    /// <c>ConfigureLifecycleEvents</c> itself.
    ///
    /// Safe to call on every platform. On one without an implementation the registration succeeds
    /// and the methods throw <see cref="PlatformNotSupportedException"/> when used, so an app
    /// targeting all platforms does not need to guard the call.
    /// </remarks>
    public static MauiAppBuilder UseMeToolkitMauiNfc(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<INfc>(_ => NfcPlatform.Create());
        NfcPlatform.Configure(builder);

        return builder;
    }
}
