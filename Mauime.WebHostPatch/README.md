# Mauime.WebHostPatch

Run an ASP.NET Core web host and Kestrel server inside a .NET MAUI app.

Ported from `Xamarinme.WebHostPatch`. **The name is kept for continuity, but there is no patch any
more** — see below.

## Use

```csharp
using Mauime.WebHostPatch;

var builder = MauiApp.CreateBuilder();
builder.UseMauimeWebHost(options =>
{
    options.Port = 5000;                  // 0 lets the OS pick; the bound port is reported back
    options.ConfigureApplication = app => app.MapGet("/", () => "Hello from a phone");
});
```

The server is registered, not started. Start it when the app is ready:

```csharp
public sealed partial class MainPage(IMauimeWebHost host) : ContentPage
{
    private async void OnStartClicked(object sender, EventArgs e)
    {
        var address = await host.StartAsync();
        UrlLabel.Text = address.ToString();     // e.g. http://192.168.1.24:5000/
    }
}
```

`Address` is read from the server after it binds, so it carries the real port even when `Port` was
left at 0. When listening on every interface — the default — the reported host is the machine's LAN
address rather than `0.0.0.0`, so it is something you can type into another device.
`NetworkAddress.GetLocalAddress()` exposes that lookup directly.

`StopAsync` shuts the server down gracefully and frees the port; the host can be started again
afterwards. Disposing it stops it.

**Android** needs `android.permission.INTERNET`. Listening on a port under 1024 is not permitted on
either mobile platform, and iOS will stop a server that is running when the app is backgrounded.

## There is no patch any more

`Xamarinme.WebHostPatch` was two forks of Microsoft's code, because ASP.NET Core 2.2 on Mono needed
them:

- `Console.CancelKeyPress` threw, so `ConsoleLifetime` and `WebHostExtensions.RunAsync` were
  reimplemented around it.
- `Microsoft.Net.Http.Headers` 2.2.0 called an `InplaceStringBuilder` that
  `Microsoft.Extensions.Primitives` 5.0 had deleted, so a fork of Primitives stamped **5.9.0.0** was
  shipped to outrank the real one at bind time.

Neither cause survives on .NET 10, and neither fork came across. Running Kestrel inside a MAUI app
now needs a `Microsoft.AspNetCore.App` framework reference and nothing else — **this package has no
NuGet dependencies at all**. What is left is the part that was never the patch: starting and
stopping the thing from app code, and knowing what address to show the user.

If you are on `Xamarinme.WebHostPatch` 1.0.0, note that it pins ASP.NET Core 2.2.0, which carries a
critical advisory in `Microsoft.AspNetCore.Server.Kestrel.Core`
([GHSA-5rrx-jjjq-q2r5](https://github.com/advisories/GHSA-5rrx-jjjq-q2r5)) and a moderate one in
`Microsoft.AspNetCore.Server.IIS`
([GHSA-prrf-397v-83xh](https://github.com/advisories/GHSA-prrf-397v-83xh)). Its forked Primitives
also shadows the real assembly for anything in the same output directory.

## Licence

MIT.
