namespace Mauime.WebHostPatch;

/// <summary>
/// An ASP.NET Core web host running inside the MAUI app, on Kestrel.
/// </summary>
/// <remarks>
/// <para>
/// Xamarinme.WebHostPatch was two forks of Microsoft's code, because ASP.NET Core 2.2 on Mono needed
/// them: <c>Console.CancelKeyPress</c> threw, so <c>ConsoleLifetime</c> and
/// <c>WebHostExtensions.RunAsync</c> were reimplemented around it, and
/// <c>Microsoft.Net.Http.Headers</c> 2.2.0 called an <c>InplaceStringBuilder</c> that
/// <c>Microsoft.Extensions.Primitives</c> 5.0 had deleted, so a fork of Primitives stamped 5.9.0.0
/// was shipped to win at bind time.
/// </para>
/// <para>
/// Neither cause survives on .NET 10, and neither fork came across. Running Kestrel inside a MAUI
/// app now needs a <c>Microsoft.AspNetCore.App</c> framework reference and nothing else — no
/// packages, no patches. What is left is the part that was never the patch: starting and stopping
/// the thing from app code, and knowing what address to show the user.
/// </para>
/// </remarks>
public interface IMauimeWebHost : IAsyncDisposable
{
    /// <summary>
    /// Where the server is listening, or <see langword="null"/> when it is not running.
    /// </summary>
    /// <remarks>
    /// Resolved from the server after it binds, so it carries the real port even when
    /// <see cref="MauimeWebHostOptions.Port"/> was left at 0 for the operating system to choose.
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
