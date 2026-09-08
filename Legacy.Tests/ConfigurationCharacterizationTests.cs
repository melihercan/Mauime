using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xamarinme;
using Xunit;

namespace Legacy.Tests;

/// <summary>
/// What Xamarinme.Configuration 1.0.3 actually does, pinned before anything is ported.
///
/// The fixtures are embedded in this test assembly, because the provider reads
/// <c>{Prefix}.appsettings.json</c> out of an assembly's manifest resources and there is no other
/// way to reach it.
///
/// Several of these assertions look wrong at first glance — booleans come back as "False", null
/// comes back as "" — and they are: the library vendors a Newtonsoft-era copy of Microsoft's
/// JsonConfigurationFileParser, and Newtonsoft renders JValues differently from the System.Text.Json
/// parser Microsoft replaced it with in .NET Core 3.0. Pinning it is the point. The library's own
/// README claims <c>Configuration["Logging:IncludeScopes"]</c> reads <c>false</c>; it does not.
/// </summary>
public class ConfigurationCharacterizationTests
{
    private const string Basic = "Legacy.Tests.TestAssets.Basic";

    private static IConfigurationRoot Build(string prefix, string? environment = null)
    {
        var builder = new ConfigurationBuilder();
        var options = new EmbeddedResourceConfigurationOptions
        {
            Assembly = typeof(ConfigurationCharacterizationTests).Assembly,
            Prefix = prefix,
        };

        return environment is null
            ? builder.AddEmbeddedResource(options).Build()
            : builder.AddEmbeddedResource(options, environment).Build();
    }

    [Fact]
    public void Top_level_string_values_are_read_from_the_base_file()
    {
        Build(Basic)["Build"].Should().Be("Default");
    }

    [Fact]
    public void Nested_objects_flatten_onto_colon_separated_keys()
    {
        var configuration = Build(Basic);

        configuration["Logging:LogLevel:Default"].Should().Be("Debug");
        configuration["Logging:LogLevel:System"].Should().Be("Information");
    }

    [Fact]
    public void Arrays_flatten_onto_index_keys()
    {
        var configuration = Build(Basic);

        configuration["Numbers:0"].Should().Be("10");
        configuration["Numbers:1"].Should().Be("20");
        configuration["Numbers:2"].Should().Be("30");
    }

    [Fact]
    public void Keys_are_matched_case_insensitively()
    {
        Build(Basic)["build"].Should().Be("Default");
    }

    [Fact]
    public void Numbers_are_rendered_with_the_invariant_culture()
    {
        var configuration = Build(Basic);

        configuration["Count"].Should().Be("42");
        configuration["Ratio"].Should().Be("1.5");
    }

    [Fact]
    public void Booleans_are_rendered_with_a_capital_first_letter()
    {
        // Newtonsoft renders a JValue of type Boolean through bool.ToString(), which is "True" /
        // "False". Microsoft's own parser does the same, through JsonElement.ToString() — checked
        // against Mauime.Configuration rather than assumed, because the obvious guess was that they
        // differed here. Binding via GetValue<bool>() copes; a string comparison against "false"
        // does not, and the library's README makes exactly that mistake about its own output.
        var configuration = Build(Basic);

        configuration["Enabled"].Should().Be("True");
        configuration["Logging:IncludeScopes"].Should().Be("False");
    }

    [Fact]
    public void Json_null_is_rendered_as_an_empty_string_rather_than_null()
    {
        // Microsoft's parser stores null for a JSON null, so IConfiguration reports the key as
        // absent. Here the key exists and reads as "".
        var configuration = Build(Basic);

        configuration["Nothing"].Should().Be(string.Empty);
    }

    [Fact]
    public void The_environment_file_overrides_the_base_file_when_the_environment_matches()
    {
        var configuration = Build(Basic, "Development");

        configuration["Build"].Should().Be("Development");
        configuration["Logging:LogLevel:Default"].Should().Be("Trace");
        configuration["OnlyInDevelopment"].Should().Be("yes");
    }

    [Fact]
    public void Base_only_keys_survive_the_environment_overlay()
    {
        Build(Basic, "Development")["Logging:LogLevel:System"].Should().Be("Information");
    }

    [Fact]
    public void The_environment_defaults_to_Production_so_the_Development_file_is_ignored()
    {
        var configuration = Build(Basic);

        configuration["Build"].Should().Be("Default");
        configuration["OnlyInDevelopment"].Should().BeNull();
    }

    [Fact]
    public void An_environment_with_no_file_is_not_an_error()
    {
        Build(Basic, "Staging")["Build"].Should().Be("Default");
    }

    [Fact]
    public void A_prefix_that_matches_nothing_yields_an_empty_configuration()
    {
        Build("No.Such.Prefix").AsEnumerable().Should().BeEmpty();
    }

    [Fact]
    public void Two_keys_that_differ_only_by_case_are_a_FormatException()
    {
        // The parser's own duplicate-key guard, reached because it folds keys case-insensitively
        // while JSON does not. Two properties with the identical name never get this far: Newtonsoft
        // rejects those itself, with a different exception. See KnownDefectTests.
        var build = () => Build("Legacy.Tests.TestAssets.CaseCollision");

        build.Should().Throw<FormatException>().WithMessage("*duplicate key*");
    }

    [Fact]
    public void Malformed_json_surfaces_as_a_Newtonsoft_exception()
    {
        // Not wrapped in anything of the library's own, so callers have to reference Newtonsoft to
        // catch it by type. Pinned because dropping Newtonsoft will necessarily change it.
        var build = () => Build("Legacy.Tests.TestAssets.Malformed");

        build.Should().Throw<Newtonsoft.Json.JsonException>();
    }

    [Fact]
    public void The_source_exposes_its_options_and_environment_for_the_callback_overload()
    {
        var configuration = new ConfigurationBuilder()
            .AddEmbeddedResource(source =>
            {
                source.Options = new EmbeddedResourceConfigurationOptions
                {
                    Assembly = typeof(ConfigurationCharacterizationTests).Assembly,
                    Prefix = Basic,
                };
                source.Environment = "Development";
            })
            .Build();

        configuration["Build"].Should().Be("Development");
    }

    [Fact]
    public void A_source_built_by_hand_defaults_its_environment_to_Production()
    {
        new EmbeddedResourceConfigurationSource().Environment.Should().Be("Production");
    }

    [Fact]
    public void Options_carry_no_defaults_of_their_own()
    {
        var options = new EmbeddedResourceConfigurationOptions();

        options.Assembly.Should().BeNull();
        options.Prefix.Should().BeNull();
    }

    [Fact]
    public void The_provider_can_be_driven_directly()
    {
        var provider = new EmbeddedResourceConfigurationProvider(
            new EmbeddedResourceConfigurationOptions
            {
                Assembly = typeof(ConfigurationCharacterizationTests).Assembly,
                Prefix = Basic,
            },
            "Development");

        provider.Load();

        provider.TryGet("Build", out var value).Should().BeTrue();
        value.Should().Be("Development");
    }

    [Fact]
    public void The_assembly_version_is_pinned_at_1_0_0_while_the_package_version_moved_on()
    {
        // Configuration.csproj sets AssemblyVersion 1.0.0 and Version 1.0.3, so every 1.0.x
        // assembly presents the same identity to the binder. Hosting sets neither and lets both
        // derive from Version. Recorded because the two projects disagree, not because either is
        // wrong on its own.
        var assembly = typeof(EmbeddedResourceConfigurationOptions).Assembly;

        assembly.GetName().Version.Should().Be(new Version(1, 0, 0, 0));
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Should().StartWith("1.0.3");
    }
}
