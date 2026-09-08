using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FluentAssertions;
using Mauime.WebHostPatch;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Xunit;

namespace Mauime.Tests;

/// <summary>
/// <see cref="IMauimeWebHost"/>, exercised by actually starting Kestrel and making requests to it.
///
/// That is the point of these tests as much as the coverage. Xamarinme.WebHostPatch existed because
/// ASP.NET Core 2.2 on Mono needed two forks of Microsoft's code to run a web host at all; the claim
/// that the patches are no longer needed is worth more when a server really does start, bind and
/// answer than when it is asserted in a comment.
///
/// What these cannot cover is the platform question that matters most: ASP.NET Core here comes from
/// netstandard2.0 packages precisely so it can run on Android and iOS, and a net10.0 test project
/// cannot prove that. Only a device can.
///
/// Everything binds loopback on port 0, so the tests take a free port from the operating system and
/// never reach the network.
/// </summary>
public class WebHostTests
{
    private static IMauimeWebHost Create(Action<MauimeWebHostOptions>? configure = null)
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false).UseMauimeWebHost(options =>
        {
            options.ListenOnAllInterfaces = false;
            options.Port = 0;
            configure?.Invoke(options);
        });

        return builder.Services.BuildServiceProvider().GetRequiredService<IMauimeWebHost>();
    }

    private static Action<MauimeWebHostOptions> Responds(string body) => options =>
        options.ConfigureApplication = app => app.Run(context => context.Response.WriteAsync(body));

    [Fact]
    public void UseMauimeWebHost_registers_a_singleton_that_is_not_started()
    {
        var host = Create();

        host.IsRunning.Should().BeFalse();
        host.Address.Should().BeNull();
    }

    [Fact]
    public void UseMauimeWebHost_returns_the_builder_and_rejects_a_null_one()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false);
        builder.UseMauimeWebHost().Should().BeSameAs(builder);

        var use = () => ((MauiAppBuilder)null!).UseMauimeWebHost();
        use.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task A_started_host_serves_requests()
    {
        await using var host = Create(Responds("hello from a MAUI app"));

        var address = await host.StartAsync(TestContext.Current.CancellationToken);

        host.IsRunning.Should().BeTrue();
        host.Address.Should().Be(address);

        using var client = new HttpClient();
        var body = await client.GetStringAsync(address, TestContext.Current.CancellationToken);

        body.Should().Be("hello from a MAUI app");
    }

    [Fact]
    public async Task The_reported_address_carries_the_port_the_operating_system_chose()
    {
        // Port 0 means "any free port". The address has to come from the server after it binds,
        // not from the options, or it would say 0.
        await using var host = Create();

        var address = await host.StartAsync(TestContext.Current.CancellationToken);

        address.Port.Should().BeGreaterThan(0);
        address.Scheme.Should().Be(Uri.UriSchemeHttp);
    }

    [Fact]
    public async Task A_loopback_host_reports_a_loopback_address()
    {
        await using var host = Create();

        var address = await host.StartAsync(TestContext.Current.CancellationToken);

        IPAddress.TryParse(address.Host, out var parsed);
        (IPAddress.IsLoopback(parsed ?? IPAddress.None) || address.Host == "localhost")
            .Should().BeTrue("'{0}' should be loopback", address.Host);
    }

    [Fact]
    public async Task Starting_twice_is_an_InvalidOperationException()
    {
        await using var host = Create();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var start = async () => await host.StartAsync(TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already running*");
    }

    [Fact]
    public async Task Stopping_releases_the_port_and_clears_the_address()
    {
        var host = Create(Responds("still here"));
        var address = await host.StartAsync(TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);

        host.IsRunning.Should().BeFalse();
        host.Address.Should().BeNull();

        // The socket is really gone: binding the same port again succeeds.
        using var listener = new TcpListener(IPAddress.Loopback, address.Port);
        var rebind = () => listener.Start();
        rebind.Should().NotThrow();
        listener.Stop();

        await host.DisposeAsync();
    }

    [Fact]
    public async Task Stopping_a_host_that_never_started_does_nothing()
    {
        await using var host = Create();

        var stop = async () => await host.StopAsync(TestContext.Current.CancellationToken);

        await stop.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_host_can_be_restarted()
    {
        await using var host = Create(Responds("round two"));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
        var address = await host.StartAsync(TestContext.Current.CancellationToken);

        using var client = new HttpClient();
        var body = await client.GetStringAsync(address, TestContext.Current.CancellationToken);

        body.Should().Be("round two");
    }

    [Fact]
    public async Task Disposing_stops_a_running_host()
    {
        var host = Create();
        await host.StartAsync(TestContext.Current.CancellationToken);

        await host.DisposeAsync();

        host.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ConfigureBuilder_runs_before_the_application_is_built()
    {
        await using var host = Create(options =>
        {
            options.ConfigureBuilder = builder =>
                builder.ConfigureServices(services => services.AddSingleton(new Greeting("configured")));
            options.ConfigureApplication = app => app.Run(context =>
                context.Response.WriteAsync(
                    app.ApplicationServices.GetRequiredService<Greeting>().Text));
        });

        var address = await host.StartAsync(TestContext.Current.CancellationToken);

        using var client = new HttpClient();
        var body = await client.GetStringAsync(address, TestContext.Current.CancellationToken);

        body.Should().Be("configured");
    }

    // The tests below replace a single permissive one that asserted only "routable IPv4, or null".
    // It passed on Android while GetLocalAddress returned null there and the demo app displayed
    // http://0.0.0.0:5001/ under a label telling the user to open it from another device. Allowing
    // null was meant to tolerate a build machine with no network; what it actually tolerated was the
    // defect. These pin the selection rules instead, which needs no network at all.

    [Fact]
    public void An_interface_the_platform_will_not_classify_is_still_used()
    {
        // Android denies /sys/class/net/<name>/type, which is how .NET classifies an interface on
        // Linux, so every interface reports Unknown - including the working Wi-Fi one. Requiring
        // Ethernet or Wireless80211, as this once did, rejects the only interface there is.
        var candidates = new[]
        {
            Candidate(NetworkInterfaceType.Unknown, "192.168.1.16"),
        };

        NetworkAddress.SelectAddress(candidates).Should().Be(IPAddress.Parse("192.168.1.16"));
    }

    [Fact]
    public void Loopback_is_never_reported_even_when_its_interface_looks_like_any_other()
    {
        // The risk created by no longer filtering on interface type. Android classified lo correctly
        // on the device this was checked against, so this case was not what broke there - but the
        // change removes the guarantee that it would be caught by type, and an address check holds
        // whether or not the platform will classify anything.
        var candidates = new[]
        {
            Candidate(NetworkInterfaceType.Unknown, "127.0.0.1"),
            Candidate(NetworkInterfaceType.Unknown, "192.168.1.16"),
        };

        NetworkAddress.SelectAddress(candidates).Should().Be(IPAddress.Parse("192.168.1.16"));
    }

    [Fact]
    public void A_link_local_address_does_not_stop_a_later_interface_being_found()
    {
        // The Xamarinme NetworkHelper bug, kept pinned: it chose an interface first and filtered
        // addresses second, so this returned nothing.
        var candidates = new[]
        {
            Candidate(NetworkInterfaceType.Ethernet, "169.254.3.7"),
            Candidate(NetworkInterfaceType.Wireless80211, "192.168.1.16"),
        };

        NetworkAddress.SelectAddress(candidates).Should().Be(IPAddress.Parse("192.168.1.16"));
    }

    [Fact]
    public void A_platform_that_does_report_types_still_prefers_the_obvious_interface()
    {
        // Unknown is accepted, not preferred. On Windows, where the type is real, an unclassified
        // virtual adapter must not win over the actual Wi-Fi one just by enumerating first.
        var candidates = new[]
        {
            Candidate(NetworkInterfaceType.Unknown, "172.20.0.1"),
            Candidate(NetworkInterfaceType.Wireless80211, "192.168.1.16"),
        };

        NetworkAddress.SelectAddress(candidates).Should().Be(IPAddress.Parse("192.168.1.16"));
    }

    [Fact]
    public void An_interface_that_is_down_is_ignored()
    {
        var candidates = new[]
        {
            new InterfaceCandidate(
                NetworkInterfaceType.Wireless80211,
                OperationalStatus.Down,
                [IPAddress.Parse("192.168.1.16")]),
        };

        NetworkAddress.SelectAddress(candidates).Should().BeNull();
    }

    [Fact]
    public void Nothing_routable_reports_nothing()
    {
        var candidates = new[]
        {
            Candidate(NetworkInterfaceType.Unknown, "127.0.0.1"),
            Candidate(NetworkInterfaceType.Ethernet, "169.254.3.7"),
        };

        NetworkAddress.SelectAddress(candidates).Should().BeNull();
    }

    [Fact]
    public void This_machine_reports_an_address_if_it_has_one()
    {
        // The one test that touches the real machine. It cannot demand an address - a build agent
        // may genuinely have no network - so it asks whether one exists by the same rules and
        // requires the two answers to agree. That is close to restating the implementation, and it
        // is deliberately not the test carrying the weight; it is here because it is the check that
        // would have failed on Android, where an interface plainly held 192.168.1.16 and
        // GetLocalAddress returned null.
        var machineHasOne = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus is OperationalStatus.Up or OperationalStatus.Unknown)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Any(address => address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(address)
                && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal));

        var address = NetworkAddress.GetLocalAddress();

        if (machineHasOne)
        {
            address.Should().NotBeNull();
            address!.AddressFamily.Should().Be(AddressFamily.InterNetwork);
            IPAddress.IsLoopback(address).Should().BeFalse();
            address.ToString().Should().NotStartWith("169.254.");
        }
        else
        {
            address.Should().BeNull();
        }
    }

    private static InterfaceCandidate Candidate(NetworkInterfaceType type, string address) =>
        new(type, OperationalStatus.Up, [IPAddress.Parse(address)]);

    private sealed record Greeting(string Text);
}
