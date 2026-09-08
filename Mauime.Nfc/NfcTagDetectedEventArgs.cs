namespace Mauime.Nfc;

/// <summary>Carries the tag that came into range and the NDEF message read from it.</summary>
public sealed class NfcTagDetectedEventArgs : EventArgs
{
    /// <summary>Creates the event arguments.</summary>
    public NfcTagDetectedEventArgs(string tagId, NdefMessage ndefMessage)
    {
        TagId = tagId;
        NdefMessage = ndefMessage;
    }

    /// <summary>
    /// The tag identifier as colon-separated hex, or an empty string when the platform does not
    /// report one. iOS does not: CoreNFC's NDEF reader session exposes no tag identifier.
    /// </summary>
    public string TagId { get; }

    /// <summary>The NDEF message read from the tag.</summary>
    public NdefMessage NdefMessage { get; }
}
