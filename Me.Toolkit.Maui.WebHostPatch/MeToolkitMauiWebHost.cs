using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Me.Toolkit.Maui.WebHostPatch;

/// <inheritdoc cref="IMeToolkitMauiWebHost"/>
internal sealed class MeToolkitMauiWebHost : IMeToolkitMauiWebHost
{
    private readonly MeToolkitMauiWebHostOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IWebHost? _host;

    internal MeToolkitMauiWebHost(MeToolkitMauiWebHostOptions options) => _options = options;

    public Uri? Address { get; private set; }

    public bool IsRunning => _host is not null;

    public async Task<Uri> StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_host is not null)
            {
                throw new InvalidOperationException("The web host is already running.");
            }

            var builder = new WebHostBuilder()
                .UseKestrel(kestrel => kestrel.Listen(
                    _options.ListenOnAllInterfaces ? IPAddress.Any : IPAddress.Loopback,
                    _options.Port))
                // Not read from configuration, because there is none: leaving it unset is what made
                // ASP.NET Core throw on Xamarin.
                .UseContentRoot(_options.ContentRootPath)
                .Configure(app => _options.ConfigureApplication?.Invoke(app));

            _options.ConfigureBuilder?.Invoke(builder);

            var host = builder.Build();

            // StartAsync, never RunAsync. RunAsync subscribes to Console.CancelKeyPress, which is
            // what Xamarinme.WebHostPatch forked WebHostExtensions to avoid — the call is still
            // there in 2.3.11. Starting and stopping explicitly never enters that code path, which
            // is also what an app wants: RunAsync blocks until shutdown.
            //
            // The generic host's ConsoleLifetime, the other half of that fork, is not involved
            // either: this is the web host, not Host.CreateDefaultBuilder.
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            _host = host;
            Address = ResolveAddress(host);

            return Address;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_host is null)
            {
                return;
            }

            var host = _host;
            _host = null;
            Address = null;

            await host.StopAsync(cancellationToken).ConfigureAwait(false);
            host.Dispose();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    /// <summary>
    /// The address the server actually bound to, read from the server's own feature rather than
    /// from the options, so a <see cref="MeToolkitMauiWebHostOptions.Port"/> of 0 reports the port the
    /// operating system picked.
    /// </summary>
    private Uri ResolveAddress(IWebHost host)
    {
        var bound = host.ServerFeatures.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("Kestrel started but reported no address.");

        var uri = new Uri(bound);

        if (!_options.ListenOnAllInterfaces)
        {
            return uri;
        }

        // Listening on every interface is reported as 0.0.0.0, which is not something to put in
        // front of a user or type into another device. Substitute a reachable address when there
        // is one.
        var local = NetworkAddress.GetLocalAddress();

        return local is null
            ? uri
            : new UriBuilder(uri) { Host = local.ToString() }.Uri;
    }
}
