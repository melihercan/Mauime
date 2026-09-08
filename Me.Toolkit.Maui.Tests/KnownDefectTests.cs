using FluentAssertions;
using Me.Toolkit.Maui.Nfc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// Defects of the Xamarinme code that the port fixed, rewritten in place from the pins that
/// recorded them, so each fix shows up as a diff rather than as silence.
///
/// Prefixes carry the status, as in Legacy.Tests:
/// <list type="bullet">
/// <item><c>FIXED_</c> — was a defect pin; the comment names the pin it replaced.</item>
/// <item><c>DEFECT_</c> — pinned as broken, waiting to be fixed.</item>
/// <item><c>LIMITATION_</c> — not a bug in this code.</item>
/// </list>
///
/// Some of these read source text. That is the same second choice the legacy pins made and for the
/// same reason: a net10.0 test project cannot reference the Android or iOS slices, so their code
/// cannot be loaded, let alone run. Where a fix *is* reachable — anything in the NDEF codec or the
/// platform-neutral slice — it is asserted by running it, in <see cref="NdefTests"/> and
/// <see cref="NfcRegistrationTests"/>.
/// </summary>
public class KnownDefectTests
{
    [Fact]
    public async Task FIXED_Nfc_has_a_platform_neutral_slice_that_compiles_and_loads()
    {
        // Was: DEFECT_Nfc_netstandard_slice_does_not_compile.
        // Xamarinme.Nfc's netstandard2.0 slice compiled `new Nfc()` against a type that slice could
        // not see, because NETSTANDARD2_0 was not defined for it, so the whole project failed to
        // build and had no test coverage at all. Me.Toolkit.Maui.Nfc's net10.0 slice is the same role — the
        // one a non-platform project resolves — and this test only exists because it now builds,
        // loads and runs.
        using var provider = NfcServices();
        var nfc = provider.GetRequiredService<INfc>();

        nfc.Should().BeAssignableTo<INfc>();

        var read = async () => await nfc.ReadNdefAsync();
        await read.Should().ThrowAsync<PlatformNotSupportedException>();
    }

    [Fact]
    public void FIXED_Nfc_carries_no_dead_conditional_blocks()
    {
        // Was: DEFECT_Nfc_Pcsc_carries_a_dead_if_false_block_with_a_second_constructor.
        // Xamarinme's Pcsc.cs held about 180 lines behind an `#if false`, including a whole second
        // Pcsc() constructor. The PC/SC layer is not part of this port, and nothing that did come
        // across brought a dead block with it.
        //
        // Read as source because there is nothing to run: dead code compiles to nothing.
        foreach (var file in Directory.GetFiles(
                     Path.Combine(TestAssemblies.RepositoryRoot, "Me.Toolkit.Maui.Nfc"), "*.cs",
                     SearchOption.AllDirectories))
        {
            File.ReadAllText(file).Should().NotContain("#if false",
                "'{0}' must not carry disabled code", file);
        }
    }

    [Fact]
    public void FIXED_Android_asks_for_a_mutable_PendingIntent_so_it_survives_Android_12()
    {
        // Found during the port rather than in Phase 0, because it is a runtime crash rather than
        // something visible in the API: Xamarinme passed 0 for the PendingIntent flags, and API 31
        // made one of FLAG_MUTABLE / FLAG_IMMUTABLE mandatory. EnableSessionAsync therefore threw
        // on every Android 12 or later device. Foreground dispatch needs the mutable one, because
        // the system fills the tag details into the intent before delivering it.
        //
        // Read as source: the Android slice cannot be loaded from a net10.0 test project, and
        // proving the fix for real needs a device, which this repository has deliberately not taken
        // on.
        var source = PlatformSource("Android", "AndroidNfc.cs");

        source.Should().Contain("PendingIntentFlags.Mutable");
        source.Should().Contain("OperatingSystem.IsAndroidVersionAtLeast(31)");
    }

    [Fact]
    public void FIXED_iOS_reports_callback_failures_through_the_task_instead_of_throwing()
    {
        // Also found during the port. Xamarinme threw from inside CoreNFC's completion callbacks,
        // which run on the session's dispatch queue: the throw could never reach the awaiting
        // caller, so a failed read or write left the caller awaiting a task that was never
        // completed. Every completion path now goes through TrySetResult / TrySetException.
        //
        // Read as source, for the same reason as the Android pin above.
        var source = PlatformSource("iOS", "IosNfc.cs");

        source.Should().Contain("TrySetException");
        source.Should().NotContain("throw new Exception");
    }

    [Fact]
    public void FIXED_The_platform_implementations_are_internal()
    {
        // Xamarinme exported a public Nfc class per platform, plus a public static CrossNfc
        // locator, so the public surface differed by platform and consumers could new up an
        // implementation directly. Me.Toolkit.Maui exposes INfc and the registration extension; every
        // implementation is internal, which is what lets MultiTargetingTests hold all five slices
        // to one surface.
        var exported = typeof(INfc).Assembly.GetExportedTypes().Select(t => t.FullName).ToList();

        exported.Should().NotContain(name => name!.Contains("CrossNfc", StringComparison.Ordinal));
        exported.Should().NotContain(name => name!.EndsWith("UnsupportedNfc", StringComparison.Ordinal));
        exported.Should().NotContain(name => name!.EndsWith("NfcPlatform", StringComparison.Ordinal));
    }

    [Fact]
    public void LIMITATION_The_Android_and_iOS_implementations_have_no_behavioural_coverage()
    {
        // Stated rather than papered over. A net10.0 test project resolves the net10.0 slice of
        // Me.Toolkit.Maui.Nfc, which is the one with no implementation, so nothing here exercises
        // AndroidNfc or IosNfc. They are covered by the public-API baseline and by
        // MultiTargetingTests, which is a real guard on their shape and none at all on their
        // behaviour; closing that gap needs the device-test harness that was deliberately not taken
        // on.
        //
        // This test asserts the fact it describes, so it fails if the situation ever changes.
        using var provider = NfcServices();

        provider.GetRequiredService<INfc>().GetType().Name.Should().Be("UnsupportedNfc");
    }

    private static ServiceProvider NfcServices() =>
        MauiApp.CreateBuilder(useDefaults: false).UseMeToolkitMauiNfc().Services.BuildServiceProvider();

    private static string PlatformSource(string platform, string fileName) =>
        File.ReadAllText(Path.Combine(
            TestAssemblies.RepositoryRoot, "Me.Toolkit.Maui.Nfc", "Platforms", platform, fileName));
}
