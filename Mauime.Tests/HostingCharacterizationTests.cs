using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xamarinme;
using Xunit;

namespace Mauime.Tests;

/// <summary>
/// What Xamarinme.Hosting 1.0.4 actually does, pinned before anything is ported.
///
/// The shape being pinned is the one MAUI already ships as <c>MauiAppBuilder</c>: Configuration,
/// Services, Logging and a Build(). What MAUI does *not* ship is the environment name, which this
/// library reads from a <c>XAMARIN_ENVIRONMENT</c> entry in appsettings.json because Xamarin had no
/// usable environment variables. Those reads are the interesting part of this file.
/// </summary>
public class HostingCharacterizationTests
{
    private static XamarinHostBuilder CreateDefault(string prefix) =>
        XamarinHostBuilder.CreateDefault(new EmbeddedResourceConfigurationOptions
        {
            Assembly = typeof(HostingCharacterizationTests).Assembly,
            Prefix = prefix,
        });

    [Fact]
    public void CreateDefault_exposes_configuration_services_environment_and_logging()
    {
        var builder = CreateDefault("Mauime.Tests.TestAssets.EnvMissing");

        builder.Configuration.Should().NotBeNull();
        builder.Services.Should().NotBeNull();
        builder.HostEnvironment.Should().NotBeNull();

        // AddLogging runs its callback synchronously, so Logging is usable before Build().
        builder.Logging.Should().NotBeNull();
    }

    [Fact]
    public void The_environment_is_Production_when_the_setting_is_absent()
    {
        CreateDefault("Mauime.Tests.TestAssets.EnvMissing")
            .HostEnvironment.Environment.Should().Be("Production");
    }

    [Fact]
    public void The_environment_comes_from_XAMARIN_ENVIRONMENT_in_the_base_file()
    {
        CreateDefault("Mauime.Tests.TestAssets.EnvDevelopment")
            .HostEnvironment.Environment.Should().Be("Development");
    }

    [Fact]
    public void The_environment_file_is_layered_once_XAMARIN_ENVIRONMENT_selects_it()
    {
        var builder = CreateDefault("Mauime.Tests.TestAssets.EnvDevelopment");

        builder.Configuration["Build"].Should().Be("FromEnvironmentFile");
    }

    [Fact]
    public void Configuration_is_readable_straight_off_the_builder_without_calling_Build()
    {
        CreateDefault("Mauime.Tests.TestAssets.EnvMissing")
            .Configuration["Build"].Should().Be("Base");
    }

    [Fact]
    public void Build_returns_a_host_whose_services_resolve_configuration_and_environment()
    {
        var host = CreateDefault("Mauime.Tests.TestAssets.EnvDevelopment").Build();

        host.Services.GetRequiredService<IConfiguration>()["Build"].Should().Be("FromEnvironmentFile");
        host.Services.GetRequiredService<IXamarinHostEnvironment>().Environment.Should().Be("Development");
    }

    [Fact]
    public void Build_resolves_the_logger_factory_the_constructor_configured()
    {
        var host = CreateDefault("Mauime.Tests.TestAssets.EnvMissing").Build();

        host.Services.GetRequiredService<ILogger<HostingCharacterizationTests>>().Should().NotBeNull();
    }

    [Fact]
    public void User_registered_services_survive_to_the_built_host()
    {
        var builder = CreateDefault("Mauime.Tests.TestAssets.EnvMissing");
        builder.Services.AddSingleton<ISample, Sample>();

        builder.Build().Services.GetRequiredService<ISample>().Should().BeOfType<Sample>();
    }

    [Fact]
    public void The_environment_is_injectable_into_a_registered_service()
    {
        var builder = CreateDefault("Mauime.Tests.TestAssets.EnvDevelopment");
        builder.Services.AddSingleton<ISample, Sample>();

        var sample = (Sample)builder.Build().Services.GetRequiredService<ISample>();

        sample.Environment.Environment.Should().Be("Development");
        sample.Configuration["Build"].Should().Be("FromEnvironmentFile");
    }

    [Fact]
    public void The_configuration_object_is_its_own_builder_and_root()
    {
        // XamarinHostConfiguration is a copy of Blazor's WebAssemblyHostConfiguration: one object
        // implementing IConfiguration, IConfigurationRoot and IConfigurationBuilder at once, so it
        // can be read while it is still being built.
        var configuration = CreateDefault("Mauime.Tests.TestAssets.EnvMissing").Configuration;

        configuration.Should().BeAssignableTo<IConfiguration>();
        configuration.Should().BeAssignableTo<IConfigurationRoot>();
        configuration.Should().BeAssignableTo<IConfigurationBuilder>();

        ((IConfigurationBuilder)configuration).Build().Should().BeSameAs(configuration);
    }

    [Fact]
    public void The_configuration_reports_one_provider_per_added_source()
    {
        var configuration = (IConfigurationRoot)CreateDefault("Mauime.Tests.TestAssets.EnvMissing").Configuration;

        configuration.Providers.Should().ContainSingle()
            .Which.Should().BeOfType<EmbeddedResourceConfigurationProvider>();
    }

    [Fact]
    public void Setting_a_value_before_any_provider_exists_is_an_InvalidOperationException()
    {
        var set = () => new XamarinHostConfiguration()["Key"] = "value";

        set.Should().Throw<InvalidOperationException>()
            .WithMessage("Can only set property if at least one provider has been inserted.");
    }

    [Fact]
    public void Adding_a_null_source_is_an_ArgumentNullException()
    {
        var add = () => new XamarinHostConfiguration().Add(null!);

        add.Should().Throw<ArgumentNullException>();
    }

    private interface ISample
    {
        IXamarinHostEnvironment Environment { get; }
        IConfiguration Configuration { get; }
    }

    private sealed class Sample(IXamarinHostEnvironment environment, IConfiguration configuration) : ISample
    {
        public IXamarinHostEnvironment Environment { get; } = environment;
        public IConfiguration Configuration { get; } = configuration;
    }
}
