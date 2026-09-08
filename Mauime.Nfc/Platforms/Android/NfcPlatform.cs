using Android.Content;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace Mauime.Nfc;

internal static class NfcPlatform
{
    internal static INfc Create() => new AndroidNfc();

    internal static void Configure(MauiAppBuilder builder) =>
        builder.ConfigureLifecycleEvents(lifecycle => lifecycle.AddAndroid(android =>
            android.OnNewIntent((_, intent) =>
            {
                if (intent is not null)
                {
                    AndroidNfc.PublishNewIntent(intent);
                }
            })));
}

/// <summary>
/// Delivers tag intents from the activity to whichever <see cref="AndroidNfc"/> is listening.
/// </summary>
/// <remarks>
/// Android hands a discovered tag to the activity through <c>onNewIntent</c>, and a broadcast
/// receiver cannot substitute for it. Xamarinme exposed a public <c>Nfc.OnNewIntent(intent)</c> that
/// the consumer had to call from their own <c>MainActivity</c>, with nothing to catch it if they
/// did not. <see cref="NfcPlatform.Configure"/> now wires the same hook through
/// <c>ConfigureLifecycleEvents</c>, so the bridge is internal and the consumer edits nothing.
/// </remarks>
internal sealed partial class AndroidNfc
{
    private static event EventHandler<Intent>? NewIntentReceived;

    internal static void PublishNewIntent(Intent intent) =>
        NewIntentReceived?.Invoke(null, intent);
}
