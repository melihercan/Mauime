using Microsoft.AspNetCore.Builder;

namespace Mauime.WebHostPatch;

/// <summary>How <see cref="IMauimeWebHost"/> should build and bind its server.</summary>
public sealed class MauimeWebHostOptions
{
    /// <summary>
    /// The TCP port to listen on. 0, the default, lets the operating system choose one;
    /// <see cref="IMauimeWebHost.Address"/> reports which.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Whether to listen on every interface rather than loopback only. Default
    /// <see langword="true"/>, because serving something to another device on the network is the
    /// reason to run a web host inside a phone app.
    /// </summary>
    public bool ListenOnAllInterfaces { get; set; } = true;

    /// <summary>
    /// The content root. Defaults to <see cref="AppContext.BaseDirectory"/> rather than the current
    /// directory, which on a mobile platform is not somewhere the app can rely on.
    /// </summary>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>Configures the builder before it is built — services, configuration, logging.</summary>
    public Action<WebApplicationBuilder>? ConfigureBuilder { get; set; }

    /// <summary>Configures the application before it starts — middleware and endpoints.</summary>
    public Action<WebApplication>? ConfigureApplication { get; set; }
}
