using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace DemoApp.ViewModels;

/// <summary>
/// What Mauime.Hosting actually changes: without it, <c>IHostEnvironment</c> reports
/// <c>Production</c> on every platform, always.
///
/// The constructor is also the demonstration — <see cref="ILogger{T}"/>,
/// <see cref="IHostEnvironment"/> and <see cref="IConfiguration"/> are injected by the container
/// MAUI already has. Xamarinme.Hosting existed to provide that container.
/// </summary>
public sealed class HostingViewModel : ReactiveObject
{
    public HostingViewModel(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<HostingViewModel> logger)
    {
        EnvironmentName = environment.EnvironmentName;
        IsDevelopment = environment.IsDevelopment();
        ApplicationName = Safe(() => environment.ApplicationName);
        ContentRootPath = Safe(() => environment.ContentRootPath);

        Greeting = configuration["Demo:Greeting"] ?? "(not set)";
        Source = configuration["Demo:Source"] ?? "(not set)";

        logger.LogInformation("Hosting demo resolved environment {Environment}", EnvironmentName);
    }

    public string EnvironmentName { get; }

    public bool IsDevelopment { get; }

    /// <summary>Comes from the platform, not from Mauime — the wrapper only changes the name.</summary>
    public string ApplicationName { get; }

    /// <summary>Also from the platform.</summary>
    public string ContentRootPath { get; }

    public string Greeting { get; }

    public string Source { get; }

    /// <summary>
    /// MAUI's own host environment throws from these on some slices — the reference-assembly stub
    /// has no application name. A demo should show that rather than fall over on it.
    /// </summary>
    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception)
        {
            return $"(unavailable: {exception.GetType().Name})";
        }
    }
}
