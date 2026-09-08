using ReactiveUI;

namespace DemoApp.ViewModels;

/// <summary>One tab's view model each, so the page can bind each tab to its own.</summary>
public sealed class MainViewModel(
    ConfigurationViewModel configuration,
    HostingViewModel hosting,
    NfcViewModel nfc,
    WebHostViewModel webHost) : ReactiveObject
{
    public ConfigurationViewModel Configuration { get; } = configuration;

    public HostingViewModel Hosting { get; } = hosting;

    public NfcViewModel Nfc { get; } = nfc;

    public WebHostViewModel WebHost { get; } = webHost;
}
