using System.Reflection;
using FluentAssertions;
using Me.Toolkit.Maui.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// <see cref="MeToolkitMauiConfigurationExtensions"/>, through its embedded-resource path.
///
/// The app-package path cannot be covered here: MAUI's <c>FileSystem</c> on the platform-neutral
/// net10.0 slice is the reference-assembly stub and throws
/// <c>NotImplementedInReferenceAssemblyException</c>. Both paths funnel into the same private
/// layering code, so what is exercised here is everything except the four lines that open a
/// <c>MauiAsset</c>.
///
/// Where these disagree with what <c>Legacy.Tests/ConfigurationCharacterizationTests</c> pins, the
/// difference is called out in place. There is exactly one: a JSON <c>null</c>. Xamarinme's
/// vendored Newtonsoft-era parser rendered it as <c>""</c>, so the key existed and read as an empty
/// string; Microsoft's stores null, so the key is simply absent. Booleans render <c>"True"</c> in
/// both, which was checked rather than assumed — the obvious guess was wrong.
/// </summary>
public class ConfigurationTests
{
    private const string Prefix = "Me.Toolkit.Maui.Tests.TestAssets";

    private static Assembly Assembly => typeof(ConfigurationTests).Assembly;

    private static IConfigurationRoot Build(
        string fileName = MeToolkitMauiConfigurationExtensions.DefaultFileName,
        string? environment = null,
        bool optional = true) =>
        new ConfigurationBuilder()
            .AddEmbeddedResourceJson(Assembly, Prefix, fileName, environment, optional)
            .Build();

    [Fact]
    public void Top_level_values_are_read()
    {
        Build()["Build"].Should().Be("Default");
    }

    [Fact]
    public void Nested_objects_flatten_onto_colon_separated_keys()
    {
        var configuration = Build();

        configuration["Logging:LogLevel:Default"].Should().Be("Debug");
        configuration["Logging:LogLevel:System"].Should().Be("Information");
    }

    [Fact]
    public void Arrays_flatten_onto_index_keys()
    {
        Build()["Numbers:1"].Should().Be("20");
    }

    [Fact]
    public void Keys_are_matched_case_insensitively()
    {
        Build()["build"].Should().Be("Default");
    }

    [Fact]
    public void Booleans_render_with_a_capital_first_letter_here_too()
    {
        // Not a difference, though it looks like one should exist: Microsoft's parser renders a
        // JSON true through JsonElement.ToString(), which is "True", exactly as Newtonsoft's
        // JValue did. Legacy.Tests pins the same values.
        //
        // So the Xamarinme README's claim that Configuration["Logging:IncludeScopes"] reads
        // "false" was never about the parser being old. It was simply wrong, and is still wrong
        // about this package. Anything comparing a configuration string to "true" is broken either
        // way; GetValue<bool>() parses both spellings.
        var configuration = Build();

        configuration["Enabled"].Should().Be("True");
        configuration["Logging:IncludeScopes"].Should().Be("False");
        configuration.GetValue<bool>("Enabled").Should().BeTrue();
    }

    [Fact]
    public void A_json_null_leaves_the_key_absent_unlike_the_Xamarinme_parser()
    {
        // Legacy.Tests pins "" here. Microsoft's parser stores null, which IConfiguration reports
        // as an absent key, so GetValue<T> falls back to a default instead of failing to parse "".
        Build()["Nothing"].Should().BeNull();
    }

    [Fact]
    public void The_environment_file_is_layered_over_the_base_one()
    {
        var configuration = Build(environment: "Development");

        configuration["Build"].Should().Be("Development");
        configuration["Logging:LogLevel:Default"].Should().Be("Trace");
        configuration["OnlyInDevelopment"].Should().Be("yes");
    }

    [Fact]
    public void Base_only_keys_survive_the_overlay()
    {
        Build(environment: "Development")["Logging:LogLevel:System"].Should().Be("Information");
    }

    [Fact]
    public void No_environment_means_no_overlay()
    {
        var configuration = Build();

        configuration["Build"].Should().Be("Default");
        configuration["OnlyInDevelopment"].Should().BeNull();
    }

    [Fact]
    public void An_environment_with_no_file_is_not_an_error_when_optional()
    {
        Build(environment: "Staging")["Build"].Should().Be("Default");
    }

    [Fact]
    public void A_missing_optional_file_yields_an_empty_configuration()
    {
        Build("nosuchfile.json").AsEnumerable().Should().BeEmpty();
    }

    [Fact]
    public void A_missing_required_file_names_the_file_and_where_it_looked()
    {
        // Xamarinme's equivalent was a NullReferenceException out of Build(), or, when only the
        // prefix was wrong, silence and an empty configuration.
        var build = () => Build("nosuchfile.json", optional: false);

        build.Should().Throw<FileNotFoundException>()
            .WithMessage("*nosuchfile.json*")
            .WithMessage($"*{Prefix}*");
    }

    [Fact]
    public void A_missing_required_environment_overlay_names_the_overlay()
    {
        var build = () => Build(environment: "Staging", optional: false);

        build.Should().Throw<FileNotFoundException>().WithMessage("*appsettings.Staging.json*");
    }

    [Fact]
    public void Malformed_json_is_rejected()
    {
        var build = () => Build("appsettings.Malformed.json");

        build.Should().Throw<Exception>();
    }

    [Fact]
    public void Two_keys_differing_only_by_case_are_a_FormatException()
    {
        // Same as the legacy behaviour, and for the same reason: configuration keys fold
        // case-insensitively while JSON does not. Microsoft's parser rejects it too.
        var build = () => Build("appsettings.CaseCollision.json");

        build.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("appsettings.json", "Development", "appsettings.Development.json")]
    [InlineData("settings.json", "Staging", "settings.Staging.json")]
    [InlineData("no-extension", "Production", "no-extension.Production")]
    public void The_overlay_file_name_puts_the_environment_before_the_extension(
        string fileName, string environment, string expected)
    {
        MeToolkitMauiConfigurationExtensions.EnvironmentFileName(fileName, environment).Should().Be(expected);
    }

    [Fact]
    public void The_builder_is_returned_so_calls_can_be_chained()
    {
        var builder = new ConfigurationBuilder();

        builder.AddEmbeddedResourceJson(Assembly, Prefix).Should().BeSameAs(builder);
    }

    [Fact]
    public void Both_sources_are_added_when_an_environment_is_given()
    {
        var builder = new ConfigurationBuilder();

        builder.AddEmbeddedResourceJson(Assembly, Prefix, environment: "Development");

        builder.Sources.Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(BadArguments))]
    public void Arguments_are_validated_rather_than_dereferenced(string name, Action call)
    {
        // Every one of these was a NullReferenceException out of Build() in Xamarinme, or worse,
        // an empty configuration and no error at all.
        _ = name;

        call.Should().Throw<ArgumentException>();
    }

    public static TheoryData<string, Action> BadArguments()
    {
        var assembly = typeof(ConfigurationTests).Assembly;

        return new TheoryData<string, Action>
        {
            { "null builder", () => ((IConfigurationBuilder)null!).AddEmbeddedResourceJson(assembly, Prefix) },
            { "null assembly", () => new ConfigurationBuilder().AddEmbeddedResourceJson(null!, Prefix) },
            { "null prefix", () => new ConfigurationBuilder().AddEmbeddedResourceJson(assembly, null!) },
            { "empty prefix", () => new ConfigurationBuilder().AddEmbeddedResourceJson(assembly, "") },
            { "empty file name", () => new ConfigurationBuilder().AddEmbeddedResourceJson(assembly, Prefix, "") },
            { "null app package file name", () => new ConfigurationBuilder().AddAppPackageJson(null!) },
            { "null builder, app package", () => ((IConfigurationBuilder)null!).AddAppPackageJson() },
        };
    }
}
