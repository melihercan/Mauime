using Android.App;
using Android.Content;
using Microsoft.Maui.ApplicationModel;

namespace Me.Toolkit.Maui.Nfc;

/// <summary>NFC through Android's foreground dispatch.</summary>
internal sealed partial class AndroidNfc : INfc
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _isSessionEnabled;
    private bool _isActivityResumed;
    private bool _isForegroundDispatchEnabled;
    private bool _isDisposed;
    private Android.Nfc.Tag? _tag;

    internal AndroidNfc()
    {
        // The window having focus is the only thing available to tell a resumed activity from one
        // that was never resumed in the first place.
        _isActivityResumed = Platform.CurrentActivity?.HasWindowFocus ?? false;

        Platform.ActivityStateChanged += OnActivityStateChanged;
        NewIntentReceived += OnNewIntentReceived;
    }

    public event EventHandler<NfcTagDetectedEventArgs>? TagDetected;

    // Android's foreground dispatch has no timeout, so this never fires here. It is part of INfc
    // because iOS's reader session does time out.
#pragma warning disable CS0067
    public event EventHandler<EventArgs>? SessionTimeout;
#pragma warning restore CS0067

    public async Task EnableSessionAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _isSessionEnabled = true;

            if (_isActivityResumed)
            {
                EnableForegroundDispatch();
            }
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
            _isSessionEnabled = false;

            if (_isActivityResumed)
            {
                DisableForegroundDispatch();
            }
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
            return ReadWhileLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteNdefAsync(NdefMessage ndefMessage)
    {
        ArgumentNullException.ThrowIfNull(ndefMessage);

        var bytes = ndefMessage.ToByteArray();

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var ndef = ConnectWhileLocked();
            await ndef.WriteNdefMessageAsync(new Android.Nfc.NdefMessage(bytes)).ConfigureAwait(false);
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
            await Task.Delay(70).ConfigureAwait(false);

            var response = await ReadNdefAsync().ConfigureAwait(false);
            if (!response.ToByteArray().AsSpan().SequenceEqual(written))
            {
                return response;
            }
        }

        throw new InvalidOperationException(
            "The tag did not answer: its content still matched the written message after five attempts.");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Xamarinme's Dispose was empty, so both subscriptions outlived the instance and the
        // ActivityStateChanged handler kept it alive for the life of the process.
        Platform.ActivityStateChanged -= OnActivityStateChanged;
        NewIntentReceived -= OnNewIntentReceived;

        if (_isForegroundDispatchEnabled)
        {
            DisableForegroundDispatch();
        }

        _gate.Dispose();
    }

    private NdefMessage ReadWhileLocked()
    {
        using var ndef = ConnectWhileLocked();

        var message = ndef.NdefMessage
            ?? throw new InvalidOperationException("The tag in range carries no NDEF message.");

        return NdefMessage.FromByteArray(message.ToByteArray());
    }

    /// <summary>
    /// Opens a connection to the tag in range. The caller owns the returned object and must dispose
    /// it, which closes the connection.
    /// </summary>
    private Android.Nfc.Tech.Ndef ConnectWhileLocked()
    {
        if (!_isForegroundDispatchEnabled)
        {
            throw new InvalidOperationException(
                "No NFC session is enabled. Call EnableSessionAsync first.");
        }

        var tag = _tag ?? throw new InvalidOperationException("No tag is in range.");

        var ndef = Android.Nfc.Tech.Ndef.Get(tag)
            ?? throw new InvalidOperationException("The tag in range does not support NDEF.");

        try
        {
            ndef.Connect();
            return ndef;
        }
        catch
        {
            // Xamarinme's equivalent catch called ndef.IsConnected on a variable that was still
            // null whenever the failure happened before Ndef.Get returned, so the cleanup threw a
            // NullReferenceException over the real error.
            ndef.Dispose();
            throw;
        }
    }

    private void OnActivityStateChanged(object? sender, ActivityStateChangedEventArgs e)
    {
        // Deliberately not taking _gate: this runs on the UI thread, and blocking it behind an
        // in-flight tag read would freeze the app.
        switch (e.State)
        {
            case ActivityState.Resumed:
                _isActivityResumed = true;
                if (_isSessionEnabled)
                {
                    EnableForegroundDispatch();
                }

                break;

            case ActivityState.Paused:
                _isActivityResumed = false;
                if (_isSessionEnabled)
                {
                    DisableForegroundDispatch();
                }

                break;
        }
    }

    private void OnNewIntentReceived(object? sender, Intent intent)
    {
        if (intent.Action != Android.Nfc.NfcAdapter.ActionNdefDiscovered)
        {
            return;
        }

        var tag = GetTag(intent);
        if (tag is null)
        {
            return;
        }

        _tag = tag;

        using var ndef = Android.Nfc.Tech.Ndef.Get(tag);
        var cached = ndef?.CachedNdefMessage;
        if (cached is null)
        {
            return;
        }

        TagDetected?.Invoke(this, new NfcTagDetectedEventArgs(
            BitConverter.ToString(tag.GetId() ?? []).Replace("-", ":"),
            NdefMessage.FromByteArray(cached.ToByteArray())));
    }

    private static Android.Nfc.Tag? GetTag(Intent intent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return (Android.Nfc.Tag?)intent.GetParcelableExtra(
                Android.Nfc.NfcAdapter.ExtraTag,
                Java.Lang.Class.FromType(typeof(Android.Nfc.Tag)));
        }

#pragma warning disable CA1422 // The untyped overload is the only one before API 33.
        return intent.GetParcelableExtra(Android.Nfc.NfcAdapter.ExtraTag) as Android.Nfc.Tag;
#pragma warning restore CA1422
    }

    private void EnableForegroundDispatch()
    {
        if (_isForegroundDispatchEnabled)
        {
            return;
        }

        // Resolved at the point of use rather than captured in the constructor: Xamarinme held the
        // activity it was created with, which goes stale the first time the activity is recreated.
        var activity = Platform.CurrentActivity;
        var adapter = activity is null ? null : Android.Nfc.NfcAdapter.GetDefaultAdapter(activity);

        if (activity is null || adapter is null)
        {
            return;
        }

        // The system fills the tag details into this intent before delivering it, so it has to be
        // mutable. API 31 made one of Mutable/Immutable mandatory: Xamarinme passed 0, which throws
        // on Android 12 and later.
        var flags = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? PendingIntentFlags.Mutable
            : 0;

        var pendingIntent = PendingIntent.GetActivity(
            activity,
            0,
            new Intent(activity, activity.GetType()).AddFlags(ActivityFlags.SingleTop),
            flags);

        // Only NDEF intents. Android prefers ActionNdefDiscovered over the Tech and Tag intents,
        // which this library has no use for.
        var ndefFilter = new IntentFilter(Android.Nfc.NfcAdapter.ActionNdefDiscovered);
        ndefFilter.AddDataType("*/*");

        adapter.EnableForegroundDispatch(activity, pendingIntent, [ndefFilter], null);

        _isForegroundDispatchEnabled = true;
    }

    private void DisableForegroundDispatch()
    {
        if (!_isForegroundDispatchEnabled)
        {
            return;
        }

        var activity = Platform.CurrentActivity;
        if (activity is not null)
        {
            Android.Nfc.NfcAdapter.GetDefaultAdapter(activity)?.DisableForegroundDispatch(activity);
        }

        _isForegroundDispatchEnabled = false;
    }
}
