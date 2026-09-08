using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Hosting;

namespace Mauime.Hosting;

/// <summary>
/// Sets the host environment name of a .NET MAUI app, so <c>IsDevelopment()</c> and friends mean
/// something.
/// </summary>
/// <remarks>
/// <para>
/// This is all that is left of Xamarinme.Hosting. That library existed because Xamarin had no
/// hosting at all: it built a parallel <c>XamarinHostBuilder</c> exposing Configuration, Services,
/// Logging and an environment, and an <c>IHost</c> that threw <see cref="NotImplementedException"/>
/// from both <c>StartAsync</c> and <c>StopAsync</c>. <c>MauiAppBuilder</c> is that builder, and MAUI
/// registers an <see cref="IHostEnvironment"/> of its own.
/// </para>
/// <para>
/// What MAUI does not do is let you change the environment name: <c>MauiHostEnvironment</c> reports
/// <c>Production</c>, always. Xamarin had no usable environment variables either, which is why
/// Xamarinme read a <c>XAMARIN_ENVIRONMENT</c> entry out of appsettings.json; the same trick works
/// here, under the name <see cref="DefaultConfigurationKey"/>.
/// </para>
/// </remarks>
public static class MauimeHostingExtensions
{
    /// <summary>
    /// The configuration key <see cref="UseMauimeHostingFromConfiguration"/> reads by default.
    /// Xamarinme.Hosting called it <c>XAMARIN_ENVIRONMENT</c>.
    /// </summary>
    public const string DefaultConfigurationKey = "MAUI_ENVIRONMENT";

    /// <summary>Sets the host environment name to <paramref name="environmentName"/>.</summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="environmentName">
    /// The environment name, such as <c>Development</c>. <see cref="Environments"/> holds the
    /// conventional ones.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static MauiAppBuilder UseMauimeHosting(this MauiAppBuilder builder, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(environmentName);

        return ReplaceHostEnvironment(builder, _ => environmentName);
    }

    /// <summary>
    /// Sets the host environment name from configuration, leaving MAUI's <c>Production</c> default
    /// in place when the key is absent or empty.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="key">The configuration key to read. Defaults to <see cref="DefaultConfigurationKey"/>.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// The value is read when <see cref="IHostEnvironment"/> is first resolved, not when this is
    /// called, so it does not matter whether the configuration sources were added before or after.
    /// </remarks>
    public static MauiAppBuilder UseMauimeHostingFromConfiguration(
        this MauiAppBuilder builder,
        string key = DefaultConfigurationKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var configuration = builder.Configuration;

        return ReplaceHostEnvironment(builder, _ => configuration[key]);
    }

    private static MauiAppBuilder ReplaceHostEnvironment(
        MauiAppBuilder builder,
        Func<IServiceProvider, string?> environmentName)
    {
        var existing = builder.Services.LastOrDefault(d => d.ServiceType == typeof(IHostEnvironment));

        if (existing is not null)
        {
            builder.Services.Remove(existing);
        }

        builder.Services.AddSingleton<IHostEnvironment>(services =>
        {
            var inner = Materialize(existing, services);
            var name = environmentName(services);

            return new MauimeHostEnvironment(inner, name);
        });

        return builder;
    }

    private static IHostEnvironment? Materialize(ServiceDescriptor? descriptor, IServiceProvider services)
    {
        if (descriptor is null)
        {
            return null;
        }

        if (descriptor.ImplementationInstance is IHostEnvironment instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(services) as IHostEnvironment;
        }

        return ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType!) as IHostEnvironment;
    }

    /// <summary>
    /// Wraps the platform's host environment and reports a different name.
    /// </summary>
    /// <remarks>
    /// Wrapping rather than mutating, because <c>MauiHostEnvironment.EnvironmentName</c> **throws
    /// <see cref="NotImplementedException"/> from its setter**. The interface declares the property
    /// as settable and MAUI's type reports <c>CanWrite</c>, so nothing short of calling it says so —
    /// which is exactly how it was found.
    ///
    /// Everything else is delegated, so <c>ApplicationName</c> and <c>ContentRootPath</c> keep
    /// coming from the platform, including their failure modes: on the platform-neutral net10.0
    /// slice MAUI's own <c>ApplicationName</c> throws, and that is faithfully passed on rather than
    /// papered over with a fabricated value.
    /// </remarks>
    private sealed class MauimeHostEnvironment(IHostEnvironment? inner, string? environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            string.IsNullOrEmpty(environmentName)
                ? inner?.EnvironmentName ?? Environments.Production
                : environmentName;

        public string ApplicationName
        {
            get => inner?.ApplicationName ?? string.Empty;
            set => throw new NotSupportedException("The application name comes from the platform.");
        }

        public string ContentRootPath
        {
            get => inner?.ContentRootPath ?? AppContext.BaseDirectory;
            set => throw new NotSupportedException("The content root comes from the platform.");
        }

        public IFileProvider ContentRootFileProvider
        {
            get => inner?.ContentRootFileProvider ?? new NullFileProvider();
            set => throw new NotSupportedException("The content root comes from the platform.");
        }
    }
}
