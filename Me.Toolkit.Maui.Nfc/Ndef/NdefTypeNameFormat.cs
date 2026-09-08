namespace Me.Toolkit.Maui.Nfc;

/// <summary>
/// How to interpret an <see cref="NdefRecord.Type"/>, from the low three bits of the record header.
/// </summary>
/// <remarks>
/// The numeric values are the on-the-wire TNF values from the NFC Forum NDEF specification and are
/// part of the format, not an implementation detail.
/// </remarks>
public enum NdefTypeNameFormat : byte
{
    /// <summary>No type, id or payload. The record an empty message is made of.</summary>
    Empty = 0x00,

    /// <summary>An NFC Forum well-known type, such as "T" for text or "U" for a URI.</summary>
    NfcWellKnown = 0x01,

    /// <summary>An RFC 2046 media type.</summary>
    Mime = 0x02,

    /// <summary>An absolute URI, per RFC 3986.</summary>
    AbsoluteUri = 0x03,

    /// <summary>An NFC Forum external type.</summary>
    NfcExternal = 0x04,

    /// <summary>The payload type is unknown; <see cref="NdefRecord.Type"/> must be empty.</summary>
    Unknown = 0x05,

    /// <summary>A middle or terminating chunk of a chunked record.</summary>
    Unchanged = 0x06,

    /// <summary>Reserved by the specification.</summary>
    Reserved = 0x07,
}
