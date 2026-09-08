# Me.Toolkit.Maui.Nfc

NFC for .NET MAUI: read and write NDEF messages on a tag.

Ported from `Xamarinme.Nfc`, which was never published. **Android and iOS are implemented.** Mac
Catalyst and Windows compile but throw `PlatformNotSupportedException`, so an app targeting every
platform needs no conditional code around the registration.

## Install and register

```csharp
using Me.Toolkit.Maui.Nfc;

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseMeToolkitMauiNfc();

    return builder.Build();
}
```

`UseMeToolkitMauiNfc` registers `INfc` as a singleton and wires up the platform plumbing — on Android that
includes the `OnNewIntent` hook that delivers tag discoveries. **Nothing needs to be added to
`MainActivity`.** Xamarinme required a hand-written `Xamarinme.Nfc.OnNewIntent(intent)` call there,
with nothing to catch it if you forgot.

Then inject it:

```csharp
public sealed class TagPage(INfc nfc)
{
    public async Task StartAsync()
    {
        nfc.TagDetected += (_, e) =>
            Debug.WriteLine($"{e.TagId}: {e.NdefMessage.Count} record(s)");

        await nfc.EnableSessionAsync();
    }
}
```

## Reading and writing

```csharp
var message = await nfc.ReadNdefAsync();

var text = new NdefMessage(new NdefRecord(
    NdefTypeNameFormat.NfcWellKnown,
    type: "T"u8.ToArray(),
    payload: [0x02, (byte)'e', (byte)'n', .. "Hello"u8]));

await nfc.WriteNdefAsync(text);
```

`WriteReadNdefAsync` is a request/response exchange with an active tag rather than a write followed
by a read-back check: it writes, then polls until the tag's content *differs* from what was written,
and returns that. A tag whose content still matches has not answered yet.

## Platform requirements

**Android** needs `android.permission.NFC` in the manifest. The library uses foreground dispatch, so
tags are delivered only while your activity is in the foreground and a session is enabled.

**iOS** needs the Near Field Communication Tag Reading capability, an `NFCReaderUsageDescription` in
`Info.plist`, and the `com.apple.developer.nfc.readersession.formats` entitlement. iOS presents its
own system sheet while a session is open, and ends the session on a timeout — that is what
`SessionTimeout` reports. CoreNFC's NDEF reader session exposes no tag identifier, so `TagId` is an
empty string there.

## NDEF

`NdefMessage` and `NdefRecord` are implemented in this package. Xamarinme returned
`NdefLibrary.Ndef.NdefMessage` from a 2017 package with a netstandard1.4 asset, which put a dead
dependency in the public API of everything that used it; `Me.Toolkit.Maui.Nfc` has no third-party
dependencies at all. The binary format is the same one, verified byte for byte against what
NdefLibrary 4.1.0 produces.

Chunked records are refused with `NotSupportedException` rather than mis-parsed. They are rare, and
no platform here produces one.

## Licence

MIT.
