using System.Collections.ObjectModel;
using System.Text;
using Me.Toolkit.Maui.Nfc;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;

namespace DemoApp.ViewModels;

/// <summary>
/// Enables an NFC session and shows what the tags carry.
///
/// The two <see cref="INfc"/> events are consumed as observables rather than with <c>+=</c>, which
/// is how the subscriptions come to be disposed with the view model rather than outliving it — the
/// exact leak Xamarinme's empty <c>Dispose</c> left behind.
///
/// On Windows and Mac Catalyst every command fails with
/// <see cref="PlatformNotSupportedException"/>, and showing that is deliberate: it is what an app
/// targeting every platform sees, and it needs no conditional code to get there.
/// </summary>
public sealed class NfcViewModel : ReactiveObject, IDisposable
{
    private readonly INfc _nfc;
    private readonly DisposableBag _subscriptions = new();

    private string _status = "Idle. Start a session, then hold a tag to the device.";
    private string? _tagId;
    private bool _isSessionEnabled;

    public NfcViewModel(INfc nfc, ISequencer mainThread)
    {
        _nfc = nfc;

        Start = ReactiveCommand.CreateFromTask(
            StartAsync,
            this.WhenAnyValue(vm => vm.IsSessionEnabled, enabled => !enabled));

        Stop = ReactiveCommand.CreateFromTask(
            StopAsync,
            this.WhenAnyValue(vm => vm.IsSessionEnabled));

        new FromEventPatternSignal<EventHandler<NfcTagDetectedEventArgs>, NfcTagDetectedEventArgs>(
                handler => _nfc.TagDetected += handler,
                handler => _nfc.TagDetected -= handler)
            .Select(detected => detected.EventArgs)
            .ObserveOn(mainThread)
            .Subscribe(OnTagDetected)
            .AddTo(_subscriptions);

        new FromEventPatternSignal<EventHandler<EventArgs>, EventArgs>(
                handler => _nfc.SessionTimeout += handler,
                handler => _nfc.SessionTimeout -= handler)
            .ObserveOn(mainThread)
            .Subscribe(_ =>
            {
                IsSessionEnabled = false;
                Status = "The reader session timed out.";
            })
            .AddTo(_subscriptions);

        // Without this a failed command becomes an unobserved exception and the UI just sits there.
        Start.ThrownExceptions
            .Merge(Stop.ThrownExceptions)
            .ObserveOn(mainThread)
            .Subscribe(exception => Status = Describe(exception))
            .AddTo(_subscriptions);
    }

    public ReactiveCommand<RxVoid, RxVoid> Start { get; }

    public ReactiveCommand<RxVoid, RxVoid> Stop { get; }

    public ObservableCollection<string> Records { get; } = [];

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public string? TagId
    {
        get => _tagId;
        private set
        {
            this.RaiseAndSetIfChanged(ref _tagId, value);
            this.RaisePropertyChanged(nameof(HasTag));
        }
    }

    /// <summary>Whether a tag has been read, so the view can hide an empty row rather than show "Tag".</summary>
    public bool HasTag => !string.IsNullOrEmpty(_tagId);

    public bool IsSessionEnabled
    {
        get => _isSessionEnabled;
        private set => this.RaiseAndSetIfChanged(ref _isSessionEnabled, value);
    }

    public void Dispose() => _subscriptions.Dispose();

    private async Task StartAsync()
    {
        await _nfc.EnableSessionAsync();

        IsSessionEnabled = true;
        Status = "Session enabled. Hold a tag to the device.";
    }

    private async Task StopAsync()
    {
        await _nfc.DisableSessionAsync();

        IsSessionEnabled = false;
        Status = "Session disabled.";
    }

    private void OnTagDetected(NfcTagDetectedEventArgs detected)
    {
        TagId = string.IsNullOrEmpty(detected.TagId)
            ? "(this platform reports no tag id)"
            : detected.TagId;

        Records.Clear();
        foreach (var record in detected.NdefMessage)
        {
            Records.Add(Describe(record));
        }

        Status = $"Read {detected.NdefMessage.Count} record(s).";
    }

    private static string Describe(NdefRecord record)
    {
        var type = Encoding.ASCII.GetString(record.Type.Span);
        var payload = record.Payload.Span;

        // Text records start with a status byte and a language code; showing the text is friendlier
        // than showing the bytes, and everything else falls back to hex.
        if (record.TypeNameFormat == NdefTypeNameFormat.NfcWellKnown && type == "T" && payload.Length > 0)
        {
            var languageLength = payload[0] & 0x3F;
            if (payload.Length > 1 + languageLength)
            {
                return $"{record.TypeNameFormat} \"{type}\": "
                    + Encoding.UTF8.GetString(payload[(1 + languageLength)..]);
            }
        }

        return $"{record.TypeNameFormat} \"{type}\": {Convert.ToHexString(payload)}";
    }

    private static string Describe(Exception exception) => exception switch
    {
        PlatformNotSupportedException => "NFC is not supported on this platform.",
        InvalidOperationException => exception.Message,
        _ => $"{exception.GetType().Name}: {exception.Message}",
    };
}
