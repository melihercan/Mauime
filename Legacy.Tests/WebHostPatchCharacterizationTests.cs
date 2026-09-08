using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Legacy.Tests;

/// <summary>
/// What Xamarinme.WebHostPatch 1.0.0 actually does, pinned before anything is ported.
///
/// The library is two forks of Microsoft code. <c>ConsoleLifetimePatch</c> is
/// <c>ConsoleLifetime</c> with the <c>Console.CancelKeyPress</c> subscription commented out,
/// because that call threw on Mono; <c>WebHostExtensionsPatch</c> is <c>WebHostExtensions</c> with
/// the same subscription replaced by a private static event. Everything else about them is
/// Microsoft's.
///
/// Only the lifetime half can be exercised in a test. <c>RunPatchedAsync</c> needs a live
/// <c>IWebHost</c> built on ASP.NET Core 2.2, which is not something to stand up here — that half is
/// covered by the API baseline instead.
/// </summary>
public class WebHostPatchCharacterizationTests
{
    private static ConsoleLifetimePatch Create(
        CancellationTokenSource started,
        CancellationTokenSource stopping,
        CapturingLoggerProvider log,
        bool suppressStatusMessages = false)
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Returns(started.Token);
        lifetime.ApplicationStopping.Returns(stopping.Token);

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        environment.ContentRootPath.Returns("/content");

        return new ConsoleLifetimePatch(
            Options.Create(new ConsoleLifetimeOptions { SuppressStatusMessages = suppressStatusMessages }),
            environment,
            lifetime,
            Options.Create(new HostOptions()),
            LoggerFactory.Create(b => b.AddProvider(log)));
    }

    [Fact]
    public async Task WaitForStartAsync_completes_immediately()
    {
        using var started = new CancellationTokenSource();
        using var stopping = new CancellationTokenSource();
        var log = new CapturingLoggerProvider();

        var lifetime = Create(started, stopping, log);
        try
        {
            var task = lifetime.WaitForStartAsync(CancellationToken.None);

            task.IsCompletedSuccessfully.Should().BeTrue();
            await task;
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    [Fact]
    public async Task Application_start_and_stop_are_logged_under_the_Microsoft_Hosting_Lifetime_category()
    {
        using var started = new CancellationTokenSource();
        using var stopping = new CancellationTokenSource();
        var log = new CapturingLoggerProvider();

        var lifetime = Create(started, stopping, log);
        try
        {
            await lifetime.WaitForStartAsync(CancellationToken.None);

            await started.CancelAsync();
            await stopping.CancelAsync();

            log.Messages.Should().Contain("Application started. Press Ctrl+C to shut down.");
            log.Messages.Should().Contain("Hosting environment: Production");
            log.Messages.Should().Contain("Content root path: /content");
            log.Messages.Should().Contain("Application is shutting down...");
            log.Categories.Should().OnlyContain(c => c == "Microsoft.Hosting.Lifetime");
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    [Fact]
    public async Task Suppressing_status_messages_unregisters_the_lifetime_callbacks_entirely()
    {
        using var started = new CancellationTokenSource();
        using var stopping = new CancellationTokenSource();
        var log = new CapturingLoggerProvider();

        var lifetime = Create(started, stopping, log, suppressStatusMessages: true);
        try
        {
            await lifetime.WaitForStartAsync(CancellationToken.None);

            await started.CancelAsync();
            await stopping.CancelAsync();

            log.Messages.Should().BeEmpty();
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    [Fact]
    public async Task StopAsync_completes_immediately_and_does_nothing()
    {
        using var started = new CancellationTokenSource();
        using var stopping = new CancellationTokenSource();
        var log = new CapturingLoggerProvider();

        var lifetime = Create(started, stopping, log);
        try
        {
            await lifetime.StopAsync(CancellationToken.None);

            log.Messages.Should().BeEmpty();
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        using var started = new CancellationTokenSource();
        using var stopping = new CancellationTokenSource();

        var lifetime = Create(started, stopping, new CapturingLoggerProvider());

        lifetime.Dispose();

        var again = () => lifetime.Dispose();
        again.Should().NotThrow();
    }

    [Fact]
    public void A_null_logger_factory_is_not_accepted_even_though_the_convenience_constructor_supplies_one()
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var environment = Substitute.For<IHostEnvironment>();

        var create = () => new ConsoleLifetimePatch(
            Options.Create(new ConsoleLifetimeOptions()),
            environment,
            lifetime,
            Options.Create(new HostOptions()),
            null!);

        create.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void The_four_argument_constructor_logs_nowhere()
    {
        using var started = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Returns(started.Token);
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        environment.ContentRootPath.Returns("/content");

        var subject = new ConsoleLifetimePatch(
            Options.Create(new ConsoleLifetimeOptions()),
            environment,
            lifetime,
            Options.Create(new HostOptions()));

        try
        {
            // NullLoggerFactory, so firing the started callback must not throw.
            subject.WaitForStartAsync(CancellationToken.None).IsCompletedSuccessfully.Should().BeTrue();
            started.Cancel();
        }
        finally
        {
            subject.Dispose();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("environment")]
    [InlineData("applicationLifetime")]
    public void The_constructor_rejects_its_null_arguments_by_name(string? _)
    {
        // Options and hostOptions are guarded too; all five guards are Microsoft's, unchanged.
        var create = () => new ConsoleLifetimePatch(null!, null!, null!, null!);

        create.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }
}

/// <summary>Captures log messages so the lifetime's status output can be asserted on.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<(string Category, string Message)> _entries = [];

    public IReadOnlyList<string> Messages
    {
        get { lock (_entries) return _entries.Select(e => e.Message).ToList(); }
    }

    public IReadOnlyList<string> Categories
    {
        get { lock (_entries) return _entries.Select(e => e.Category).ToList(); }
    }

    public ILogger CreateLogger(string categoryName) => new Capturing(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(string category, string message)
    {
        lock (_entries) _entries.Add((category, message));
    }

    private sealed class Capturing(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            owner.Add(category, formatter(state, exception));
    }
}
