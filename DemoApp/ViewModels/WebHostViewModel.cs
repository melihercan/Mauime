using Mauime.WebHostPatch;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;

namespace DemoApp.ViewModels;

/// <summary>
/// Starts and stops a Kestrel server inside the app, and shows the address to reach it at.
///
/// This is the whole of what Xamarinme.WebHostPatch needed two forks of Microsoft's code to do.
/// Open the address from another device on the same network to see it answer.
/// </summary>
public sealed class WebHostViewModel : ReactiveObject, IDisposable
{
    private readonly IMauimeWebHost _host;
    private readonly DisposableBag _subscriptions = new();

    private string _status = "Stopped.";
    private string? _address;
    private bool _isRunning;

    public WebHostViewModel(IMauimeWebHost host, ISequencer mainThread)
    {
        _host = host;

        Start = ReactiveCommand.CreateFromTask(
            StartAsync,
            this.WhenAnyValue(vm => vm.IsRunning, running => !running));

        Stop = ReactiveCommand.CreateFromTask(
            StopAsync,
            this.WhenAnyValue(vm => vm.IsRunning));

        Start.ThrownExceptions
            .Merge(Stop.ThrownExceptions)
            .ObserveOn(mainThread)
            .Subscribe(exception => Status = $"{exception.GetType().Name}: {exception.Message}")
            .AddTo(_subscriptions);
    }

    public ReactiveCommand<RxVoid, RxVoid> Start { get; }

    public ReactiveCommand<RxVoid, RxVoid> Stop { get; }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    /// <summary>
    /// Where the server is listening. Comes back from the server after it binds, so a configured
    /// port of 0 shows the port the operating system chose, and listening on every interface shows
    /// the machine's address rather than 0.0.0.0.
    /// </summary>
    public string? Address
    {
        get => _address;
        private set => this.RaiseAndSetIfChanged(ref _address, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public void Dispose() => _subscriptions.Dispose();

    private async Task StartAsync()
    {
        Status = "Starting...";

        var address = await _host.StartAsync();

        Address = address.ToString();
        IsRunning = true;
        Status = "Running. Open the address from another device on this network.";
    }

    private async Task StopAsync()
    {
        await _host.StopAsync();

        Address = null;
        IsRunning = false;
        Status = "Stopped.";
    }
}
