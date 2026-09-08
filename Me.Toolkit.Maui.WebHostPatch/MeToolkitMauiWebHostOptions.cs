using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Me.Toolkit.Maui.WebHostPatch;

/// <summary>How <see cref="IMeToolkitMauiWebHost"/> should build and bind its server.</summary>
public sealed class MeToolkitMauiWebHostOptions
{
    /// <summary>
    /// The TCP port to listen on. 0, the default, lets the operating system choose one;
    /// <see cref="IMeToolkitMauiWebHost.Address"/> reports which.
    /// </summary>
    /// <remarks>Ports below 1024 are not available to an app on Android or iOS.</remarks>
    public int Port { get; set; }

    /// <summary>
    /// Whether to listen on every interface rather than loopback only. Default
    /// <see langword="true"/>, because serving something to another device on the network is the
    /// reason to run a web host inside a phone app.
    /// </summary>
    public bool ListenOnAllInterfaces { get; set; } = true;

    /// <summary>
    /// The content root. Defaults to <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Set explicitly because ASP.NET Core reads it from configuration and there is none here, so
    /// it would otherwise be null and throw further in. That was one of the four problems
    /// Xamarinme.WebHostPatch had to work around; supplying the value is the whole fix.
    /// </remarks>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>Configures the host builder before it is built — services, logging, Kestrel options.</summary>
    public Action<IWebHostBuilder>? ConfigureBuilder { get; set; }

    /// <summary>Configures the request pipeline.</summary>
    public Action<IApplicationBuilder>? ConfigureApplication { get; set; }
}
