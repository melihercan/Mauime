using FluentAssertions;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// That every Me.Toolkit.Maui library really is built for every MAUI target framework, and that all of its
/// slices expose the same public surface.
///
/// The baseline renders each library from its <c>net10.0</c> slice only, which would otherwise
/// leave four fifths of what ships unchecked. This is what holds the rest to it. An
/// Android-only member added by accident — the easy mistake when half a plugin lives behind
/// <c>#if ANDROID</c> — fails here rather than shipping.
///
/// If a platform-specific member is ever added on purpose, this test is rewritten to name the
/// exception, in the commit that adds it. Do not weaken it into a subset check.
/// </summary>
public class MultiTargetingTests
{
    public static TheoryData<string> Libraries()
    {
        var data = new TheoryData<string>();
        foreach (var name in TestAssemblies.LibraryNames)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    /// What <c>Directory.Build.props</c> produces on the operating system running the tests. The
    /// iOS and Mac Catalyst library slices do compile on Windows — only building and signing an
    /// *app* for them needs a Mac — but the whole set is unavailable on Linux, which is what CI
    /// will run on.
    /// </summary>
    private static string[] ExpectedTargetFrameworks()
    {
        var frameworks = new List<string> { "net10.0", "net10.0-android" };

        if (!OperatingSystem.IsLinux())
        {
            frameworks.Add("net10.0-ios");
            frameworks.Add("net10.0-maccatalyst");
        }

        if (OperatingSystem.IsWindows())
        {
            frameworks.Add("net10.0-windows10.0.19041.0");
        }

        return [.. frameworks.OrderBy(f => f, StringComparer.Ordinal)];
    }

    [Theory]
    [MemberData(nameof(Libraries))]
    public void Every_library_is_built_for_every_expected_target_framework(string assemblyName)
    {
        TestAssemblies.BuiltTargetFrameworks(assemblyName)
            .Should().BeEquivalentTo(ExpectedTargetFrameworks());
    }

    [Theory]
    [MemberData(nameof(Libraries))]
    public void Every_platform_slice_exposes_the_same_surface_as_the_neutral_one(string assemblyName)
    {
        var neutral = PublicApiDumper.Dump(
            assemblyName, TestAssemblies.LocateDll(assemblyName, TestAssemblies.NeutralTargetFramework));

        foreach (var framework in TestAssemblies.BuiltTargetFrameworks(assemblyName)
                     .Where(f => f != TestAssemblies.NeutralTargetFramework))
        {
            var slice = PublicApiDumper.Dump(assemblyName, TestAssemblies.LocateDll(assemblyName, framework));

            slice.Should().Be(neutral,
                "the {0} slice of {1} must expose the same public surface as {2}",
                framework, assemblyName, TestAssemblies.NeutralTargetFramework);
        }
    }

    [Theory]
    [MemberData(nameof(Libraries))]
    public void Every_slice_records_what_it_was_compiled_against(string assemblyName)
    {
        // Directory.Build.targets writes Me.Toolkit.Maui.ReferencePaths.txt beside each assembly, and the
        // API baseline cannot resolve a platform slice's types without it. A silent regression here
        // would not fail the baseline while the libraries are still empty, so it is asserted
        // directly.
        foreach (var framework in TestAssemblies.BuiltTargetFrameworks(assemblyName))
        {
            var dll = TestAssemblies.LocateDll(assemblyName, framework);
            var referencePaths = Path.Combine(Path.GetDirectoryName(dll)!, "Me.Toolkit.Maui.ReferencePaths.txt");

            File.Exists(referencePaths).Should().BeTrue(
                "{0} ({1}) must record its reference paths at '{2}'", assemblyName, framework, referencePaths);

            File.ReadAllLines(referencePaths).Should().NotBeEmpty();
        }
    }
}
