using System.Net;
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

    [Fact]
    public void The_local_address_is_routable_IPv4_or_nothing()
    {
        // Cannot assert that a machine has a network; it can assert that whatever comes back is
        // usable. The Xamarinme version this replaces could return null with a working connection,
        // because it filtered addresses only on the first qualifying interface.
        var address = NetworkAddress.GetLocalAddress();

        if (address is not null)
        {
            address.AddressFamily.Should().Be(AddressFamily.InterNetwork);
            address.ToString().Should().NotStartWith("169.254.");
        }
    }

    private sealed record Greeting(string Text);
}
