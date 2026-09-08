using FluentAssertions;
using Me.Toolkit.Maui.Configuration;
using Me.Toolkit.Maui.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Hosting;
using NSubstitute;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// <see cref="MeToolkitMauiHostingExtensions"/>, which is all that survived of Xamarinme.Hosting.
///
/// The first test is the reason the rest of that library did not come across: MAUI already
/// registers an <see cref="IHostEnvironment"/>. What it does not do is let the name be anything
/// other than <c>Production</c>.
/// </summary>
public class HostingTests
{
    private static MauiAppBuilder Builder() => MauiApp.CreateBuilder(useDefaults: false);

    private static IHostEnvironment Environment(MauiAppBuilder builder)
    {
        var provider = builder.Services.BuildServiceProvider();

        return provider.GetRequiredService<IHostEnvironment>();
    }

    [Fact]
    public void MAUI_registers_a_host_environment_of_its_own_and_it_always_says_Production()
    {
        var environment = Environment(Builder());

        environment.Should().NotBeNull();
        environment.EnvironmentName.Should().Be(Environments.Production);
        environment.IsProduction().Should().BeTrue();
    }

    [Fact]
    public void UseMeToolkitMauiHosting_sets_the_environment_name()
    {
        var environment = Environment(Builder().UseMeToolkitMauiHosting(Environments.Development));

        environment.EnvironmentName.Should().Be(Environments.Development);
        environment.IsDevelopment().Should().BeTrue();
    }

    [Fact]
    public void The_platform_environment_is_wrapped_so_everything_but_the_name_still_comes_from_it()
    {
        // The first attempt at this set EnvironmentName on MAUI's own instance, because the
        // interface declares a setter and MauiHostEnvironment reports CanWrite. Its setter throws
        // NotImplementedException, which only calling it reveals. So the platform environment is
        // wrapped instead, and everything except the name is delegated — asserted here against a
        // stand-in, since MAUI's real ApplicationName throws on this slice.
        var platform = Substitute.For<IHostEnvironment>();
        platform.EnvironmentName.Returns(Environments.Production);
        platform.ApplicationName.Returns("PlatformApp");
        platform.ContentRootPath.Returns("/platform/root");

        var builder = Builder();
        builder.Services.AddSingleton(platform);

        var environment = Environment(builder.UseMeToolkitMauiHosting(Environments.Development));

        environment.EnvironmentName.Should().Be(Environments.Development);
        environment.ApplicationName.Should().Be("PlatformApp");
        environment.ContentRootPath.Should().Be("/platform/root");
    }

    [Fact]
    public void An_arbitrary_environment_name_is_accepted()
    {
        Environment(Builder().UseMeToolkitMauiHosting("QA")).EnvironmentName.Should().Be("QA");
    }

    [Fact]
    public void UseMeToolkitMauiHostingFromConfiguration_reads_the_name_from_configuration()
    {
        var builder = Builder();
        builder.Configuration.AddInMemoryCollection(
            [new(MeToolkitMauiHostingExtensions.DefaultConfigurationKey, "Development")]);

        Environment(builder.UseMeToolkitMauiHostingFromConfiguration()).EnvironmentName.Should().Be("Development");
    }

    [Fact]
    public void Configuration_added_after_the_call_is_still_seen()
    {
        // The key is read when IHostEnvironment is first resolved, not when the extension runs, so
        // the order of the two calls does not matter. That is worth pinning: MauiProgram is a
        // sequence of builder calls and getting them the wrong way round is easy.
        var builder = Builder().UseMeToolkitMauiHostingFromConfiguration();

        builder.Configuration.AddInMemoryCollection(
            [new(MeToolkitMauiHostingExtensions.DefaultConfigurationKey, "Staging")]);

        Environment(builder).EnvironmentName.Should().Be("Staging");
    }

    [Fact]
    public void A_custom_configuration_key_is_honoured()
    {
        var builder = Builder();
        builder.Configuration.AddInMemoryCollection([new("Deployment:Ring", "Canary")]);

        Environment(builder.UseMeToolkitMauiHostingFromConfiguration("Deployment:Ring"))
            .EnvironmentName.Should().Be("Canary");
    }

    [Fact]
    public void An_absent_key_leaves_the_platform_default_alone()
    {
        Environment(Builder().UseMeToolkitMauiHostingFromConfiguration())
            .EnvironmentName.Should().Be(Environments.Production);
    }

    [Fact]
    public void An_empty_value_leaves_the_platform_default_alone()
    {
        var builder = Builder();
        builder.Configuration.AddInMemoryCollection(
            [new(MeToolkitMauiHostingExtensions.DefaultConfigurationKey, "")]);

        Environment(builder.UseMeToolkitMauiHostingFromConfiguration())
            .EnvironmentName.Should().Be(Environments.Production);
    }

    [Fact]
    public void The_environment_reads_from_an_embedded_appsettings_file()
    {
        // The two packages together, which is how Xamarinme's XAMARIN_ENVIRONMENT worked: the
        // environment name comes out of the settings file, under a key renamed for MAUI.
        var builder = Builder();
        builder.Configuration.AddEmbeddedResourceJson(
            typeof(HostingTests).Assembly, "Me.Toolkit.Maui.Tests.TestAssets", "environment.json");

        Environment(builder.UseMeToolkitMauiHostingFromConfiguration()).EnvironmentName.Should().Be("Staging");
    }

    [Fact]
    public void The_environment_is_resolved_once()
    {
        var builder = Builder().UseMeToolkitMauiHosting("Development");
        var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<IHostEnvironment>()
            .Should().BeSameAs(provider.GetRequiredService<IHostEnvironment>());
    }

    [Fact]
    public void Both_extensions_return_the_builder_so_calls_can_be_chained()
    {
        var one = Builder();
        var two = Builder();

        one.UseMeToolkitMauiHosting("Development").Should().BeSameAs(one);
        two.UseMeToolkitMauiHostingFromConfiguration().Should().BeSameAs(two);
    }

    [Theory]
    [MemberData(nameof(BadArguments))]
    public void Arguments_are_validated(string name, Action call)
    {
        _ = name;

        call.Should().Throw<ArgumentException>();
    }

    public static TheoryData<string, Action> BadArguments() => new()
    {
        { "null builder", () => ((MauiAppBuilder)null!).UseMeToolkitMauiHosting("Development") },
        { "null environment name", () => MauiApp.CreateBuilder(false).UseMeToolkitMauiHosting(null!) },
        { "empty environment name", () => MauiApp.CreateBuilder(false).UseMeToolkitMauiHosting("") },
        { "null builder, from configuration", () => ((MauiAppBuilder)null!).UseMeToolkitMauiHostingFromConfiguration() },
        { "empty key", () => MauiApp.CreateBuilder(false).UseMeToolkitMauiHostingFromConfiguration("") },
    };
}
