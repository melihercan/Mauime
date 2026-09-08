using Microsoft.Maui.Hosting;

namespace Me.Toolkit.Maui.Nfc;

/// <summary>
/// The implementation used on target frameworks with no NFC support: plain <c>net10.0</c>,
/// Mac Catalyst and Windows.
/// </summary>
/// <remarks>
/// <para>
/// <c>net10.0</c> is the slice a non-platform project resolves, and exists so such a project can
/// reference the package at all. Xamarinme.Nfc used <c>netstandard2.0</c> for the same role, where
/// <c>CrossNfc.Current</c> threw <see cref="NotImplementedException"/>.
/// </para>
/// <para>
/// Mac Catalyst has no NFC hardware API — CoreNFC is iOS-only — and Windows would need the PC/SC
/// smart-card path, which is an external USB reader rather than phone NFC and is not part of this
/// port.
/// </para>
/// </remarks>
internal static class NfcPlatform
{
    internal static INfc Create() => new UnsupportedNfc();

    internal static void Configure(MauiAppBuilder builder)
    {
        // Nothing to wire up.
    }
}

internal sealed class UnsupportedNfc : INfc
{
    // Declared so the type satisfies INfc; never raised, because nothing here ever detects a tag.
    // The compiler is told as much rather than left to warn about it.
#pragma warning disable CS0067
    public event EventHandler<NfcTagDetectedEventArgs>? TagDetected;
    public event EventHandler<EventArgs>? SessionTimeout;
#pragma warning restore CS0067

    public Task EnableSessionAsync() => throw NotSupported();

    public Task DisableSessionAsync() => throw NotSupported();

    public Task<NdefMessage> ReadNdefAsync() => throw NotSupported();

    public Task WriteNdefAsync(NdefMessage ndefMessage) => throw NotSupported();

    public Task<NdefMessage> WriteReadNdefAsync(NdefMessage ndefMessage) => throw NotSupported();

    public void Dispose()
    {
    }

    private static PlatformNotSupportedException NotSupported() =>
        new("Me.Toolkit.Maui.Nfc has no implementation for this platform. Android and iOS are supported.");
}
