using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Mauime.WebHostPatch;

/// <summary>Registers an ASP.NET Core web host on a <see cref="MauiAppBuilder"/>.</summary>
public static class MauimeWebHostExtensions
{
    /// <summary>
    /// Registers <see cref="IMauimeWebHost"/> as a singleton. The server is not started; call
    /// <see cref="IMauimeWebHost.StartAsync"/> when the app is ready for it.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="configure">Configures the host. Optional; the defaults bind every interface on an operating-system-chosen port.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Starting is left to the caller rather than done here, because a phone app that opens a
    /// listening socket the moment it launches is rarely what anyone wants. Xamarinme's demo started
    /// its host from the app constructor and had no way to stop it.
    /// </remarks>
    public static MauiAppBuilder UseMauimeWebHost(
        this MauiAppBuilder builder,
        Action<MauimeWebHostOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new MauimeWebHostOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton<IMauimeWebHost>(_ => new MauimeWebHost(options));

        return builder;
    }
}
