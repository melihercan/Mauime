using CoreNFC;
using Foundation;

namespace Mauime.Nfc;

/// <summary>NFC through CoreNFC's NDEF reader session.</summary>
internal sealed class IosNfc : NFCNdefReaderSessionDelegate, INfc
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _isSessionEnabled;
    private NFCNdefReaderSession? _session;
    private INFCNdefTag? _tag;

    public event EventHandler<NfcTagDetectedEventArgs>? TagDetected;

    public event EventHandler<EventArgs>? SessionTimeout;

    public async Task EnableSessionAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isSessionEnabled)
            {
                return;
            }

            // A null queue means CoreNFC creates a private serial one for its callbacks, which is
            // the documented default. Xamarinme passed DispatchQueue.CurrentQueue, obsolete since
            // iOS 6 and dependent on whichever queue happened to call EnableSessionAsync.
            _session = new NFCNdefReaderSession(this, queue: null, invalidateAfterFirstRead: false)
            {
                AlertMessage = "Tap a tag to start...",
            };

            _session.BeginSession();
            _isSessionEnabled = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisableSessionAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isSessionEnabled)
            {
                return;
            }

            _session?.InvalidateSession();
            _session = null;
            _tag = null;
            _isSessionEnabled = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<NdefMessage> ReadNdefAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await ReadWhileLockedAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteNdefAsync(NdefMessage ndefMessage)
    {
        ArgumentNullException.ThrowIfNull(ndefMessage);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var tag = TagWhileLocked();
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            tag.WriteNdef(ToPlatform(ndefMessage), error =>
            {
                // Xamarinme threw from inside this callback. It runs on CoreNFC's dispatch queue,
                // so the throw never reached the awaiting caller: it took down the queue instead,
                // and the await hung forever.
                if (error is not null)
                {
                    completion.TrySetException(Failure(error));
                }
                else
                {
                    completion.TrySetResult();
                }
            });

            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<NdefMessage> WriteReadNdefAsync(NdefMessage ndefMessage)
    {
        ArgumentNullException.ThrowIfNull(ndefMessage);

        await WriteNdefAsync(ndefMessage).ConfigureAwait(false);

        // The tag is expected to replace what was written with its answer, so a message that still
        // matches means it has not answered yet.
        var written = ndefMessage.ToByteArray();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Task.Delay(40).ConfigureAwait(false);

            var response = await ReadNdefAsync().ConfigureAwait(false);
            if (!response.ToByteArray().AsSpan().SequenceEqual(written))
            {
                return response;
            }
        }

        throw new InvalidOperationException(
            "The tag did not answer: its content still matched the written message after five attempts.");
    }

    public override async void DidDetectTags(NFCNdefReaderSession session, INFCNdefTag[] tags)
    {
        // An override of a void delegate method, so nothing can observe an exception thrown out of
        // it and an escaping one would terminate the process.
        try
        {
            if (tags.Length == 0)
            {
                return;
            }

            _tag = tags[0];
            await session.ConnectToTagAsync(_tag).ConfigureAwait(false);

            var message = await ReadNdefAsync().ConfigureAwait(false);

            // CoreNFC's NDEF reader session exposes no tag identifier, so there is none to report.
            TagDetected?.Invoke(this, new NfcTagDetectedEventArgs(string.Empty, message));
        }
        catch (Exception exception)
        {
            session.InvalidateSession(exception.Message);
        }
    }

    public override void DidDetect(NFCNdefReaderSession session, NFCNdefMessage[] messages)
    {
        // Only called when the delegate does not implement DidDetectTags. This one does, so reaching
        // here would mean iOS chose the read-only path and no tag is available to write to.
        session.InvalidateSession("This session reads tags, not detached messages.");
    }

    public override async void DidInvalidate(NFCNdefReaderSession session, NSError error)
    {
        try
        {
            await DisableSessionAsync().ConfigureAwait(false);

            if ((NFCReaderError)(long)error.Code == NFCReaderError.ReaderSessionInvalidationErrorSessionTimeout)
            {
                SessionTimeout?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // The session is already gone; there is nowhere left to report a failure to tear it down.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session?.InvalidateSession();
            _session = null;
            _tag = null;
            _gate.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task<NdefMessage> ReadWhileLockedAsync()
    {
        var tag = TagWhileLocked();
        var completion = new TaskCompletionSource<NdefMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        tag.ReadNdef((message, error) =>
        {
            if (error is not null)
            {
                completion.TrySetException(Failure(error));
            }
            else if (message is null)
            {
                completion.TrySetException(
                    new InvalidOperationException("The tag in range carries no NDEF message."));
            }
            else
            {
                completion.TrySetResult(FromPlatform(message));
            }
        });

        return await completion.Task.ConfigureAwait(false);
    }

    private INFCNdefTag TagWhileLocked()
    {
        if (!_isSessionEnabled)
        {
            throw new InvalidOperationException(
                "No NFC session is enabled. Call EnableSessionAsync first.");
        }

        return _tag ?? throw new InvalidOperationException("No tag is in range.");
    }

    private static NdefMessage FromPlatform(NFCNdefMessage message) =>
        new(message.Records.Select(record => new NdefRecord(
            (NdefTypeNameFormat)(byte)record.TypeNameFormat,
            record.Type.ToArray(),
            record.Payload.ToArray(),
            record.Identifier.ToArray())));

    private static NFCNdefMessage ToPlatform(NdefMessage message) =>
        new([.. message.Select(record => new NFCNdefPayload(
            format: (NFCTypeNameFormat)(byte)record.TypeNameFormat,
            type: NSData.FromArray(record.Type.ToArray()),
            identifier: NSData.FromArray(record.Id.ToArray()),
            payload: NSData.FromArray(record.Payload.ToArray())))]);

    private static InvalidOperationException Failure(NSError error) =>
        new($"The NFC operation failed: {error.LocalizedDescription}");
}
