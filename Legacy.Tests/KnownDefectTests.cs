using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xamarinme;
using Xunit;

namespace Legacy.Tests;

/// <summary>
/// One test per defect found in the Xamarinme code, pinned as it behaves today.
///
/// Rewrite these in place as each defect is fixed — never delete one — so a fix shows up as a diff
/// rather than as silence. A fixed pin is renamed <c>FIXED_</c> and its comment names the pin it
/// replaced, as in Blazorme.
///
/// Prefixes carry the status:
/// <list type="bullet">
/// <item><c>DEFECT_</c> — pinned as broken, waiting to be fixed.</item>
/// <item><c>FIXED_</c> — was a defect pin, rewritten when fixed.</item>
/// <item><c>LIMITATION_</c> — not a bug in this code.</item>
/// </list>
///
/// Six of these assert against files under <c>legacy/</c> rather than running code, and say why in
/// place. Two are about csproj declarations, which is where those defects live. The other four
/// cannot be reached from a test: legacy/Nfc does not build at all on this toolchain, and two of the
/// WebHostPatch defects are Mono-specific or would hang the run rather than fail it.
/// </summary>
public class KnownDefectTests
{
    // ---- Xamarinme.Configuration -------------------------------------------------------------

    [Fact]
    public void DEFECT_Configuration_options_are_never_validated()
    {
        // AddEmbeddedResource takes the options, hands them to the source, and the provider
        // dereferences them in Load(). Nothing checks anything, so a caller who forgets the options
        // gets a NullReferenceException from inside a Build() call with no indication of which
        // argument was wrong.
        var build = () => new ConfigurationBuilder()
            .AddEmbeddedResource(null!)
            .Build();

        build.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void DEFECT_Configuration_a_missing_assembly_is_a_NullReferenceException_too()
    {
        var build = () => new ConfigurationBuilder()
            .AddEmbeddedResource(new EmbeddedResourceConfigurationOptions { Prefix = "Whatever" })
            .Build();

        build.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void DEFECT_Configuration_a_missing_prefix_silently_loads_nothing()
    {
        // Worse than the two above, because it does not fail: the provider looks for a resource
        // named ".appsettings.json", finds none, and returns an empty configuration. Every lookup
        // then reads null and the app runs on defaults it never chose.
        var configuration = new ConfigurationBuilder()
            .AddEmbeddedResource(new EmbeddedResourceConfigurationOptions
            {
                Assembly = typeof(KnownDefectTests).Assembly,
            })
            .Build();

        configuration.AsEnumerable().Should().BeEmpty();
    }

    // ---- Xamarinme.Hosting --------------------------------------------------------------------

    [Fact]
    public async Task DEFECT_XamarinHost_claims_IHost_but_throws_from_StartAsync()
    {
        // XamarinHost implements IHost so that Build() can return one, then throws from both halves
        // of the interface's contract. Anything written against IHost — including
        // Microsoft's own RunAsync/StartAsync extension methods — fails on it.
        var host = Build("Legacy.Tests.TestAssets.EnvMissing");

        var start = async () => await host.StartAsync();

        await start.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task DEFECT_XamarinHost_throws_from_StopAsync_as_well()
    {
        var host = Build("Legacy.Tests.TestAssets.EnvMissing");

        var stop = async () => await host.StopAsync();

        await stop.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public void DEFECT_XamarinHost_Dispose_leaves_its_service_provider_undisposed()
    {
        // XamarinHost.Dispose has an empty body. The ServiceProvider it owns is never disposed, so
        // neither is anything singleton and IDisposable inside it.
        var builder = XamarinHostBuilder.CreateDefault(Options("Legacy.Tests.TestAssets.EnvMissing"));
        builder.Services.AddSingleton<DisposableService>();

        var host = builder.Build();
        var service = host.Services.GetRequiredService<DisposableService>();

        host.Dispose();

        service.Disposed.Should().BeFalse();
    }

    [Fact]
    public void DEFECT_Hosting_swallows_every_error_while_reading_the_environment()
    {
        // The GetProperty/GetString pair sits in a try with an empty catch. A XAMARIN_ENVIRONMENT
        // that is not a string throws InvalidOperationException, which is discarded, and the app
        // silently runs as Production. The same catch hides a genuinely absent key, so a typo in
        // the setting name is indistinguishable from not setting it.
        XamarinHostBuilder.CreateDefault(Options("Legacy.Tests.TestAssets.EnvNumeric"))
            .HostEnvironment.Environment.Should().Be("Production");
    }

    [Fact]
    public void DEFECT_Hosting_turns_a_json_null_environment_into_a_null_environment()
    {
        // GetString() returns null for a JSON null rather than throwing, so the assignment
        // succeeds and the "Production" default is overwritten with null. IXamarinHostEnvironment
        // then reports null, and the configuration source goes looking for
        // "{Prefix}.appsettings..json".
        var builder = XamarinHostBuilder.CreateDefault(Options("Legacy.Tests.TestAssets.EnvNull"));

        builder.HostEnvironment.Environment.Should().BeNull();
        builder.Configuration["Build"].Should().Be("Base");
    }

    [Fact]
    public void DEFECT_Hosting_lets_a_malformed_appsettings_escape_the_catch_it_wrote()
    {
        // JsonDocument.Parse is called one line *above* the try, so the empty catch that hides
        // every other failure does not cover the one failure a user is most likely to cause.
        var create = () => XamarinHostBuilder.CreateDefault(Options("Legacy.Tests.TestAssets.Malformed"));

        create.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void DEFECT_Build_registers_IConfiguration_but_not_IConfigurationRoot()
    {
        // The configuration object implements IConfigurationRoot, and Reload() is only reachable
        // through it, but only IConfiguration is registered. Injecting IConfigurationRoot fails.
        var host = Build("Legacy.Tests.TestAssets.EnvMissing");

        host.Services.GetService<IConfiguration>().Should().NotBeNull();
        host.Services.GetService<IConfigurationRoot>().Should().BeNull();
    }

    [Fact]
    public void DEFECT_Build_can_be_called_twice_and_registers_the_configuration_again()
    {
        // Build() mutates the shared ServiceCollection instead of working from a copy, so a second
        // call adds a second IConfiguration registration and builds a second provider over it.
        var builder = XamarinHostBuilder.CreateDefault(Options("Legacy.Tests.TestAssets.EnvMissing"));

        builder.Build();
        var second = builder.Build();

        second.Services.GetServices<IConfiguration>().Should().HaveCount(2);
    }

    [Fact]
    public void DEFECT_XamarinHost_is_exported_from_a_namespace_no_consumer_should_see()
    {
        // XamarinHost lives in XamarinmeHosting — the RootNamespace, used everywhere else in the
        // library for internals — yet it is public, so it shows up in the package's surface next to
        // the Xamarinme types. Its only constructor is internal, so nobody outside can build one;
        // it is exported for no reason. Recorded because the port is free to fix it.
        var type = typeof(XamarinmeHosting.XamarinHost);

        type.IsPublic.Should().BeTrue();
        type.Namespace.Should().Be("XamarinmeHosting");
        type.GetConstructors().Should().BeEmpty("the only constructor is internal");
    }

    // ---- Xamarinme.WebHostPatch ---------------------------------------------------------------

    [Fact]
    public void DEFECT_ShutdownPatched_ignores_the_host_it_is_called_on()
    {
        // ShutdownPatched is an extension method on IWebHost that never touches its own parameter:
        // it raises a private *static* event instead. Shutdown is therefore process-global, every
        // host in the process is torn down together, and calling it on a host that was never run
        // succeeds while doing nothing at all.
        var host = Substitute.For<IWebHost>();

        var shutdown = () => host.ShutdownPatched();

        shutdown.Should().NotThrow();
        host.ReceivedCalls().Should().BeEmpty();

        var onNothing = () => ((IWebHost)null!).ShutdownPatched();
        onNothing.Should().NotThrow();
    }

    [Fact]
    public void DEFECT_ConsoleLifetimePatch_Dispose_touches_the_very_API_the_patch_exists_to_avoid()
    {
        // WaitForStartAsync has the Console.CancelKeyPress subscription commented out, with a note
        // saying the call throws on Xamarin. Dispose still unsubscribes it — an unsubscription that
        // can never match a subscription, on the API the whole fork was written to stay away from.
        //
        // Read as source rather than run, because the throw is Mono-specific: on Windows both calls
        // are harmless, so no assertion here could tell the difference.
        var source = LegacySource.Read("WebHostPatch", "ConsoleLifetimePatch.cs");

        source.Should().Contain("////Console.CancelKeyPress += OnCancelKeyPress;",
            "the subscription is commented out because it throws on Xamarin");

        Lines(source).Should().Contain("Console.CancelKeyPress -= OnCancelKeyPress;",
            "but the matching unsubscription in Dispose was left live");
    }

    [Fact]
    public void DEFECT_ConsoleLifetimePatch_waits_forever_at_process_exit()
    {
        // OnProcessExit waits for the shutdown block with a timeout, logs that it timed out, and
        // then waits for the same block again with no timeout at all. A host that fails to dispose
        // hangs the process on the way out instead of the timeout doing anything.
        //
        // Read as source rather than run: exercising it means letting a real ProcessExit handler
        // block, which would hang this test run rather than fail it.
        var lines = Lines(LegacySource.Read("WebHostPatch", "ConsoleLifetimePatch.cs"));

        var timed = lines.IndexOf("if (!_shutdownBlock.WaitOne(HostOptions.ShutdownTimeout))");
        var untimed = lines.IndexOf("_shutdownBlock.WaitOne();");

        timed.Should().BeGreaterThan(-1);
        untimed.Should().BeGreaterThan(timed, "the unbounded wait follows the timed one");
    }

    [Fact]
    public void DEFECT_WebHostPatch_ships_a_fork_of_Microsoft_Extensions_Primitives_that_shadows_the_real_one()
    {
        // The vendored project sets AssemblyName Microsoft.Extensions.Primitives and Version 5.9.0.0
        // purely so it outranks the real 5.0 assembly at bind time, and WebHostPatch.csproj copies
        // it into the package output. It exists to restore InplaceStringBuilder, which
        // Microsoft.Net.Http.Headers 2.2.0 needs and Primitives 5.0 removed.
        var csproj = LegacySource.Read(Path.Combine("Microsoft.Extensions.Primitives.Patch", "src"),
            "Microsoft.Extensions.Primitives.Patch.csproj");

        csproj.Should().Contain("<AssemblyName>Microsoft.Extensions.Primitives</AssemblyName>");
        csproj.Should().Contain("<Version>5.9.0.0</Version>");

        // And the shadowing is not theoretical: merely referencing WebHostPatch replaces the real
        // Primitives in this test project's own output, so these tests are themselves running on
        // the fork. Any consumer of Xamarinme.WebHostPatch gets the same substitution.
        typeof(Microsoft.Extensions.Primitives.StringSegment).Assembly
            .GetName().Version.Should().Be(new Version(5, 9, 0, 0));
    }

    [Fact]
    public void DEFECT_WebHostPatch_pulls_in_a_Kestrel_with_a_critical_advisory()
    {
        // Microsoft.AspNetCore 2.2.0 and Microsoft.AspNetCore.Server.Kestrel 2.2.0 are pinned, and
        // restoring them today reports NU1904 (critical, GHSA-5rrx-jjjq-q2r5) for
        // Microsoft.AspNetCore.Server.Kestrel.Core and NU1902 (moderate, GHSA-prrf-397v-83xh) for
        // Microsoft.AspNetCore.Server.IIS. Xamarinme.WebHostPatch 1.0.0 is on nuget.org, so every
        // consumer inherits both. This is why the repository cannot build under -warnaserror until
        // legacy/ is gone.
        var csproj = LegacySource.Read("WebHostPatch", "WebHostPatch.csproj");

        csproj.Should().Contain("""<PackageReference Include="Microsoft.AspNetCore" Version="2.2.0" />""");
        csproj.Should().Contain("""<PackageReference Include="Microsoft.AspNetCore.Server.Kestrel" Version="2.2.0" />""");
    }

    // ---- Xamarinme.Nfc ------------------------------------------------------------------------
    //
    // Its two pins, DEFECT_Nfc_netstandard_slice_does_not_compile and
    // DEFECT_Nfc_Pcsc_carries_a_dead_if_false_block_with_a_second_constructor, were rewritten as
    // FIXED_ in Mauime.Tests/KnownDefectTests.cs when the library was ported, and legacy/Nfc was
    // deleted in the same commit. They are named here so the move is findable from the side the
    // defect was pinned on.

    // ---- helpers ------------------------------------------------------------------------------

    private static EmbeddedResourceConfigurationOptions Options(string prefix) => new()
    {
        Assembly = typeof(KnownDefectTests).Assembly,
        Prefix = prefix,
    };

    private static Microsoft.Extensions.Hosting.IHost Build(string prefix) =>
        XamarinHostBuilder.CreateDefault(Options(prefix)).Build();

    private static List<string> Lines(string source) =>
        source.Split('\n').Select(l => l.Trim()).ToList();

    private sealed class DisposableService : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}

/// <summary>Reads a file out of <c>legacy/</c> for the pins that cannot be behavioural.</summary>
internal static class LegacySource
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    internal static string Read(string projectDirectory, string fileName)
    {
        var path = Path.Combine(RepositoryRoot, "legacy", projectDirectory, fileName);

        return File.Exists(path)
            ? File.ReadAllText(path)
            : throw new FileNotFoundException($"Legacy source '{path}' is missing.", path);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(LegacySource).Assembly.Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mauime.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate Mauime.slnx above the test assembly.");
    }
}
