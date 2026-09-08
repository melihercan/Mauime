namespace Me.Toolkit.Maui.Nfc;

/// <summary>
/// Reads and writes NDEF messages on an NFC tag.
/// </summary>
/// <remarks>
/// Resolve this from dependency injection after calling
/// <see cref="MeToolkitMauiNfcExtensions.UseMeToolkitMauiNfc"/>. Android and iOS have implementations; every other
/// platform throws <see cref="PlatformNotSupportedException"/> from every method.
/// </remarks>
public interface INfc : IDisposable
{
    /// <summary>Raised when a tag comes into range and its NDEF message has been read.</summary>
    event EventHandler<NfcTagDetectedEventArgs>? TagDetected;

    /// <summary>Raised when the platform ends the reader session because nothing was tapped.</summary>
    /// <remarks>iOS only. Android's foreground dispatch has no timeout.</remarks>
    event EventHandler<EventArgs>? SessionTimeout;

    /// <summary>Starts listening for tags.</summary>
    Task EnableSessionAsync();

    /// <summary>Stops listening for tags.</summary>
    Task DisableSessionAsync();

    /// <summary>Reads the NDEF message from the tag currently in range.</summary>
    /// <exception cref="InvalidOperationException">No session is enabled, or no tag is in range.</exception>
    Task<NdefMessage> ReadNdefAsync();

    /// <summary>Writes an NDEF message to the tag currently in range.</summary>
    /// <exception cref="InvalidOperationException">No session is enabled, or no tag is in range.</exception>
    Task WriteNdefAsync(NdefMessage ndefMessage);

    /// <summary>
    /// Writes <paramref name="ndefMessage"/>, then polls the tag until its content differs from what
    /// was written, and returns that content.
    /// </summary>
    /// <remarks>
    /// This is a request/response exchange with an active tag, not a write followed by a read-back
    /// check: the tag is expected to *replace* the written message with its answer, so content that
    /// still matches what was written means the tag has not answered yet. Five attempts, then
    /// <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The tag did not answer.</exception>
    Task<NdefMessage> WriteReadNdefAsync(NdefMessage ndefMessage);
}
