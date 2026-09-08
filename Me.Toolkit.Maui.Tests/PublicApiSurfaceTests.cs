using FluentAssertions;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// The public surface of the legacy libraries, captured before anything was touched.
///
/// Me.Toolkit.Maui ships under new package IDs, so there are no existing consumers and this is *not* an
/// additive-only guarantee: the API is free to be fixed. The baseline exists so that every change
/// to it is a decision rather than an accident — a diff to review, in the commit that causes it.
///
/// When a change is intentional, review the diff and copy <c>PublicApi.received.txt</c> from the
/// test output directory over <c>PublicApi.approved.txt</c> in the same commit. Never weaken the
/// assertion.
/// </summary>
public class PublicApiSurfaceTests
{
    [Fact]
    public void Public_surface_matches_the_approved_baseline()
    {
        var received = PublicApiDumper.Dump(TestAssemblies.Baseline());

        var receivedPath = Path.Combine(AppContext.BaseDirectory, "PublicApi.received.txt");
        File.WriteAllText(receivedPath, received);

        var approvedPath = Path.Combine(TestAssemblies.RepositoryRoot, "Me.Toolkit.Maui.Tests", "PublicApi.approved.txt");
        File.Exists(approvedPath).Should().BeTrue(
            "the baseline must be checked in; generate it from '{0}'", receivedPath);

        var approved = File.ReadAllText(approvedPath);

        Normalize(received).Should().Be(
            Normalize(approved),
            "a change to the public surface must be deliberate. If this one is, review the diff and "
            + "copy '{0}' over 'Me.Toolkit.Maui.Tests/PublicApi.approved.txt' in the same commit.",
            receivedPath);
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").TrimEnd();
}
