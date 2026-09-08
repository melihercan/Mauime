using System.Buffers.Binary;
using System.Collections;

namespace Mauime.Nfc;

/// <summary>
/// An NDEF message: an ordered, non-empty sequence of <see cref="NdefRecord"/>, and the parsing and
/// serialization of the NFC Forum NDEF binary format.
/// </summary>
/// <remarks>
/// <para>
/// Xamarinme.Nfc took these types from <c>NdefLibrary</c> 4.1.0, whose last release was in 2017 and
/// which ships a netstandard1.4 asset only. It still works on .NET 10, but it sat in the public API
/// — <c>ReadNdefAsync</c> returned its <c>NdefMessage</c> — so every consumer inherited it. Mauime
/// ships under new package IDs with no consumers to keep compatible, so the format is implemented
/// here instead and the package has no third-party dependencies at all.
/// </para>
/// <para>
/// Chunked records are not supported. They are rare, no platform in this library produces them, and
/// silently mis-parsing one would be worse than refusing it.
/// </para>
/// </remarks>
public sealed class NdefMessage : IReadOnlyList<NdefRecord>
{
    private const byte MessageBegin = 0x80;
    private const byte MessageEnd = 0x40;
    private const byte ChunkFlag = 0x20;
    private const byte ShortRecord = 0x10;
    private const byte IdLengthPresent = 0x08;
    private const byte TypeNameFormatMask = 0x07;

    private readonly NdefRecord[] _records;

    /// <summary>Creates a message from a sequence of records.</summary>
    public NdefMessage(IEnumerable<NdefRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        _records = [.. records];

        if (Array.IndexOf(_records, null!) >= 0)
        {
            throw new ArgumentException("An NDEF message cannot contain a null record.", nameof(records));
        }
    }

    /// <summary>Creates a message from records.</summary>
    public NdefMessage(params NdefRecord[] records)
        : this((IEnumerable<NdefRecord>)records)
    {
    }

    /// <inheritdoc />
    public int Count => _records.Length;

    /// <inheritdoc />
    public NdefRecord this[int index] => _records[index];

    /// <inheritdoc />
    public IEnumerator<NdefRecord> GetEnumerator() => ((IEnumerable<NdefRecord>)_records).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _records.GetEnumerator();

    /// <summary>Parses an NDEF message from its binary form.</summary>
    /// <exception cref="ArgumentException">
    /// The bytes are not a well-formed NDEF message: truncated, missing the message-begin or
    /// message-end flag, or carrying trailing bytes after the final record.
    /// </exception>
    /// <exception cref="NotSupportedException">The message contains a chunked record.</exception>
    public static NdefMessage FromByteArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("An NDEF message must contain at least one record.", nameof(bytes));
        }

        var records = new List<NdefRecord>();
        var offset = 0;
        var ended = false;

        while (offset < bytes.Length)
        {
            if (ended)
            {
                throw new ArgumentException(
                    "Trailing bytes after the record carrying the message-end flag.", nameof(bytes));
            }

            var header = Take(bytes, ref offset, 1, nameof(bytes))[0];

            if (records.Count == 0 && (header & MessageBegin) == 0)
            {
                throw new ArgumentException(
                    "The first record does not carry the message-begin flag.", nameof(bytes));
            }

            if (records.Count > 0 && (header & MessageBegin) != 0)
            {
                throw new ArgumentException(
                    "A record other than the first carries the message-begin flag.", nameof(bytes));
            }

            if ((header & ChunkFlag) != 0)
            {
                throw new NotSupportedException("Chunked NDEF records are not supported.");
            }

            var typeLength = Take(bytes, ref offset, 1, nameof(bytes))[0];

            var payloadLength = (header & ShortRecord) != 0
                ? Take(bytes, ref offset, 1, nameof(bytes))[0]
                : BinaryPrimitives.ReadUInt32BigEndian(Take(bytes, ref offset, 4, nameof(bytes)));

            var idLength = (header & IdLengthPresent) != 0
                ? Take(bytes, ref offset, 1, nameof(bytes))[0]
                : 0;

            // Guard before the cast: a four-byte payload length can name more bytes than an int
            // can index, and far more than could possibly be present.
            if (payloadLength > (uint)(bytes.Length - offset))
            {
                throw new ArgumentException(
                    $"Record payload length {payloadLength} exceeds the {bytes.Length - offset} bytes remaining.",
                    nameof(bytes));
            }

            var type = Take(bytes, ref offset, typeLength, nameof(bytes));
            var id = Take(bytes, ref offset, idLength, nameof(bytes));
            var payload = Take(bytes, ref offset, (int)payloadLength, nameof(bytes));

            records.Add(new NdefRecord(
                (NdefTypeNameFormat)(header & TypeNameFormatMask),
                type.ToArray(),
                payload.ToArray(),
                id.ToArray()));

            ended = (header & MessageEnd) != 0;
        }

        if (!ended)
        {
            throw new ArgumentException(
                "The last record does not carry the message-end flag.", nameof(bytes));
        }

        return new NdefMessage(records);
    }

    /// <summary>Serializes the message to its binary form.</summary>
    /// <exception cref="InvalidOperationException">The message has no records.</exception>
    public byte[] ToByteArray()
    {
        if (_records.Length == 0)
        {
            throw new InvalidOperationException(
                "An NDEF message must contain at least one record. Use NdefRecord.Empty for an empty message.");
        }

        using var stream = new MemoryStream();

        // Hoisted out of the loop: a stackalloc inside one accumulates a frame per iteration.
        Span<byte> longPayloadLength = stackalloc byte[4];

        for (var i = 0; i < _records.Length; i++)
        {
            var record = _records[i];
            var type = record.Type.Span;
            var id = record.Id.Span;
            var payload = record.Payload.Span;

            if (type.Length > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Record {i} has a {type.Length}-byte type; the NDEF type length field holds one byte.");
            }

            if (id.Length > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Record {i} has a {id.Length}-byte id; the NDEF id length field holds one byte.");
            }

            var shortRecord = payload.Length <= byte.MaxValue;

            var header = (byte)record.TypeNameFormat;
            if (i == 0) header |= MessageBegin;
            if (i == _records.Length - 1) header |= MessageEnd;
            if (shortRecord) header |= ShortRecord;
            if (id.Length > 0) header |= IdLengthPresent;

            stream.WriteByte(header);
            stream.WriteByte((byte)type.Length);

            if (shortRecord)
            {
                stream.WriteByte((byte)payload.Length);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(longPayloadLength, (uint)payload.Length);
                stream.Write(longPayloadLength);
            }

            if (id.Length > 0)
            {
                stream.WriteByte((byte)id.Length);
            }

            stream.Write(type);
            stream.Write(id);
            stream.Write(payload);
        }

        return stream.ToArray();
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> bytes, ref int offset, int count, string parameterName)
    {
        if (count > bytes.Length - offset)
        {
            throw new ArgumentException(
                $"Truncated NDEF message: {count} bytes needed at offset {offset}, "
                + $"{bytes.Length - offset} available.",
                parameterName);
        }

        var slice = bytes.Slice(offset, count);
        offset += count;
        return slice;
    }
}
