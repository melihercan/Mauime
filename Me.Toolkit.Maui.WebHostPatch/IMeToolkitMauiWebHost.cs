namespace Me.Toolkit.Maui.WebHostPatch;

/// <summary>
/// An ASP.NET Core web host running inside the MAUI app, on Kestrel.
/// </summary>
/// <remarks>
/// <para>
/// The approach is Xamarinme.WebHostPatch's, because it is the only one that works: ASP.NET Core as
/// <c>netstandard2.0</c> packages, which are copied into the app like any assembly. The
/// <c>Microsoft.AspNetCore.App</c> *framework* has no runtime pack for android, ios or maccatalyst,
/// so a framework reference compiles a library and then fails the consuming app.
/// </para>
/// <para>
/// What is gone is the patching. Xamarinme needed two forks of Microsoft's code:
/// <c>Microsoft.Net.Http.Headers</c> 2.2.0 called an <c>InplaceStringBuilder</c> that
/// <c>Microsoft.Extensions.Primitives</c> 5.0 had deleted, so a fork of Primitives stamped 5.9.0.0
/// was shipped to win at bind time; and <c>Console.CancelKeyPress</c> threw on Mono, so
/// <c>ConsoleLifetime</c> and <c>WebHostExtensions.RunAsync</c> were reimplemented around it.
/// </para>
/// <para>
/// The first is fixed upstream — 2.3.11 no longer references that type. The second is avoided by
/// construction: this starts and stops the host explicitly rather than calling <c>RunAsync</c>,
/// which is where the <c>CancelKeyPress</c> subscription lives, and uses the web host rather than
/// the generic host, so <c>ConsoleLifetime</c> is never involved. Blocking until shutdown is not
/// what an app wants anyway.
/// </para>
/// </remarks>
public interface IMeToolkitMauiWebHost : IAsyncDisposable
{
    /// <summary>
    /// Where the server is listening, or <see langword="null"/> when it is not running.
    /// </summary>
    /// <remarks>
    /// Resolved from the server after it binds, so it carries the real port even when
    /// <see cref="MeToolkitMauiWebHostOptions.Port"/> was left at 0 for the operating system to choose.
    /// </remarks>
    Uri? Address { get; }

    /// <summary>Whether the server is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Starts the server.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The address the server bound to.</returns>
    /// <exception cref="InvalidOperationException">The server is already running.</exception>
    Task<Uri> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the server. Does nothing when it is not running.</summary>
    /// <param name="cancellationToken">Cancels the graceful shutdown.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
