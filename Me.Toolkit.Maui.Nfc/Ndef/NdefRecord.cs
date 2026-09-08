namespace Me.Toolkit.Maui.Nfc;

/// <summary>
/// One record of an <see cref="NdefMessage"/>.
/// </summary>
/// <remarks>
/// Records are immutable. The message-begin, message-end and short-record header flags are not
/// represented here: they are decided by position and payload size when the message is serialized,
/// so there is no way to build a message whose flags contradict its contents.
/// </remarks>
public sealed class NdefRecord
{
    /// <summary>Creates a record.</summary>
    /// <param name="typeNameFormat">How <paramref name="type"/> should be interpreted.</param>
    /// <param name="type">The record type, empty for <see cref="NdefTypeNameFormat.Empty"/>.</param>
    /// <param name="payload">The record payload.</param>
    /// <param name="id">An optional record identifier.</param>
    public NdefRecord(
        NdefTypeNameFormat typeNameFormat,
        ReadOnlyMemory<byte> type = default,
        ReadOnlyMemory<byte> payload = default,
        ReadOnlyMemory<byte> id = default)
    {
        TypeNameFormat = typeNameFormat;
        Type = type;
        Payload = payload;
        Id = id;
    }

    /// <summary>How <see cref="Type"/> should be interpreted.</summary>
    public NdefTypeNameFormat TypeNameFormat { get; }

    /// <summary>The record type.</summary>
    public ReadOnlyMemory<byte> Type { get; }

    /// <summary>The record payload.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>The record identifier. Empty when the record carries none.</summary>
    public ReadOnlyMemory<byte> Id { get; }

    /// <summary>The empty record, which is what an otherwise empty NDEF message contains.</summary>
    public static NdefRecord Empty { get; } = new(NdefTypeNameFormat.Empty);
}
