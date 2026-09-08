using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Mauime.WebHostPatch;

/// <inheritdoc cref="IMauimeWebHost"/>
internal sealed class MauimeWebHost : IMauimeWebHost
{
    private readonly MauimeWebHostOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private WebApplication? _application;

    internal MauimeWebHost(MauimeWebHostOptions options) => _options = options;

    public Uri? Address { get; private set; }

    public bool IsRunning => _application is not null;

    public async Task<Uri> StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_application is not null)
            {
                throw new InvalidOperationException("The web host is already running.");
            }

            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ContentRootPath = _options.ContentRootPath,
            });

            builder.WebHost.ConfigureKestrel(kestrel =>
            {
                if (_options.ListenOnAllInterfaces)
                {
                    kestrel.ListenAnyIP(_options.Port);
                }
                else
                {
                    // Not ListenLocalhost: it binds both loopback addresses at once and Kestrel
                    // refuses a dynamic port for that, since the two would get different ones.
                    kestrel.Listen(IPAddress.Loopback, _options.Port);
                }
            });

            _options.ConfigureBuilder?.Invoke(builder);

            var application = builder.Build();
            _options.ConfigureApplication?.Invoke(application);

            await application.StartAsync(cancellationToken).ConfigureAwait(false);

            _application = application;
            Address = ResolveAddress(application);

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
            if (_application is null)
            {
                return;
            }

            var application = _application;
            _application = null;
            Address = null;

            await application.StopAsync(cancellationToken).ConfigureAwait(false);
            await application.DisposeAsync().ConfigureAwait(false);
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
    /// from the options, so a <see cref="MauimeWebHostOptions.Port"/> of 0 reports the port the
    /// operating system picked.
    /// </summary>
    private Uri ResolveAddress(WebApplication application)
    {
        var addresses = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;

        var bound = addresses?.FirstOrDefault()
            ?? throw new InvalidOperationException("Kestrel started but reported no address.");

        var uri = new Uri(bound);

        if (!_options.ListenOnAllInterfaces)
        {
            return uri;
        }

        // Listening on every interface is reported as 0.0.0.0 or [::], which is not something to
        // put in front of a user or type into another device. Substitute a reachable address when
        // there is one.
        var local = NetworkAddress.GetLocalAddress();

        return local is null
            ? uri
            : new UriBuilder(uri) { Host = local.ToString() }.Uri;
    }
}
