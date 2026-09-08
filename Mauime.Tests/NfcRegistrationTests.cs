using FluentAssertions;
using Mauime.Nfc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Xunit;

namespace Mauime.Tests;

/// <summary>
/// That <c>UseMauimeNfc</c> registers <see cref="INfc"/>, and how the slice with no implementation
/// behaves.
///
/// This runs against the <c>net10.0</c> slice of Mauime.Nfc, which is the unsupported one — a
/// net10.0 test project cannot reference the Android or iOS slices, so their implementations are
/// covered by the public-API baseline and not by behaviour. That limit is real and is not worked
/// around here.
/// </summary>
public class NfcRegistrationTests
{
    private static MauiAppBuilder Builder() => MauiApp.CreateBuilder(useDefaults: false);

    [Fact]
    public void UseMauimeNfc_registers_INfc_as_a_singleton()
    {
        var builder = Builder().UseMauimeNfc();

        var descriptor = builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(INfc)).Subject;

        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void UseMauimeNfc_returns_the_same_builder_so_it_can_be_chained()
    {
        var builder = Builder();

        builder.UseMauimeNfc().Should().BeSameAs(builder);
    }

    [Fact]
    public void UseMauimeNfc_rejects_a_null_builder()
    {
        var use = () => ((MauiAppBuilder)null!).UseMauimeNfc();

        use.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Registering_twice_yields_two_descriptors()
    {
        // AddSingleton appends. Recorded rather than guarded: the last registration wins on
        // resolution, so a duplicate call is harmless, and TryAdd would silently ignore a second
        // call that meant to replace the first.
        var builder = Builder().UseMauimeNfc().UseMauimeNfc();

        builder.Services.Count(d => d.ServiceType == typeof(INfc)).Should().Be(2);
    }

    [Fact]
    public void The_registration_resolves_to_an_INfc()
    {
        var builder = Builder().UseMauimeNfc();

        using var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<INfc>().Should().BeAssignableTo<INfc>();
    }

    [Fact]
    public void The_resolved_instance_is_the_same_every_time()
    {
        var builder = Builder().UseMauimeNfc();
        using var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<INfc>().Should().BeSameAs(provider.GetRequiredService<INfc>());
    }

    [Theory]
    [MemberData(nameof(EveryOperation))]
    public async Task Every_operation_throws_PlatformNotSupportedException_on_a_platform_without_NFC(
        string name, Func<INfc, Task> operation)
    {
        _ = name;

        using var provider = Builder().UseMauimeNfc().Services.BuildServiceProvider();
        var nfc = provider.GetRequiredService<INfc>();

        var call = async () => await operation(nfc);

        (await call.Should().ThrowAsync<PlatformNotSupportedException>())
            .WithMessage("*Android and iOS are supported*");
    }

    public static TheoryData<string, Func<INfc, Task>> EveryOperation()
    {
        var message = new NdefMessage(NdefRecord.Empty);

        return new TheoryData<string, Func<INfc, Task>>
        {
            { nameof(INfc.EnableSessionAsync), nfc => nfc.EnableSessionAsync() },
            { nameof(INfc.DisableSessionAsync), nfc => nfc.DisableSessionAsync() },
            { nameof(INfc.ReadNdefAsync), nfc => nfc.ReadNdefAsync() },
            { nameof(INfc.WriteNdefAsync), nfc => nfc.WriteNdefAsync(message) },
            { nameof(INfc.WriteReadNdefAsync), nfc => nfc.WriteReadNdefAsync(message) },
        };
    }

    [Fact]
    public void Disposing_the_unsupported_implementation_does_not_throw()
    {
        using var provider = Builder().UseMauimeNfc().Services.BuildServiceProvider();
        var nfc = provider.GetRequiredService<INfc>();

        var dispose = () => nfc.Dispose();

        dispose.Should().NotThrow();
    }
}
